"""校验发布产物的 PE 资源：图标存在、manifest 内嵌为 requireAdministrator。

资源目录结构（三层：Type -> Name/ID -> Language），每个目录项 8 字节：
    DWORD NameOrId   (高位 1 = 字符串名，指向 UTF-16 串；0 = 整数资源 ID)
    DWORD Offset     (高位 1 = 指向子目录，低位 1 = 指向数据项)
所有 Offset 都相对资源目录根起点。
数据项 16 字节：DWORD DataRVA, DWORD Size, DWORD CodePage, DWORD Reserved
"""
import struct
import sys


def load(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:2] != b"MZ":
        raise ValueError("not a PE file")
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("bad PE signature")
    coff = pe + 4
    nsec = struct.unpack_from("<H", data, coff + 2)[0]
    optsz = struct.unpack_from("<H", data, coff + 16)[0]
    opt = coff + 20
    magic = struct.unpack_from("<H", data, opt)[0]
    dd = opt + (112 if magic == 0x20B else 96)
    rsrc_rva, rsrc_size = struct.unpack_from("<II", data, dd + 2 * 8)

    sec = opt + optsz
    secs = []
    for i in range(nsec):
        s = sec + i * 40
        # 节头：Name(8) VirtualSize(4) VirtualAddress(4) SizeOfRawData(4) PointerToRawData(4)
        vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII", data, s + 8)
        secs.append((vaddr, vsize, rawptr, rawsize))
    return data, rsrc_rva, rsrc_size, secs, magic


def rva2off(rva, secs):
    for vaddr, vsize, rawptr, rawsize in secs:
        if vaddr <= rva < vaddr + max(vsize, rawsize):
            return rawptr + (rva - vaddr)
    return None


def walk(data, root, node_off):
    """产出 (资源ID 或 None, 是否为子目录, 子节点偏移 或 数据项偏移)。"""
    named, idcnt = struct.unpack_from("<HH", data, node_off + 12)
    for i in range(named + idcnt):
        e = node_off + 16 + i * 8
        name_val, off_val = struct.unpack_from("<II", data, e)
        rid = None if (name_val & 0x80000000) else name_val
        is_sub = bool(off_val & 0x80000000)
        target = root + (off_val & 0x7FFFFFFF)  # 偏移始终相对资源目录根
        yield rid, is_sub, target


def find_data(data, root, type_id):
    """取指定资源类型下第一个数据项的 (RVA, Size)。"""
    type_dir = None
    for rid, is_sub, off in walk(data, root, root):
        if rid == type_id and is_sub:
            type_dir = off
            break
    if type_dir is None:
        return None
    for _name, is_sub, off in walk(data, root, type_dir):
        if not is_sub:
            continue
        # 语言层
        for _lang, sub2, off2 in walk(data, root, off):
            if sub2:
                continue
            rva, size = struct.unpack_from("<II", data, off2)
            return rva, size
    return None


def count_images(data, root, type_id):
    type_dir = None
    for rid, is_sub, off in walk(data, root, root):
        if rid == type_id and is_sub:
            type_dir = off
            break
    if type_dir is None:
        return 0
    return len([c for c in walk(data, root, type_dir) if c[1]])


def main():
    path = sys.argv[1]
    data, rsrc_rva, rsrc_size, secs, magic = load(path)
    print("发布产物校验：" + path)
    print("  PE 类型      = " + ("PE32+" if magic == 0x20B else "PE32"))
    print("  资源表 RVA   = 0x%X  大小 = %d 字节" % (rsrc_rva, rsrc_size))
    print()

    root = rva2off(rsrc_rva, secs)
    if root is None:
        print("  [失败] 资源表无法映射到文件偏移")
        return 1

    types = [rid for rid, is_sub, _ in walk(data, root, root) if rid is not None and is_sub]
    print("  资源类型 ID：" + (", ".join(str(t) for t in sorted(types)) if types else "(无)"))
    print()

    RT_ICON, RT_GROUP_ICON, RT_MANIFEST, RT_VERSION = 3, 14, 24, 16
    ok = True

    if RT_ICON in types:
        n = count_images(data, root, RT_ICON)
        print("  [通过] 图标资源 RT_ICON=" + str(RT_ICON) + "，共 " + str(n) + " 个图像")
    else:
        print("  [失败] 缺少图标资源 RT_ICON")
        ok = False

    if RT_GROUP_ICON in types:
        n = count_images(data, root, RT_GROUP_ICON)
        print("  [通过] 图标组 RT_GROUP_ICON=" + str(RT_GROUP_ICON) + "，共 " + str(n) + " 组")
    else:
        print("  [失败] 缺少图标组 RT_GROUP_ICON")
        ok = False

    if RT_VERSION in types:
        print("  [通过] 版本资源 RT_VERSION=" + str(RT_VERSION))
    else:
        print("  [警告] 缺少版本资源 RT_VERSION")

    if RT_MANIFEST not in types:
        print("  [失败] 缺少 manifest 资源 RT_MANIFEST")
        print()
        return 1

    found = find_data(data, root, RT_MANIFEST)
    if found is None:
        print("  [失败] 无法读取 manifest 数据项")
        print()
        return 1

    m_rva, m_size = found
    moff = rva2off(m_rva, secs)
    manifest = data[moff:moff + m_size].decode("utf-8", "replace")
    print("  manifest 长度 = " + str(m_size) + " 字节")

    if "requireAdministrator" in manifest:
        print("  [通过] manifest 声明 requireAdministrator")
    elif "asInvoker" in manifest:
        print("  [失败] manifest 是 asInvoker（不会提权）")
        ok = False
    else:
        print("  [失败] manifest 未声明 requestedExecutionLevel")
        ok = False

    if "dpiAware" in manifest:
        print("  [通过] manifest 含 dpiAware")
    else:
        print("  [警告] manifest 未含 dpiAware")

    for line in manifest.splitlines():
        if "requestedExecutionLevel" in line or "dpiAware" in line:
            print("         " + line.strip())

    print()
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
