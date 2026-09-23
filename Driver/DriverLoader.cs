using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using ECController.Services;
using Microsoft.Win32.SafeHandles;

namespace ECController.Driver
{
    /// <summary>
    /// WinRing0 驱动的安装与启动。
    ///
    /// 加载流程：
    ///   1. 直接尝试打开设备（驱动可能已随系统启动）
    ///   2. 打开失败 -> 定位驱动文件（Drivers\ 优先，回退 exe 同目录）
    ///   3. 注册内核服务并启动
    ///   4. 再打开设备验证；失败则清理服务并重试
    /// </summary>
    public static class DriverLoader
    {
        /// <summary>WinRing0 设备名，供 EcAccess 拼接设备路径使用。</summary>
        public const string DriverDeviceName = "WinRing0_1_2_0";

        /// <summary>驱动服务名（注册表 / SCM 中使用的名字）。</summary>
        private const string DriverServiceName = "WinRing0_1_2_0";

        private const string DriverFileName = "WinRing0x64.sys";

        /// <summary>安装+验证的最大尝试次数。</summary>
        private const int MaxAttempt = 2;

        /// <summary>驱动目录名（exe 同级）。</summary>
        private const string DriverFolderName = "Drivers";

        private const string DevicePath = @"\\.\" + DriverDeviceName;

        /// <summary>
        /// 按优先级枚举候选驱动文件路径：Drivers\ 优先，回退 exe 同目录。
        /// </summary>
        public static IEnumerable<string> GetDriverCandidates()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            yield return Path.Combine(baseDir, DriverFolderName, DriverFileName);
            yield return Path.Combine(baseDir, DriverFileName);
        }

        /// <summary>
        /// 返回第一个实际存在的驱动文件路径；都不存在时返回 null。
        /// </summary>
        public static string ResolveDriverPath()
        {
            foreach (string candidate in GetDriverCandidates())
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// 确保驱动已加载且设备可打开。
        /// 成功返回；失败抛出 <see cref="DriverLoadException"/>，内含排查建议。
        /// </summary>
        public static void InitializeDriver()
        {
            Logger.Info("开始初始化 WinRing0 驱动。");

            string lastOpenError = null;

            for (int attempt = 1; attempt <= MaxAttempt; attempt++)
            {
                // 1) 驱动可能已经在运行，直接打开
                if (TryOpenDriver(out lastOpenError))
                {
                    Logger.Info("驱动设备已就绪，无需安装。");
                    return;
                }

                Logger.Warn(string.Format(
                    "第 {0} 次尝试：设备尚未就绪（{1}），准备安装驱动。",
                    attempt,
                    lastOpenError));

                // 2) 定位驱动文件
                string driverPath = ResolveDriverPath();

                if (driverPath == null)
                {
                    string searched = string.Join(
                        "、",
                        new List<string>(GetDriverCandidates()).ToArray());

                    Logger.Error("找不到驱动文件，已查找：" + searched);

                    throw new DriverLoadException(
                        "找不到 WinRing0 驱动文件。",
                        new[]
                        {
                            "驱动文件名：" + DriverFileName,
                            "已查找位置：" + searched,
                            "请确认驱动文件随程序一起分发，且未被安全软件隔离。"
                        });
                }

                Logger.Info("使用驱动文件：" + driverPath);

                // 3) 安装并启动服务
                string installError = InstallAndStartDriver(driverPath);

                if (installError != null)
                {
                    Logger.Error("驱动安装失败：" + installError);

                    // 服务状态可能已损坏，清掉后重试
                    DeleteDriverService();
                    Thread.Sleep(1000);
                    continue;
                }

                Thread.Sleep(1000);

                // 4) 再次验证设备可打开
                if (TryOpenDriver(out lastOpenError))
                {
                    Logger.Info("驱动安装成功，设备已可打开。");
                    return;
                }

                Logger.Warn("驱动服务已启动，但设备仍打不开：" + lastOpenError);

                DeleteDriverService();
                Thread.Sleep(1000);
            }

            Logger.Error("驱动初始化失败，最后一次错误：" + lastOpenError);

            throw new DriverLoadException(
                "WinRing0 驱动初始化失败，无法打开 EC 访问设备。",
                new[]
                {
                    lastOpenError ?? "设备打开失败，未返回具体错误。",
                    "",
                    "请按顺序排查：",
                    "1. 驱动文件是否存在：Drivers\\" + DriverFileName + " 或 exe 同目录",
                    "2. 是否以管理员身份运行本程序",
                    "3. 安全软件是否拦截了驱动加载（可临时加入白名单）",
                    "4. 系统是否禁用了内核驱动签名或存在冲突的驱动",
                    "5. 详细过程见 Logs\\ 下的日志文件"
                });
        }

        /// <summary>
        /// 尝试打开驱动设备。成功返回 true；
        /// 失败返回 false 并通过 <paramref name="error"/> 输出原因。
        /// </summary>
        private static bool TryOpenDriver(out string error)
        {
            error = null;

            SafeFileHandle handle = new SafeFileHandle(
                NativeMethods.CreateFile(
                    DevicePath,
                    FileAccessFlags.GENERIC_READ | FileAccessFlags.GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    CreationDisposition.OPEN_EXISTING,
                    FileAttributesFlags.FILE_ATTRIBUTE_NORMAL,
                    IntPtr.Zero),
                true);

            if (handle.IsInvalid)
            {
                int code = Marshal.GetLastWin32Error();
                handle.Dispose();

                error = string.Format(
                    "CreateFile({0}) 失败，错误码 {1}：{2}",
                    DevicePath,
                    code,
                    EcAccessException.DescribeWin32Error(code));

                return false;
            }

            try
            {
                // 顺带确认句柄可用：设备对象不支持文件 ACL 查询时视为不可用
                File.GetAccessControl(DevicePath);
            }
            catch
            {
                // 查询 ACL 失败不代表设备不能用，忽略。
            }
            finally
            {
                handle.Dispose();
            }

            return true;
        }

        /// <summary>
        /// 注册并启动内核驱动服务。
        /// 成功返回 null，失败返回错误说明。
        ///
        /// 关键点：服务已存在时必须走 OpenService + StartService，
        /// 而不是当作失败——否则第二次运行程序永远无法加载驱动。
        /// </summary>
        private static string InstallAndStartDriver(string driverPath)
        {
            IntPtr manager = NativeMethods.OpenSCManager(
                null,
                null,
                ServiceControlManagerAccessRights.SC_MANAGER_ALL_ACCESS);

            if (manager == IntPtr.Zero)
            {
                int code = Marshal.GetLastWin32Error();

                return string.Format(
                    "OpenSCManager 失败，错误码 {0}：{1}",
                    code,
                    EcAccessException.DescribeWin32Error(code));
            }

            IntPtr service = IntPtr.Zero;

            try
            {
                // 先尝试创建服务
                service = NativeMethods.CreateService(
                    manager,
                    DriverServiceName,
                    DriverServiceName,
                    ServiceAccessRights.SERVICE_ALL_ACCESS,
                    ServiceType.SERVICE_KERNEL_DRIVER,
                    StartType.SERVICE_DEMAND_START,
                    ErrorControl.SERVICE_ERROR_NORMAL,
                    driverPath,
                    null,
                    null,
                    null,
                    null,
                    null);

                if (service == IntPtr.Zero)
                {
                    int code = Marshal.GetLastWin32Error();

                    // 服务已存在：不是我方错误，改为打开它
                    if (code == ERROR_SERVICE_EXISTS || code == ERROR_SERVICE_ALREADY_EXISTS)
                    {
                        Logger.Info("驱动服务已存在，改为直接打开。");

                        service = NativeMethods.OpenService(
                            manager,
                            DriverServiceName,
                            ServiceAccessRights.SERVICE_ALL_ACCESS);

                        if (service == IntPtr.Zero)
                        {
                            int openCode = Marshal.GetLastWin32Error();

                            return string.Format(
                                "驱动服务已存在但无法打开，错误码 {0}：{1}",
                                openCode,
                                EcAccessException.DescribeWin32Error(openCode));
                        }

                        // 已存在的服务可能指向旧的驱动路径（例如程序目录搬迁过）。
                        // 路径不对时 StartService 会以"找不到文件(2)"失败，
                        // 而且重试多少次都没用——必须先把 ImagePath 改正。
                        string pathFixError = EnsureServicePointsAtCurrentDriver(service, driverPath);

                        if (pathFixError != null)
                            return pathFixError;
                    }
                    else
                    {
                        return string.Format(
                            "CreateService 失败，错误码 {0}：{1}",
                            code,
                            EcAccessException.DescribeWin32Error(code));
                    }
                }

                // 启动服务
                if (!NativeMethods.StartService(service, 0, null))
                {
                    int code = Marshal.GetLastWin32Error();

                    if (code != ERROR_SERVICE_ALREADY_RUNNING)
                    {
                        return string.Format(
                            "StartService 失败，错误码 {0}：{1}",
                            code,
                            EcAccessException.DescribeWin32Error(code));
                    }

                    Logger.Info("驱动服务已在运行。");
                }

                // 放宽设备对象权限，让非提权进程也能打开（尽力而为，失败不影响结果）
                TryGrantDeviceAccess();

                return null;
            }
            finally
            {
                if (service != IntPtr.Zero)
                    NativeMethods.CloseServiceHandle(service);

                NativeMethods.CloseServiceHandle(manager);
            }
        }

        /// <summary>
        /// 确认已存在的服务指向当前这次要用的驱动文件。
        ///
        /// 典型场景：程序曾经装在 A 目录并注册过服务，后来移动到 B 目录。
        /// 服务注册表里的 ImagePath 仍指向 A 目录，StartService 会报
        /// "系统找不到指定的文件(2)"。这里检测到路径不一致就改正它。
        /// 返回 null 表示无需修正或修正成功，否则返回错误说明。
        /// </summary>
        private static string EnsureServicePointsAtCurrentDriver(
            IntPtr service,
            string driverPath)
        {
            string currentPath;

            if (!TryQueryServiceImagePath(service, out currentPath))
            {
                // 查不到就当它没问题，交给后面的 StartService 去报错
                return null;
            }

            // 服务里存的是 \??\D:\... 这种原生路径，比较时统一去掉前缀
            string normalized = NormalizeImagePath(currentPath);
            string wanted = NormalizeImagePath(driverPath);

            if (string.Equals(normalized, wanted, StringComparison.OrdinalIgnoreCase))
                return null;

            Logger.Warn("服务注册的驱动路径已失效：" + normalized);
            Logger.Info("将其修正为：" + wanted);

            if (!NativeMethods.ChangeServiceConfig(
                service,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                driverPath,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                null))
            {
                int code = Marshal.GetLastWin32Error();

                return string.Format(
                    "驱动服务指向了失效路径（{0}），且无法自动修正，错误码 {1}：{2}",
                    normalized,
                    code,
                    EcAccessException.DescribeWin32Error(code));
            }

            Logger.Info("驱动服务路径已修正。");

            return null;
        }

        /// <summary>查询服务的 ImagePath。失败返回 false。</summary>
        private static bool TryQueryServiceImagePath(IntPtr service, out string path)
        {
            path = null;

            uint needed = 0;

            // 先问需要多大缓冲区
            NativeMethods.QueryServiceConfig(
                service,
                IntPtr.Zero,
                0,
                ref needed);

            if (needed == 0)
            {
                Logger.Warn("查询服务配置未返回缓冲区大小，跳过驱动路径校验。");
                return false;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)needed);

            try
            {
                uint size = needed;

                if (!NativeMethods.QueryServiceConfig(service, buffer, size, ref size))
                {
                    int queryCode = Marshal.GetLastWin32Error();

                    Logger.Warn(string.Format(
                        "查询服务配置失败（错误码 {0}：{1}），跳过驱动路径校验。",
                        queryCode,
                        EcAccessException.DescribeWin32Error(queryCode)));
                    return false;
                }

                // 布局细节与坑见 QueryServiceConfigLayout 的注释：
                // 路径指针不在偏移 0（那里是 dwServiceType/dwStartType），
                // 按偏移 0 读会得到野指针，解引用直接 AccessViolationException 杀掉进程。
                path = QueryServiceConfigLayout.ReadBinaryPathName(buffer, (int)size);

                if (string.IsNullOrEmpty(path))
                {
                    Logger.Warn("服务配置中读不到有效的驱动路径，跳过路径校验。");
                    return false;
                }

                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>把 \??\C:\x\y.sys / \\?\C:\x\y.sys 统一成 C:\x\y.sys 形式再比较。</summary>
        private static string NormalizeImagePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string result = path.Trim().Trim('"');

            if (result.StartsWith(@"\??\", StringComparison.Ordinal))
                result = result.Substring(4);
            else if (result.StartsWith(@"\\?\", StringComparison.Ordinal))
                result = result.Substring(4);

            return result.Replace('/', '\\');
        }

        private static void TryGrantDeviceAccess()
        {
            try
            {
                FileSecurity security = File.GetAccessControl(DevicePath);

                security.SetSecurityDescriptorSddlForm(
                    "O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");

                File.SetAccessControl(DevicePath, security);

                Logger.Info("已调整驱动设备访问权限。");
            }
            catch (Exception ex)
            {
                // 权限调整失败不阻断流程——管理员运行通常已足够。
                Logger.Warn("调整驱动设备权限失败（不影响继续）：" + ex.Message);
            }
        }

        /// <summary>停止并删除驱动服务。用于安装失败后的清理。</summary>
        public static void DeleteDriverService()
        {
            IntPtr manager = NativeMethods.OpenSCManager(
                null,
                null,
                ServiceControlManagerAccessRights.SC_MANAGER_ALL_ACCESS);

            if (manager == IntPtr.Zero)
                return;

            IntPtr service = IntPtr.Zero;

            try
            {
                service = NativeMethods.OpenService(
                    manager,
                    DriverServiceName,
                    ServiceAccessRights.SERVICE_ALL_ACCESS);

                if (service == IntPtr.Zero)
                    return;

                ServiceStatus status = new ServiceStatus();
                NativeMethods.ControlService(
                    service,
                    ServiceControl.SERVICE_CONTROL_STOP,
                    ref status);

                NativeMethods.DeleteService(service);

                Logger.Info("已删除驱动服务。");
            }
            finally
            {
                if (service != IntPtr.Zero)
                    NativeMethods.CloseServiceHandle(service);

                NativeMethods.CloseServiceHandle(manager);
            }
        }

        #region Native and Structs

        private enum ServiceAccessRights : uint
        {
            SERVICE_ALL_ACCESS = 0xF01FF
        }

        private enum ServiceControlManagerAccessRights : uint
        {
            SC_MANAGER_ALL_ACCESS = 0xF003F
        }

        private enum ServiceType : uint
        {
            SERVICE_KERNEL_DRIVER = 1
        }

        private enum StartType : uint
        {
            /// <summary>按需启动——加载后即可用，不强制开机自启。</summary>
            SERVICE_DEMAND_START = 3
        }

        private enum ErrorControl : uint
        {
            SERVICE_ERROR_NORMAL = 1
        }

        private enum ServiceControl : uint
        {
            SERVICE_CONTROL_STOP = 1
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ServiceStatus
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        private enum FileAccessFlags : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000
        }

        private enum CreationDisposition : uint
        {
            OPEN_EXISTING = 3
        }

        private enum FileAttributesFlags : uint
        {
            FILE_ATTRIBUTE_NORMAL = 0x80
        }

        private const int
            ERROR_SERVICE_EXISTS = 1073,          // 0x431
            ERROR_SERVICE_ALREADY_EXISTS = 1073,
            ERROR_SERVICE_ALREADY_RUNNING = 1056; // 0x420

        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        private static class NativeMethods
        {
            private const string ADVAPI = "advapi32.dll";
            private const string KERNEL = "kernel32.dll";

            [DllImport(ADVAPI, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenSCManager(
                string machineName,
                string databaseName,
                ServiceControlManagerAccessRights dwAccess);

            [DllImport(ADVAPI, SetLastError = true)]
            public static extern bool CloseServiceHandle(IntPtr hSCObject);

            [DllImport(ADVAPI, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateService(
                IntPtr hSCManager,
                string lpServiceName,
                string lpDisplayName,
                ServiceAccessRights dwDesiredAccess,
                ServiceType dwServiceType,
                StartType dwStartType,
                ErrorControl dwErrorControl,
                string lpBinaryPathName,
                string lpLoadOrderGroup,
                string lpdwTagId,
                string lpDependencies,
                string lpServiceStartName,
                string lpPassword);

            [DllImport(ADVAPI, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenService(
                IntPtr hSCManager,
                string lpServiceName,
                ServiceAccessRights dwDesiredAccess);

            [DllImport(ADVAPI, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool ChangeServiceConfig(
                IntPtr hService,
                uint dwServiceType,
                uint dwStartType,
                uint dwErrorControl,
                string lpBinaryPathName,
                string lpLoadOrderGroup,
                IntPtr lpdwTagId,
                string lpDependencies,
                string lpServiceStartName,
                string lpPassword,
                string lpDisplayName);

            [DllImport(ADVAPI, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool QueryServiceConfig(
                IntPtr hService,
                IntPtr lpServiceConfig,
                uint cbBufSize,
                ref uint pcbBytesNeeded);

            [DllImport(ADVAPI, SetLastError = true)]
            public static extern bool DeleteService(IntPtr hService);

            [DllImport(ADVAPI, SetLastError = true)]
            public static extern bool StartService(
                IntPtr hService,
                uint dwNumServiceArgs,
                string[] lpServiceArgVectors);

            [DllImport(ADVAPI, SetLastError = true)]
            public static extern bool ControlService(
                IntPtr hService,
                ServiceControl dwControl,
                ref ServiceStatus lpServiceStatus);

            [DllImport(KERNEL, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateFile(
                string lpFileName,
                FileAccessFlags dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                CreationDisposition dwCreationDisposition,
                FileAttributesFlags dwFlagsAndAttributes,
                IntPtr hTemplateFile);
        }

        #endregion
    }

    /// <summary>
    /// 驱动加载失败异常。附带可直接展示给用户的排查步骤列表。
    /// </summary>
    public class DriverLoadException : Exception
    {
        /// <summary>面向用户的排查步骤/原因说明行。</summary>
        public string[] Details { get; private set; }

        public DriverLoadException(string message, string[] details)
            : base(message + (details == null || details.Length == 0
                ? string.Empty
                : Environment.NewLine + Environment.NewLine + string.Join(
                    Environment.NewLine,
                    details)))
        {
            Details = details ?? new string[0];
        }
    }
}
