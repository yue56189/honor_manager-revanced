using System;
using System.Collections.Generic;
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

            InstallCrashHandlers();

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
        /// 兜底异常记录。
        ///
        /// 起因：v1.0.1/1.0.2 在「驱动服务已存在」这条分支上会读到野指针并抛
        /// AccessViolationException，而 .NET 4 默认把这类异常算作「损坏状态异常」，
        /// 托管代码捕获不到 —— 进程当场消失，日志里一个字都没留，看起来就像
        /// 「双击没反应」。排查时只能翻事件日志里的 .NET Runtime 1026。
        ///
        /// 这里能拦住的只有普通未处理异常（注意：<b>拦不住</b>损坏状态异常）。
        /// 真正的防线是别产生野指针，见 Driver\QueryServiceConfigLayout.cs。
        /// </summary>
        private static void InstallCrashHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(
                object sender, UnhandledExceptionEventArgs e)
            {
                try
                {
                    Logger.Error("未处理异常，进程即将终止。", e.ExceptionObject as Exception);
                }
                catch
                {
                }
            };

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                try
                {
                    Logger.Error("界面线程未处理异常。", e.Exception);
                }
                catch
                {
                }

                ShowError(
                    "EC Controller",
                    "程序遇到未处理的错误。\n\n"
                    + (e.Exception == null ? "未知错误。" : e.Exception.Message)
                    + "\n\n详细信息已写入 Logs\\ 目录下的日志文件。");
            };
        }

        /// <summary>
        /// 登录自动应用：不开窗口，直接写 EC，结果写日志 + 托盘气泡。
        /// 失败也不能弹阻塞式对话框，否则会卡住登录流程。
        ///
        /// 会依次应用配置里勾选的每个功能组（例如 性能模式 + 电池管理），
        /// 一项失败不影响其余项，最后汇总结果。
        /// </summary>
        private static void RunApplyOnly(AppSettings settings)
        {
            if (!settings.ApplyOnBoot)
            {
                Logger.Info("配置未启用登录自动应用，退出。");
                return;
            }

            if (settings.BootSelections.Count == 0)
            {
                Logger.Warn("已启用登录自动应用，但没有勾选任何功能组，退出。");
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

            // 先把每一项都解析出来：缺哪个单独记哪个，不因为一项写错就放弃其余的
            List<string> labels = new List<string>();
            List<EcMode> modes = new List<EcMode>();
            List<string> missing = new List<string>();

            foreach (BootSelection selection in settings.BootSelections)
            {
                EcMode mode = FindMode(parse, selection.Group, selection.Mode);

                if (mode == null)
                {
                    missing.Add(selection.ToString());

                    Logger.Error(string.Format(
                        "找不到配置的模式：功能组 \"{0}\"，模式 \"{1}\"。",
                        selection.Group,
                        selection.Mode));

                    continue;
                }

                labels.Add(selection.ToString());
                modes.Add(mode);
            }

            if (modes.Count == 0)
            {
                string detail = "找不到任何可用模式：" + string.Join("、", missing.ToArray());

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

                List<string> applied = new List<string>();
                List<string> failed = new List<string>();

                for (int i = 0; i < modes.Count; i++)
                {
                    ApplyResult result = service.Apply(modes[i]);

                    if (result.Success)
                    {
                        applied.Add(labels[i]);
                        Logger.Info("自动应用成功：" + labels[i] + " —— " + result.Message);
                    }
                    else
                    {
                        failed.Add(labels[i] + "（" + result.Message + "）");
                        Logger.Error("自动应用未完全成功：" + labels[i] + " —— " + result.Message);
                    }
                }

                if (failed.Count == 0 && missing.Count == 0)
                {
                    string text = "已自动应用：" + string.Join("、", applied.ToArray());

                    Logger.Info(text);
                    NotifyFailure("EC Controller", text, ToolTipIcon.Info);
                    return;
                }

                string summary = string.Empty;

                if (applied.Count > 0)
                    summary += "已应用：" + string.Join("、", applied.ToArray()) + "\n";

                if (failed.Count > 0)
                    summary += "未成功：" + string.Join("、", failed.ToArray()) + "\n";

                if (missing.Count > 0)
                    summary += "配置里找不到：" + string.Join("、", missing.ToArray());

                Logger.Error("自动应用未完全成功：" + summary.Replace("\n", " | "));
                NotifyFailure("自动应用未完全成功", summary.TrimEnd());
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
