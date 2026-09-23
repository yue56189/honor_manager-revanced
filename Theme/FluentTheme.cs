using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ECController.Theme
{
    /// <summary>
    /// Fluent Design 配色与度量。纯自绘，无第三方依赖。
    /// 采用浅色主题（Fluent Light）：WinForms 的 ComboBox/TextBox 原生控件无法
    /// 可靠地深色重绘，整体走浅色可保证全局一致，不会出现半深半浅的割裂感。
    /// </summary>
    public static class FluentTheme
    {
        // ---- 背景层次 ----
        /// <summary>
        /// 窗体底色（渐变的下端色）。
        /// </summary>
        public static readonly Color WindowBackground = Color.FromArgb(233, 238, 244);

        /// <summary>窗体渐变的顶端色。与 <see cref="WindowBackground"/> 共同构成极淡的垂直渐变。</summary>
        public static readonly Color WindowBackgroundTop = Color.FromArgb(250, 251, 253);

        /// <summary>卡片/内容区底色</summary>
        public static readonly Color CardBackground = Color.FromArgb(255, 255, 255);

        /// <summary>卡片描边（Fluent 的 stroke 极轻）</summary>
        public static readonly Color CardBorder = Color.FromArgb(229, 229, 229);

        /// <summary>悬停态背景</summary>
        public static readonly Color SubtleHover = Color.FromArgb(249, 249, 249);

        /// <summary>按下态背景</summary>
        public static readonly Color SubtlePressed = Color.FromArgb(239, 239, 239);

        /// <summary>输入控件底色</summary>
        public static readonly Color InputBackground = Color.FromArgb(255, 255, 255);

        /// <summary>输入控件描边</summary>
        public static readonly Color InputBorder = Color.FromArgb(209, 209, 209);

        /// <summary>输入控件悬停描边</summary>
        public static readonly Color InputBorderHover = Color.FromArgb(150, 150, 150);

        // ---- 文字 ----
        /// <summary>主文字（Fluent TextFillColorPrimary）</summary>
        public static readonly Color TextPrimary = Color.FromArgb(26, 26, 26);

        /// <summary>次要文字</summary>
        public static readonly Color TextSecondary = Color.FromArgb(96, 96, 96);

        /// <summary>禁用文字</summary>
        public static readonly Color TextDisabled = Color.FromArgb(161, 161, 161);

        /// <summary>反色文字（强调色块上）</summary>
        public static readonly Color TextOnAccent = Color.FromArgb(255, 255, 255);

        // ---- 强调色 ----
        /// <summary>Fluent 默认强调色（Windows 蓝）</summary>
        public static readonly Color Accent = Color.FromArgb(0, 95, 184);

        /// <summary>强调色悬停</summary>
        public static readonly Color AccentHover = Color.FromArgb(25, 116, 205);

        /// <summary>强调色按下</summary>
        public static readonly Color AccentPressed = Color.FromArgb(0, 78, 152);

        /// <summary>强调色禁用</summary>
        public static readonly Color AccentDisabled = Color.FromArgb(199, 224, 244);

        /// <summary>次要按钮底色</summary>
        public static readonly Color ButtonBackground = Color.FromArgb(253, 253, 253);

        /// <summary>次要按钮描边</summary>
        public static readonly Color ButtonBorder = Color.FromArgb(214, 214, 214);

        // ---- 语义色（沿用项目既有约定：一致=绿，不一致=红）----
        /// <summary>成功/一致</summary>
        public static readonly Color Success = Color.FromArgb(15, 123, 15);

        /// <summary>错误/不一致</summary>
        public static readonly Color Error = Color.FromArgb(196, 43, 28);

        /// <summary>警告</summary>
        public static readonly Color Warning = Color.FromArgb(157, 93, 0);

        /// <summary>错误底色（浅）</summary>
        public static readonly Color ErrorBackground = Color.FromArgb(253, 231, 233);

        /// <summary>成功底色（浅）</summary>
        public static readonly Color SuccessBackground = Color.FromArgb(223, 246, 221);

        // ---- 表格 ----
        /// <summary>表头底色</summary>
        public static readonly Color GridHeaderBackground = Color.FromArgb(250, 250, 250);

        /// <summary>表格网格线</summary>
        public static readonly Color GridLine = Color.FromArgb(237, 237, 237);

        /// <summary>表格选中行底色</summary>
        public static readonly Color GridSelection = Color.FromArgb(238, 246, 253);

        // ---- 度量 ----
        /// <summary>卡片圆角半径</summary>
        public const int CornerRadius = 8;

        /// <summary>输入控件圆角半径</summary>
        public const int InputCornerRadius = 4;

        /// <summary>卡片内边距</summary>
        public const int CardPadding = 16;

        /// <summary>窗体内容外边距</summary>
        public const int WindowPadding = 20;

        /// <summary>控件之间标准间距</summary>
        public const int Gap = 12;

        // ---- 客户区背景 ----
        //
        // 这里曾经用"读桌面壁纸 → 强模糊 → 叠浅色光罩"合成一张位图来模拟 Mica。
        // 实测（PerfProbe，Windows 11 26200 / DPI 125%）：
        //   · 合成一次要 49.5 ms，而拖动窗口时每次位移都会重做 → 拖动 87 ms/帧
        //   · 那张位图还要被 1:1 DrawImage 贴到客户区，GDI+ 不做硬件加速，占重绘 7.1 ms
        // 换成一个纯色垂直渐变后，这两项开销归零，观感差异很小——
        // 因为原来的位图经过强模糊 + 0.86 不透明度光罩之后，本身就几乎是一块均匀浅色。
        //
        // 结论：客户区不值得为"Mica 质感"付出这个代价。真材质只有标题栏拿得到
        // （见 Mica 的注释），客户区本来就是仿的。

        /// <summary>
        /// 用窗体背景（垂直渐变）填充指定矩形。
        ///
        /// <paramref name="full"/> 是"渐变从哪到哪"，<paramref name="target"/> 是"要填哪块"。
        /// 两者都在**窗体客户区坐标系**里。窗体自己整块重绘时两者相同；
        /// 子控件要露出身后的窗体底色时，target 就是控件自己的矩形。
        ///
        /// 注意：渐变端点必须始终按整窗算，否则局部重绘时颜色会跳。
        /// </summary>
        public static void PaintWindowBackground(Graphics g, Rectangle full, Rectangle target)
        {
            if (g == null || full.Width <= 0 || full.Height <= 0 || target.Width <= 0 || target.Height <= 0)
                return;

            using (LinearGradientBrush brush = new LinearGradientBrush(
                full, WindowBackgroundTop, WindowBackground, LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, target);
            }
        }

        /// <summary>窗体背景在指定纵向位置的颜色（供无法透明的原生控件取用）。</summary>
        public static Color WindowBackgroundAt(int y, int height)
        {
            if (height <= 0)
                return WindowBackground;

            return Blend(WindowBackgroundTop, WindowBackground,
                Math.Max(0.0, Math.Min(1.0, y / (double)height)));
        }

        /// <summary>两色线性插值，t 为 0 取 a，为 1 取 b。</summary>
        public static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        // ---- 字体 ----
        private const string PreferFont = "微软雅黑";
        private const string FallbackFont = "Segoe UI";

        private static string _fontFamilyName;
        private static bool _fontMissing;

        /// <summary>
        /// 根据字号取得正文字体（微软雅黑优先，回退 Segoe UI）。
        /// </summary>
        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FontFamilyName, size, style);
        }

        /// <summary>可用字体族名（微软雅黑优先，回退 Segoe UI）。</summary>
        public static string FontFamilyName
        {
            get
            {
                if (_fontMissing)
                    return _fontFamilyName ?? FontFamily.GenericSansSerif.Name;

                if (_fontFamilyName != null)
                    return _fontFamilyName;

                try
                {
                    foreach (FontFamily f in FontFamily.Families)
                    {
                        if (f.Name == PreferFont) { _fontFamilyName = PreferFont; return _fontFamilyName; }
                    }

                    foreach (FontFamily f in FontFamily.Families)
                    {
                        if (f.Name == FallbackFont) { _fontFamilyName = FallbackFont; return _fontFamilyName; }
                    }
                }
                catch
                {
                    _fontMissing = true;
                }

                _fontFamilyName = FontFamily.GenericSansSerif.Name;

                return _fontFamilyName;
            }
        }

        /// <summary>是否用上了首选的微软雅黑（供界面/日志说明）。</summary>
        public static bool UsingPreferredFont
        {
            get { return FontFamilyName == PreferFont; }
        }

        // 字体在这里缓存成实例。
        //
        // 这几项原来是只会 new 的只读属性，被控件的 OnPaint 直接调用——
        // 也就是**每次重绘都新建 Font**。Font 内部持有 GDI+ 的原生对象，
        // 靠终结器回收，所以不会泄漏句柄（实测 300 次重绘 GDI 对象增量 0），
        // 但会持续制造垃圾并带来 GC 停顿。字体在一个进程里是恒定不变的，
        // 缓存成常量实例即可，也让控件之间能安全共享。
        //
        // 共享是安全的：Control.Font 的 setter 会 Clone 传入的字体，
        // 控件 Dispose 不会去释放调用方持有的实例。
        private static readonly Font CachedTitle;
        private static readonly Font CachedSubtitle;
        private static readonly Font CachedBody;
        private static readonly Font CachedCaption;

        /// <summary>
        /// 在静态构造里建缓存字体。这里刻意写 <c>new Font(字体族名, 字号, 样式)</c>
        /// 而不是调用本类的 <see cref="Font(float, FontStyle)"/>——两者同名，
        /// 用构造器形式可以避免"Font 到底是类还是方法"的解析歧义。
        /// </summary>
        static FluentTheme()
        {
            string family = FontFamilyName;

            CachedTitle = new Font(family, 13.5f, FontStyle.Bold);
            CachedSubtitle = new Font(family, 10f, FontStyle.Bold);
            CachedBody = new Font(family, 9.5f);
            CachedCaption = new Font(family, 8.5f);
        }

        /// <summary>标题字体（16pt 半粗）</summary>
        public static Font TitleFont { get { return CachedTitle; } }

        /// <summary>小标题字体</summary>
        public static Font SubtitleFont { get { return CachedSubtitle; } }

        /// <summary>正文字体</summary>
        public static Font BodyFont { get { return CachedBody; } }

        /// <summary>说明文字（略小、次要色）</summary>
        public static Font CaptionFont { get { return CachedCaption; } }
    }
}
