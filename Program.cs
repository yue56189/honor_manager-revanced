using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using ECController.Config;
using ECController.Models;
using ECController.Services;

namespace ECController
{
    internal static class Program
    {
        /// <summary>静默模式参数：登录自动应用时使用，不显示主界面。</summary>
        private const string ApplyOnlyArg = "--apply-only";

        /// <summary>强制显示界面（即使配置里勾了自动应用）。</summary>
        private const string ShowUiArg = "--show-ui";

        /// <summary>单实例互斥体名。</summary>
        private const string MutexName = @"Global\ECController_SingleInstance";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool applyOnly = HasArgument(args, ApplyOnlyArg) &&
                !HasArgument(args, ShowUiArg);

            // 非管理员时提权重启。静默模式同样需要提权，否则驱动装不上。
            if (!IsAdministrator())
            {
                if (!RestartAsAdministrator(args))
                {
                    ShowError(
                        "需要管理员权限",
                        "EC Controller 需要管理员权限才能加载 WinRing0 驱动并访问 EC。\n\n" +
                        "请在弹出用户账户控制提示时选择\"是\"。");

                    return;
                }

                return;
            }

            // 单实例检查：避免重复启动导致两个进程同时读写 EC
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    if (!applyOnly)
                    {
                        ShowError(
                            "程序已在运行",
                            "EC Controller 已经有一个实例在运行了。\n\n" +
                            "请在任务栏或系统托盘找到它，而不是重复启动。");
                    }
                    else
                    {
                        Logger.Warn("检测到已有实例在运行，跳过本次自动应用。");
                    }

                    return;
                }

                Run(args, applyOnly);
            }
        }

        private static void Run(string[] args, bool applyOnly)
        {
            AppSettings settings = SettingsManager.Load();

            Logger.Info("===== EC Controller 启动 =====");
            Logger.Info(string.Format(
                "模式：{0}，管理员：{1}",
                applyOnly ? "静默自动应用" : "图形界面",
                IsAdministrator()));

            if (applyOnly)
            {
                RunApplyOnly(settings);
                return;
            }

            // 把配置里的开机启动意图同步到注册表（用户可能手工改了 json）
            if (StartupManager.IsEnabled() != settings.AutoStart)
                StartupManager.Apply(settings.AutoStart);

            Application.Run(new MainForm(settings));
        }

        /// <summary>
        /// 登录自动应用：不开窗口，直接写 EC，结果写日志 + 托盘气泡。
        /// 失败也不能弹阻塞式对话框，否则会卡住登录流程。
        /// </summary>
        private static void RunApplyOnly(AppSettings settings)
        {
            if (!settings.ApplyOnBoot)
            {
                Logger.Info("配置未启用登录自动应用，退出。");
                return;
            }

            // 等系统与 EC 稳定下来
            int delay = settings.BootDelaySeconds;

            if (delay > 0)
            {
                Logger.Info(string.Format("等待 {0} 秒后开始自动应用。", delay));
                Thread.Sleep(delay * 1000);
            }

            ParseResult parse = AddressParser.Load();
            EcMode mode = FindMode(parse, settings.BootGroup, settings.BootMode);

            if (mode == null)
            {
                string detail = string.Format(
                    "找不到配置的模式：功能组 \"{0}\"，模式 \"{1}\"。",
                    settings.BootGroup,
                    settings.BootMode);

                Logger.Error("自动应用失败：" + detail);
                NotifyFailure("自动应用失败", detail);
                return;
            }

            using (EcService service = new EcService(settings.WaitTime))
            {
                try
                {
                    service.Initialize();
                }
                catch (Exception ex)
                {
                    Logger.Error("自动应用：驱动初始化失败。", ex);
                    NotifyFailure("自动应用失败", "驱动初始化失败：" + ex.Message);
                    return;
                }

                ApplyResult result = service.Apply(mode);

                if (result.Success)
                {
                    Logger.Info("自动应用成功：" + result.Message);
                    NotifyFailure("EC Controller", "已自动应用：" + mode.Name,
                        ToolTipIcon.Info);
                }
                else
                {
                    Logger.Error("自动应用未完全成功：" + result.Message);
                    NotifyFailure("自动应用未完全成功", result.Message);
                }
            }
        }

        /// <summary>在解析结果里按组名+模式名查找模式。</summary>
        private static EcMode FindMode(ParseResult parse, string groupName, string modeName)
        {
            if (parse == null || parse.Groups == null)
                return null;

            foreach (EcGroup group in parse.Groups)
            {
                if (!string.Equals(group.Name, groupName, StringComparison.Ordinal))
                    continue;

                foreach (EcMode mode in group.Modes)
                {
                    if (string.Equals(mode.Name, modeName, StringComparison.Ordinal))
                        return mode;
                }
            }

            return null;
        }

        /// <summary>
        /// 静默模式下的一次性提示：用一个临时托盘气泡，
        /// 不阻塞登录。失败时也只在日志留痕。
        /// </summary>
        private static void NotifyFailure(string title, string text)
        {
            NotifyFailure(title, text, ToolTipIcon.Warning);
        }

        private static void NotifyFailure(string title, string text, ToolTipIcon icon)
        {
            try
            {
                using (TrayManager tray = new TrayManager())
                {
                    tray.SetVisible(true);
                    tray.Notify(title, text, icon);

                    // 给气泡一点展示时间再退出进程
                    DateTime deadline = DateTime.Now.AddSeconds(5);
                    while (DateTime.Now < deadline)
                    {
                        Application.DoEvents();
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("显示托盘提示失败。", ex);
            }
        }

        private static bool HasArgument(string[] args, string name)
        {
            if (args == null)
                return false;

            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 以管理员身份重启自身（保留原参数）。
        /// 用户拒绝 UAC 或启动失败时返回 false。
        /// </summary>
        private static bool RestartAsAdministrator(string[] args)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = BuildArguments(args)
                };

                Process.Start(info);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("提权重启失败。", ex);
                return false;
            }
        }

        private static string BuildArguments(string[] args)
        {
            if (args == null || args.Length == 0)
                return string.Empty;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (string arg in args)
            {
                if (sb.Length > 0)
                    sb.Append(' ');

                // 参数含空格时加引号，避免提权后丢失
                if (arg.IndexOf(' ') >= 0)
                    sb.Append('"').Append(arg).Append('"');
                else
                    sb.Append(arg);
            }

            return sb.ToString();
        }

        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);

                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void ShowError(string title, string message)
        {
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
