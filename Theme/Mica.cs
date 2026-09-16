using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ECController.Theme
{
    /// <summary>
    /// DWM 背景材质（Mica）接入 + 客户区"Mica 质感"底纹。
    ///
    /// 为什么要分成两件事（这是实测出来的，不是设计选择）：
    ///   在纯 WinForms（GDI 自绘 + 子控件）里，DWM 的 Mica 材质**只有非客户区
    ///   （标题栏）拿得到**。试过的办法都没用：
    ///     · DwmExtendFrameIntoClientArea(-1) 后把客户区刷黑 —— 仍是纯黑
    ///     · DwmExtendFrameIntoClientArea(top=90) 玻璃带 —— 完全无效
    ///     · SetWindowCompositionAttribute 的 ACRYLICBLURBEHIND / HOSTBACKDROP
    ///     · Form.BackColor 设半透明色 —— 直接抛 ArgumentException
    ///     · TransparencyKey + 材质 —— 客户区确实透出材质，但颜色键是二值透明，
    ///       抗锯齿边缘会留下同色描边，有自绘圆角与文字时不可用
    ///   客户区想透出真材质，只能改用 UpdateLayeredWindow 全自绘（放弃子控件、
    ///   自己做命中测试）或换 WinUI3/WPF 外壳。
    ///
    /// 所以这里的做法是：
    ///   标题栏 = 真 Mica（<see cref="ApplyBackdrop"/>）；
    ///   客户区 = Mica 风格模拟（<see cref="ComposeBackdrop"/>：
    ///   取窗口下方那片桌面壁纸 → 强模糊 → 铺一层浅色光罩），读不到壁纸就回退纯色。
    /// </summary>
    public static class Mica
    {
        #region DWM 互操作

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMWA_MICA_EFFECT = 1029;

        private const int DWMSBT_AUTO = 0;
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

        /// <summary>把 DWM 属性刷到窗口上，返回一句可写进日志/界面的说明。</summary>
        public static string Apply(IntPtr hwnd, bool darkTitleBar)
        {
            if (hwnd == IntPtr.Zero)
                return "窗口句柄无效，未启用窗口特效";

            bool backdrop = false;

            if (CompositionEnabled)
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
                text = "标题栏 Mica 已启用（系统内部版本 " + OsBuild + "）";
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

        #region 壁纸 -> 客户区底纹

        /// <summary>光罩不透明度：越大越接近纯色（可读性越好），越小壁纸越明显。</summary>
        public const double ScrimOpacity = 0.86;

        /// <summary>模糊用的中间尺寸（先缩到这么小再放大，等效强模糊）。</summary>
        private const int BlurSourceSize = 20;

        /// <summary>
        /// 读注册表里的桌面壁纸路径。取不到返回 null。
        /// 注意：Windows 把壁纸转码缓存在 TranscodedWallpaper，原图可能已被删除。
        /// </summary>
        public static string LoadWallpaperPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("WallPaper");

                        if (value != null)
                        {
                            string path = value.ToString();

                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                return path;
                        }
                    }
                }
            }
            catch
            {
                // 落到下面的转码缓存
            }

            try
            {
                string transcoded = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Themes\TranscodedWallpaper");

                if (File.Exists(transcoded))
                    return transcoded;
            }
            catch
            {
                // 忽略
            }

            return null;
        }

        /// <summary>壁纸在显示器上的排布方式（HKCU\Control Panel\Desktop）。</summary>
        private static void ReadWallpaperStyle(out int style, out bool tile)
        {
            style = 10;   // 默认按 Fill 处理，这是最常见的设置
            tile = false;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
                {
                    if (key == null)
                        return;

                    object s = key.GetValue("WallpaperStyle");
                    object t = key.GetValue("TileWallpaper");

                    int parsed;
                    if (s != null && int.TryParse(s.ToString(), out parsed))
                        style = parsed;

                    tile = t != null && t.ToString() == "1";
                }
            }
            catch
            {
                // 用默认值
            }
        }

        /// <summary>
        /// 把"窗口所在那片屏幕区域"映射到壁纸图片上的矩形。
        /// 这是纯几何换算，抽出来是为了能单独测（见 MicaProbe）。
        /// </summary>
        public static RectangleF MapScreenToWallpaper(
            Size wallpaper, Rectangle monitor, Rectangle window, int style, bool tile)
        {
            float sx = 1f;
            float sy = 1f;

            switch (style)
            {
                case 2:   // 拉伸
                    sx = monitor.Width / (float)wallpaper.Width;
                    sy = monitor.Height / (float)wallpaper.Height;
                    break;

                case 6:   // 适应
                case 10:  // 填充
                {
                    float a = monitor.Width / (float)wallpaper.Width;
                    float b = monitor.Height / (float)wallpaper.Height;
                    float s = style == 6 ? Math.Min(a, b) : Math.Max(a, b);
                    sx = s;
                    sy = s;
                    break;
                }

                default:  // 居中 / 平铺 / 跨屏（近似）
                    sx = 1f;
                    sy = 1f;
                    break;
            }

            float drawnW = wallpaper.Width * sx;
            float drawnH = wallpaper.Height * sy;

            float offsetX = monitor.X + (monitor.Width - drawnW) / 2f;
            float offsetY = monitor.Y + (monitor.Height - drawnH) / 2f;

            float x = (window.X - offsetX) / sx;
            float y = (window.Y - offsetY) / sy;
            float w = window.Width / sx;
            float h = window.Height / sy;

            if (tile && drawnW > 0 && drawnH > 0)
            {
                x = x % wallpaper.Width;
                y = y % wallpaper.Height;

                if (x < 0) x += wallpaper.Width;
                if (y < 0) y += wallpaper.Height;
            }

            return new RectangleF(x, y, w, h);
        }

        /// <summary>
        /// 合成客户区底纹：壁纸对应区域 -> 强模糊 -> 浅色光罩。
        /// 任何一步失败都返回 null，由调用方回退纯色底。
        /// </summary>
        public static Bitmap ComposeBackdrop(
            string wallpaperPath,
            Rectangle monitor,
            Rectangle window,
            Size output,
            double scrimOpacity)
        {
            if (string.IsNullOrEmpty(wallpaperPath) ||
                !File.Exists(wallpaperPath) ||
                output.Width <= 0 || output.Height <= 0 ||
                monitor.Width <= 0 || monitor.Height <= 0)
            {
                return null;
            }

            try
            {
                int style;
                bool tile;
                ReadWallpaperStyle(out style, out tile);

                // 用流加载，避免锁住用户的壁纸文件
                using (FileStream fs = new FileStream(
                    wallpaperPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image source = Image.FromStream(fs, false, false))
                {
                    if (source.Width <= 0 || source.Height <= 0)
                        return null;

                    RectangleF patch = MapScreenToWallpaper(
                        new Size(source.Width, source.Height), monitor, window, style, tile);

                    if (patch.Width <= 0 || patch.Height <= 0)
                        return null;

                    // 窗口可能有一部分落在壁纸绘制区之外（居中/平铺时很常见），
                    // 先取交集，再把目标矩形按同样的比例缩回来，避免把图外的部分
                    // 也拉进来导致整块错位。
                    RectangleF src = RectangleF.Intersect(
                        patch, new RectangleF(0, 0, source.Width, source.Height));

                    if (src.Width <= 1 || src.Height <= 1)
                        return null;

                    int smallHeight = Math.Max(1, (int)Math.Round(
                        BlurSourceSize * output.Height / (double)output.Width));

                    using (Bitmap small = new Bitmap(
                        BlurSourceSize, smallHeight, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.White);

                            float kx = small.Width / patch.Width;
                            float ky = small.Height / patch.Height;

                            RectangleF dest = new RectangleF(
                                (src.X - patch.X) * kx,
                                (src.Y - patch.Y) * ky,
                                src.Width * kx,
                                src.Height * ky);

                            g.DrawImage(source, dest, src, GraphicsUnit.Pixel);
                        }

                        Bitmap result = new Bitmap(output.Width, output.Height, PixelFormat.Format32bppArgb);

                        using (Graphics g = Graphics.FromImage(result))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.SmoothingMode = SmoothingMode.HighQuality;

                            // 放大回目标尺寸 = 强模糊（已经缩到 20px 宽，细节没了）
                            g.DrawImage(small, new Rectangle(0, 0, output.Width, output.Height));

                            // 浅色光罩：保证控件文字仍然清晰
                            int alpha = (int)Math.Round(
                                Math.Max(0, Math.Min(1, scrimOpacity)) * 255);

                            using (SolidBrush scrim = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                                g.FillRectangle(scrim, 0, 0, output.Width, output.Height);
                        }

                        return result;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 按控件的实际位置合成底纹（取它所在显示器的壁纸）。
        /// 失败返回 null。
        /// </summary>
        public static Bitmap ComposeForControl(Control control, Size output)
        {
            if (control == null)
                return null;

            try
            {
                string path = LoadWallpaperPath();

                if (path == null)
                    return null;

                Screen screen = Screen.FromControl(control);

                return ComposeBackdrop(
                    path,
                    screen.Bounds,
                    control.RectangleToScreen(control.ClientRectangle),
                    output,
                    ScrimOpacity);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
