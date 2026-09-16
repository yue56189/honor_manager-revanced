using System;
using System.Runtime.InteropServices;
using System.Threading;
using ECController.Services;

namespace ECController.Driver
{
    /// <summary>
    /// EC（嵌入式控制器）寄存器读写。通过 WinRing0 驱动的端口 IOCTL 访问
    /// 标准 ACPI EC 接口：命令/状态口 0x66，数据口 0x62。
    ///
    /// 生命周期：Init() 打开驱动设备句柄 -> ReadEC/WriteEC -> Dispose() 关闭。
    /// 所有公开方法都不是线程安全的，调用方需自行串行化（MainForm 用 EcService 串行）。
    /// </summary>
    public sealed class EcAccess : IDisposable
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;

        private const uint OLS_TYPE = 40000;
        private const uint METHOD_BUFFERED = 0;
        private const uint FILE_READ_ACCESS = 0x0001;
        private const uint FILE_WRITE_ACCESS = 0x0002;

        private const byte EC_SC = 0x66;
        private const byte EC_DATA = 0x62;

        /// <summary>等待 EC 状态口就绪的重试次数。</summary>
        private const int StatusRetry = 100;

        /// <summary>状态口两次轮询之间的间隔（毫秒）。</summary>
        private const int StatusPollDelayMs = 5;

        private const uint IOCTL_READ_PORT = 2621464780;   // CTL_CODE(40000, 0x09, 0, 0x0001)
        private const uint IOCTL_WRITE_PORT = 2621481176;  // CTL_CODE(40000, 0x0A, 0, 0x0002)

        private IntPtr _handle = InvalidHandle;

        private static readonly IntPtr InvalidHandle = new IntPtr(-1);

        /// <summary>驱动设备是否已成功打开。</summary>
        public bool IsOpen
        {
            get { return _handle != IntPtr.Zero && _handle != InvalidHandle; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WriteIoPortInput
        {
            public uint PortNumber;
            public byte Value;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ReadIoPortInput
        {
            public uint PortNumber;
        }

        /// <summary>
        /// 打开 WinRing0 设备。失败抛出 <see cref="EcAccessException"/>，
        /// 其中带 Win32 错误码与可读解释。
        /// </summary>
        public void Init()
        {
            if (IsOpen)
                return;

            _handle = NativeMethods.CreateFile(
                @"\\.\" + DriverLoader.DriverDeviceName,
                GENERIC_READ | GENERIC_WRITE,
                0,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (_handle == InvalidHandle)
            {
                int err = Marshal.GetLastWin32Error();
                _handle = IntPtr.Zero;

                throw new EcAccessException(
                    "打开 WinRing0 设备失败。",
                    err,
                    @"\\.\" + DriverLoader.DriverDeviceName);
            }

            Logger.Info("EC 驱动设备已打开：" + @"\\.\" + DriverLoader.DriverDeviceName);
        }

        /// <summary>写入一个 EC 寄存器。地址与数据都是 8 位。</summary>
        /// <param name="address">EC 寄存器地址。</param>
        /// <param name="data">要写入的值。</param>
        /// <param name="wait">每次端口操作后的等待毫秒数，给 EC 留出处理时间。</param>
        public void WriteEC(byte address, byte data, int wait)
        {
            EnsureOpen();

            Logger.Info(string.Format("WriteEC 开始：0x{0:X2} = 0x{1:X2}", address, data));

            WaitIbfClear(wait);
            WritePort(EC_SC, 0x81);
            Sleep(wait);

            WaitIbfClear(wait);
            WritePort(EC_DATA, address);
            Sleep(wait);

            WaitIbfClear(wait);
            WritePort(EC_DATA, data);
            Sleep(wait);

            WaitIbfClear(wait);

            Logger.Info(string.Format("WriteEC 完成：0x{0:X2} = 0x{1:X2}", address, data));
        }

        /// <summary>读取一个 EC 寄存器。</summary>
        public byte ReadEC(byte address, int wait)
        {
            return ReadEC(address, wait, false);
        }

        /// <summary>
        /// 读取一个 EC 寄存器。
        /// </summary>
        /// <param name="address">寄存器地址。</param>
        /// <param name="wait">端口操作之间的等待毫秒数。</param>
        /// <param name="discardFirst">
        /// 是否先丢弃一次读取结果。
        ///
        /// 某些 EC 寄存器（实测本机 0x78）是"写入触发型"：写入后第一次读
        /// 会返回上一次的旧值，第二次读才是新值。校验这类寄存器时必须
        /// 丢弃首次读数，否则会把成功的写入误判为失败。
        /// </param>
        public byte ReadEC(byte address, int wait, bool discardFirst)
        {
            EnsureOpen();

            if (discardFirst)
            {
                // 第一次读到的可能是上一轮遗留的陈旧数据，直接丢掉
                byte stale = ReadOnce(address, wait);

                Logger.Info(string.Format(
                    "ReadEC 丢弃首次读数：0x{0:X2} = 0x{1:X2}（可能为旧值）",
                    address,
                    stale));
            }

            return ReadOnce(address, wait);
        }

        private byte ReadOnce(byte address, int wait)
        {
            WaitIbfClear(wait);
            WritePort(EC_SC, 0x80);
            Sleep(wait);

            WaitIbfClear(wait);
            WritePort(EC_DATA, address);
            Sleep(wait);

            // 等待 OBF(bit0)=1，表示 EC 已把数据放到数据口
            bool ready = false;

            for (int i = 0; i < StatusRetry; i++)
            {
                byte status = ReadPort(EC_SC);

                if ((status & 0x01) != 0)
                {
                    ready = true;
                    break;
                }

                Thread.Sleep(StatusPollDelayMs);
            }

            if (!ready)
            {
                // 超时不直接抛异常：EC 某些寄存器本来就不响应 OBF。
                // 记录警告后仍读一次数据口，由调用方的回读校验判定真假。
                Logger.Warn(string.Format(
                    "读取 EC 0x{0:X2} 时等待 OBF 超时，仍尝试读取数据口。",
                    address));
            }

            byte value = ReadPort(EC_DATA);

            Logger.Info(string.Format("ReadEC：0x{0:X2} = 0x{1:X2}", address, value));

            return value;
        }

        private void Sleep(int wait)
        {
            if (wait > 0)
                Thread.Sleep(wait);
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new EcAccessException(
                    "EC 设备尚未初始化，请先调用 Init()。",
                    0,
                    null);
            }
        }

        /// <summary>等待输入缓冲区空（IBF=0），即 EC 可以接受新命令。</summary>
        private void WaitIbfClear(int wait)
        {
            for (int i = 0; i < StatusRetry; i++)
            {
                byte status = ReadPort(EC_SC);

                if ((status & 0x02) == 0) // IBF = 0
                {
                    Sleep(wait);
                    return;
                }

                Thread.Sleep(StatusPollDelayMs);
            }

            throw new EcAccessException(
                "等待 EC 输入缓冲区清空（IBF）超时，EC 可能无响应。",
                0,
                null);
        }

        private byte ReadPort(byte port)
        {
            ReadIoPortInput input = new ReadIoPortInput { PortNumber = port };
            byte[] outBuffer = new byte[1];
            uint bytesReturned = 0;

            bool success = NativeMethods.DeviceIoControl(
                _handle,
                IOCTL_READ_PORT,
                GetBytes(input),
                (uint)Marshal.SizeOf(input),
                outBuffer,
                (uint)outBuffer.Length,
                ref bytesReturned,
                IntPtr.Zero);

            if (!success)
            {
                throw new EcAccessException(
                    "DeviceIoControl 读取端口失败。",
                    Marshal.GetLastWin32Error(),
                    "port 0x" + port.ToString("X2"));
            }

            return outBuffer[0];
        }

        private void WritePort(byte port, byte value)
        {
            WriteIoPortInput input = new WriteIoPortInput
            {
                PortNumber = port,
                Value = value
            };

            uint bytesReturned = 0;

            bool success = NativeMethods.DeviceIoControl(
                _handle,
                IOCTL_WRITE_PORT,
                GetBytes(input),
                (uint)Marshal.SizeOf(input),
                null,
                0,
                ref bytesReturned,
                IntPtr.Zero);

            if (!success)
            {
                throw new EcAccessException(
                    "DeviceIoControl 写入端口失败。",
                    Marshal.GetLastWin32Error(),
                    "port 0x" + port.ToString("X2"));
            }
        }

        private static byte[] GetBytes<T>(T str) where T : struct
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return arr;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero && _handle != InvalidHandle)
            {
                NativeMethods.CloseHandle(_handle);
                Logger.Info("EC 驱动设备句柄已关闭。");
            }

            _handle = IntPtr.Zero;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateFile(
                string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool DeviceIoControl(
                IntPtr hDevice,
                uint dwIoControlCode,
                byte[] lpInBuffer,
                uint nInBufferSize,
                [Out] byte[] lpOutBuffer,
                uint nOutBufferSize,
                ref uint lpBytesReturned,
                IntPtr lpOverlapped);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);
        }
    }

    /// <summary>
    /// EC 访问异常。携带 Win32 错误码，便于上层区分"权限不足/设备不存在/超时"等情况。
    /// </summary>
    public class EcAccessException : Exception
    {
        /// <summary>Win32 错误码，0 表示不适用。</summary>
        public int Win32ErrorCode { get; private set; }

        /// <summary>出错的上下文对象（设备路径或端口）。</summary>
        public string Context { get; private set; }

        public EcAccessException(string message, int win32ErrorCode, string context)
            : base(BuildMessage(message, win32ErrorCode, context))
        {
            Win32ErrorCode = win32ErrorCode;
            Context = context;
        }

        private static string BuildMessage(string message, int code, string context)
        {
            string text = message;

            if (code != 0)
            {
                text += string.Format(
                    " 错误码 {0}（0x{1:X8}）：{2}",
                    code,
                    code,
                    DescribeWin32Error(code));
            }

            if (!string.IsNullOrEmpty(context))
            {
                text += " [目标：" + context + "]";
            }

            return text;
        }

        /// <summary>把常见的 Win32 错误码翻译成用户能看懂的说明。</summary>
        public static string DescribeWin32Error(int code)
        {
            switch (code)
            {
                case 2:
                    return "系统找不到指定的文件——通常是 WinRing0 驱动未安装或未启动。";
                case 3:
                    return "系统找不到指定的路径。";
                case 5:
                    return "拒绝访问——需要以管理员身份运行本程序。";
                case 32:
                    return "文件正被另一个进程占用。";
                case 87:
                    return "参数错误。";
                case 1058:
                    return "服务已被禁用，无法启动。";
                case 1060:
                    return "指定的服务未安装。";
                case 1063:
                    return "服务进程无法启动。";
                case 1275:
                    return "驱动被系统策略阻止加载（可能被安全软件拦截）。";
                case 577:
                case 1279:
                    return "驱动签名验证失败，系统拒绝了该驱动。";
                default:
                    return "未识别的错误，请查看日志。";
            }
        }
    }
}
