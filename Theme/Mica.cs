using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ECController.Theme
{
    /// <summary>
    /// DWM 窗口材质接入（标题栏 Mica / 圆角 / 深色标题栏）。
    ///
    /// 这里曾经还有"客户区 Mica 模拟"——读桌面壁纸、强模糊、叠浅色光罩合成一张位图
    /// 当作窗体背景。**已移除**，原因是实测数据（PerfProbe）：
    ///   · 合成一次 49.5 ms，而窗口每移动 8px 就会重做一次 → 拖动 87 ms/帧，肉眼可见地卡
    ///   · 位图还要 1:1 贴到客户区，占去单次重绘 7.1 ms
    ///   · 而它经过强模糊 + 0.86 不透明度光罩后，本身就几乎是一块均匀浅色，
    ///     换成静态渐变在观感上几乎没有区别
    ///
    /// 顺带记清楚为什么客户区只能"模拟"而不能拿真材质（当初试过的手段）：
    ///   · DwmExtendFrameIntoClientArea(-1) 后把客户区刷黑 —— 仍是纯黑
    ///   · DwmExtendFrameIntoClientArea(top=90) 玻璃带 —— 完全无效
    ///   · SetWindowCompositionAttribute 的 ACRYLICBLURBEHIND / HOSTBACKDROP —— 无效
    ///   · Form.BackColor 设半透明色 —— 直接抛 ArgumentException
    ///   · TransparencyKey + 材质 —— 客户区确实透出材质，但颜色键是二值透明，
    ///     抗锯齿边缘会留下同色描边，有自绘圆角与文字时不可用
    /// 客户区想透出真材质，只能改用 UpdateLayeredWindow 全自绘（放弃子控件、
    /// 自己做命中测试）或换 WinUI3/WPF 外壳。代价太大，不值得。
    ///
    /// 标题栏则是真材质：由 DWM 合成器绘制，不占本进程 CPU。
    /// </summary>
    public static class Mica
    {
        #region DWM 互操作

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWA_MICA_EFFECT = 1029;

        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;      // Mica

        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmIsCompositionEnabled(out bool enabled);

        #endregion

        #region 能力探测

        private static int _osBuild = -1;

        /// <summary>当前系统内部版本号（如 26200）；取不到返回 0。</summary>
        public static int OsBuild
        {
            get
            {
                if (_osBuild >= 0)
                    return _osBuild;

                _osBuild = 0;

                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                    {
                        if (key != null)
                        {
                            object value = key.GetValue("CurrentBuildNumber");

                            int build;
                            if (value != null &&
                                int.TryParse(value.ToString(), out build))
                            {
                                _osBuild = build;
                            }
                        }
                    }
                }
                catch
                {
                    // 读不到就按不支持处理
                }

                return _osBuild;
            }
        }

        /// <summary>Win11 22H2+ 支持 DWMWA_SYSTEMBACKDROP_TYPE。</summary>
        public static bool SupportsSystemBackdrop
        {
            get { return OsBuild >= 22621; }
        }

        /// <summary>Win11 21H2 只能走未公开的 DWMWA_MICA_EFFECT。</summary>
        public static bool SupportsMicaEffect
        {
            get { return OsBuild >= 22000 && OsBuild < 22621; }
        }

        /// <summary>Win11 才有系统圆角。</summary>
        public static bool SupportsRoundedCorners
        {
            get { return OsBuild >= 22000; }
        }

        /// <summary>Win10 1809+ 支持深色标题栏。</summary>
        public static bool SupportsDarkTitleBar
        {
            get { return OsBuild >= 17763; }
        }

        private static bool CompositionEnabled
        {
            get
            {
                try
                {
                    bool enabled;
                    return DwmIsCompositionEnabled(out enabled) == 0 && enabled;
                }
                catch
                {
                    return false;
                }
            }
        }

        #endregion

        #region 应用到窗口

        /// <summary>
        /// 把 DWM 属性刷到窗口上，返回一句可写进日志/界面的说明。
        ///
        /// <paramref name="enableBackdrop"/> 为 false 时不动背景材质，
        /// 只保留圆角与深色标题栏——这两项由 DWM 合成，同样不占本进程 CPU。
        /// </summary>
        public static string Apply(IntPtr hwnd, bool darkTitleBar, bool enableBackdrop)
        {
            if (hwnd == IntPtr.Zero)
                return "窗口句柄无效，未启用窗口特效";

            bool backdrop = false;

            if (enableBackdrop && CompositionEnabled)
            {
                if (SupportsSystemBackdrop)
                {
                    int type = DWMSBT_MAINWINDOW;
                    backdrop = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int)) == 0;
                }
                else if (SupportsMicaEffect)
                {
                    int on = 1;
                    backdrop = DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref on, sizeof(int)) == 0;
                }
            }
            else if (!enableBackdrop && CompositionEnabled && SupportsSystemBackdrop)
            {
                // 显式关掉，避免上一次设过的材质残留
                int none = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
            }

            if (SupportsDarkTitleBar)
                SetDarkTitleBar(hwnd, darkTitleBar);

            if (SupportsRoundedCorners)
            {
                int round = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            }

            string text;

            if (backdrop)
            {
                text = "标题栏 Mica 已启用（系统内部版本 " + OsBuild + "，由 DWM 合成，不占本进程 CPU）";
            }
            else if (!enableBackdrop)
            {
                text = "标题栏 Mica 已按设置关闭（窗口圆角与深色标题栏仍保留）";
            }
            else if (OsBuild >= 22000)
            {
                text = "标题栏 Mica 未能启用（DWM 拒绝设置，内部版本 " + OsBuild + "）";
            }
            else
            {
                text = "当前系统不支持标题栏 Mica（内部版本 " + OsBuild + "）";
            }

            return text;
        }

        private static void SetDarkTitleBar(IntPtr hwnd, bool dark)
        {
            int value = dark ? 1 : 0;

            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref value, sizeof(int));
        }

        #endregion
    }
}
