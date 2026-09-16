using System;

namespace ECController.Config
{
    /// <summary>
    /// 用户设置。只描述"用户想怎么运行程序"，
    /// 与 EC 配置（Data\ec ADDRESS.txt 里定义了哪些模式）严格分开。
    /// </summary>
    public class AppSettings
    {
        /// <summary>是否开机自启（写入 HKCU Run 注册表项）。</summary>
        public bool AutoStart { get; set; }

        /// <summary>登录后是否自动应用某个模式（以 --apply-only 静默方式）。</summary>
        public bool ApplyOnBoot { get; set; }

        /// <summary>关闭/最小化窗口时是否隐藏到托盘而不是退出。</summary>
        public bool Tray { get; set; }

        /// <summary>登录自动应用时使用的功能组名。</summary>
        public string BootGroup { get; set; }

        /// <summary>登录自动应用时使用的模式名。</summary>
        public string BootMode { get; set; }

        /// <summary>每次 EC 端口操作后的等待毫秒数。</summary>
        public int WaitTime { get; set; }

        /// <summary>开机自动应用前额外等待的秒数，等系统 EC 服务就绪。</summary>
        public int BootDelaySeconds { get; set; }

        public AppSettings()
        {
            AutoStart = false;
            ApplyOnBoot = false;
            Tray = false;
            BootGroup = string.Empty;
            BootMode = string.Empty;
            WaitTime = 5;
            BootDelaySeconds = 5;
        }

        /// <summary>
        /// 收敛非法值。配置文件被手工改坏时也能安全使用。
        /// </summary>
        public void Normalize()
        {
            if (WaitTime < 0 || WaitTime > 1000)
                WaitTime = 5;

            if (BootDelaySeconds < 0 || BootDelaySeconds > 300)
                BootDelaySeconds = 5;

            if (BootGroup == null)
                BootGroup = string.Empty;

            if (BootMode == null)
                BootMode = string.Empty;
        }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }
    }
}
