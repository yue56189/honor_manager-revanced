import struct, sys, os

# 解析 PE 文件的 VS_FIXEDFILEINFO，取文件版本号（不依赖 PowerShell）。
def get_file_version(path):
    with open(path, "rb") as f:
        data = f.read()

    # PE 头
    pe_off = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe_off:pe_off+4] != b"PE\0\0":
        return None

    num_sections = struct.unpack_from("<H", data, pe_off + 6)[0]
    opt_size = struct.unpack_from("<H", data, pe_off + 20)[0]
    opt_off = pe_off + 24

    # 数据目录：资源表是第 3 项（索引 2）
    magic = struct.unpack_from("<H", data, opt_off)[0]
    dd_off = opt_off + (112 if magic == 0x20b else 96)
    res_rva, res_size = struct.unpack_from("<II", data, dd_off + 2 * 8)

    section_off = opt_off + opt_size
    sections = []
    for i in range(num_sections):
        off = section_off + i * 40
        name = data[off:off+8].rstrip(b"\0").decode("ascii", "replace")
        vaddr, vsize = struct.unpack_from("<II", data, off + 12)
        raw_size, raw_ptr = struct.unpack_from("<II", data, off + 16)
        sections.append((vaddr, vsize, raw_ptr, raw_size))

    def rva_to_off(rva):
        for vaddr, vsize, raw_ptr, raw_size in sections:
            if vaddr <= rva < vaddr + max(vsize, raw_size):
                return raw_ptr + (rva - vaddr)
        return None

    # 遍历资源树找 RT_VERSION (16)
    root = rva_to_off(res_rva)
    if root is None:
        return None

    def entries(off):
        nnamed, nid = struct.unpack_from("<HH", data, off + 12)
        out = []
        for i in range(nnamed + nid):
            e = off + 16 + i * 8
            name_id, data_off = struct.unpack_from("<II", data, e)
            out.append((name_id, data_off))
        return out

    for name_id, sub in entries(root):
        if name_id != 16:
            continue
        for _, sub2 in entries(root + (sub & 0x7FFFFFFF)):
            for _, sub3 in entries(root + (sub2 & 0x7FFFFFFF)):
                data_entry = root + (sub3 & 0x7FFFFFFF)
                data_rva, data_len = struct.unpack_from("<II", data, data_entry)
                blob_off = rva_to_off(data_rva)
                if blob_off is None:
                    return None

                # VS_VERSIONINFO -> VS_FIXEDFILEINFO 在头部对齐后
                pos = blob_off
                # 跳过 wLength, wValueLength, wType, szKey("VS_VERSION_INFO")
                wLength, wValueLength = struct.unpack_from("<HH", data, pos)
                key = data[pos+6:pos+6+32].decode("utf-16-le", "replace")
                key_end = pos + 6
                while data[key_end:key_end+2] != b"\0\0":
                    key_end += 2
                key_end += 2
                while key_end % 4 != 0:
                    key_end += 1

                sig, ver, fv_hi, fv_lo = struct.unpack_from("<IIII", data, key_end)
                if sig != 0xFEEF04BD:
                    return None
                ms, ls = fv_hi, fv_lo
                return "%d.%d.%d.%d" % (ms >> 16, ms & 0xFFFF, ls >> 16, ls & 0xFFFF)

    return None


for exe in sys.argv[1:]:
    v = get_file_version(exe)
    print("%s\n    FileVersion = %s" % (exe, v if v else "(未设置)"))
