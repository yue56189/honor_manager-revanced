using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace ECController.Services
{
    /// <summary>
    /// 维护 Windows 开机启动项（HKCU\...\Run）。
    ///
    /// 刻意与 EC 写入逻辑完全解耦：这里只负责注册表的增删查，
    /// 不碰驱动、不碰 EC，方便单独排查启动问题。
    /// </summary>
    public static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// 启动项名。用带项目标识的名字，避免与别的程序冲突。
        /// </summary>
        private const string ValueName = "ECController";

        /// <summary>当前 exe 的完整路径。</summary>
        public static string ExecutablePath
        {
            get
            {
                string location = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(location)
                    ? Process.GetCurrentProcess().MainModule.FileName
                    : location;
            }
        }

        /// <summary>
        /// 登录自动应用时使用的命令行：静默模式，不显示主界面。
        /// </summary>
        private const string SilentArgument = "--apply-only";

        /// <summary>启用开机启动。</summary>
        public static bool Enable()
        {
            return WriteStartupValue("\"" + ExecutablePath + "\" " + SilentArgument);
        }

        /// <summary>关闭开机启动。已经不存在时也算成功。</summary>
        public static bool Disable()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null)
                        return true;

                    if (key.GetValue(ValueName) != null)
                    {
                        key.DeleteValue(ValueName, false);
                        Logger.Info("已移除开机启动项。");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("移除开机启动项失败。", ex);
                return false;
            }
        }

        /// <summary>把设置里期望的状态同步到注册表。返回是否成功。</summary>
        public static bool Apply(bool enable)
        {
            return enable ? Enable() : Disable();
        }

        /// <summary>查询注册表中当前是否已存在本程序的开机启动项。</summary>
        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                        return false;

                    object value = key.GetValue(ValueName);

                    return value != null &&
                        !string.IsNullOrEmpty(value.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.Error("查询开机启动项失败。", ex);
                return false;
            }
        }

        private static bool WriteStartupValue(string command)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        Logger.Error("无法创建/打开 Run 注册表项。");
                        return false;
                    }

                    key.SetValue(ValueName, command, RegistryValueKind.String);
                }

                Logger.Info("已设置开机启动：" + command);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("设置开机启动项失败。", ex);
                return false;
            }
        }
    }
}
