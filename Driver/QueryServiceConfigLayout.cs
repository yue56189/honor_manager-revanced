using System;
using System.Runtime.InteropServices;

namespace ECController.Driver
{
    /// <summary>
    /// QUERY_SERVICE_CONFIGW 的内存布局解析。
    ///
    /// <para>
    /// <b>为什么单独拿出来：</b>这块布局被写错过一次，后果是进程无日志、无弹窗地直接消失，
    /// 极难定位。因此把它隔离成一个不依赖任何东西的小类，并用单元测试钉死偏移量。
    /// </para>
    ///
    /// <para>
    /// 结构体字段顺序（Win32 定义，注意 <c>lpBinaryPathName</c> <b>不是</b>第一个字段）：
    /// </para>
    /// <code>
    ///   DWORD  dwServiceType;
    ///   DWORD  dwStartType;
    ///   DWORD  dwErrorControl;
    ///   LPWSTR lpBinaryPathName;   // ← 第 4 个字段，x64 偏移 16 / x86 偏移 12
    ///   LPWSTR lpLoadOrderGroup;
    ///   ...
    /// </code>
    ///
    /// <para>
    /// <b>踩过的坑（2026-09-23 实测）：</b>按「第一个字段就是路径指针」写成
    /// <c>Marshal.ReadIntPtr(buffer, 0)</c>，读到的实际是
    /// <c>dwServiceType | (dwStartType &lt;&lt; 32)</c>，本例中为 <c>0x0000000300000001</c>。
    /// 把它交给 <c>Marshal.PtrToStringUni</c> 去解引用 → <c>AccessViolationException</c>；
    /// 而该异常在 .NET 4 默认属于「损坏状态异常」，<b>连 catch (Exception) 都拦不住</b>，
    /// 进程当场终止，只在事件日志里留一条 .NET Runtime 1026。
    /// </para>
    /// </summary>
    internal static class QueryServiceConfigLayout
    {
        /// <summary>QUERY_SERVICE_CONFIGW 前 4 个字段，只为算出 lpBinaryPathName 的偏移。</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Prefix
        {
            public uint dwServiceType;
            public uint dwStartType;
            public uint dwErrorControl;
            public IntPtr lpBinaryPathName;
        }

        /// <summary>
        /// lpBinaryPathName 在缓冲区中的字节偏移。由编译器按平台算出来，
        /// 不写死数字，避免 32/64 位各写一个常量。
        /// </summary>
        public static readonly int BinaryPathNameOffset =
            (int)Marshal.OffsetOf(typeof(Prefix), "lpBinaryPathName");

        /// <summary>
        /// 从 QueryServiceConfig 返回的缓冲区里读出 lpBinaryPathName。
        ///
        /// <para>
        /// 解引用之前必须校验指针落在缓冲区内：QueryServiceConfig 返回的是
        /// 一整块连续内存（定长结构 + 紧随其后的字符串），路径指针必然指向块内。
        /// 一旦越界，说明布局判断有误 —— 这时返回 null，绝不拿野指针去读字符串。
        /// </para>
        /// </summary>
        /// <param name="buffer">QueryServiceConfig 填充的缓冲区。</param>
        /// <param name="bufferSize">该次调用实际使用的字节数。</param>
        /// <returns>驱动路径；无法安全读取时返回 null。</returns>
        public static string ReadBinaryPathName(IntPtr buffer, int bufferSize)
        {
            if (buffer == IntPtr.Zero || bufferSize <= 0)
                return null;

            if (bufferSize < BinaryPathNameOffset + IntPtr.Size)
                return null;

            IntPtr pathPtr = Marshal.ReadIntPtr(buffer, BinaryPathNameOffset);

            if (pathPtr == IntPtr.Zero)
                return null;

            long start = buffer.ToInt64();
            long target = pathPtr.ToInt64();

            // 字符串是本块内存的一部分；落在块外一律视为布局误判。
            if (target < start || target >= start + bufferSize)
                return null;

            return Marshal.PtrToStringUni(pathPtr);
        }
    }
}
