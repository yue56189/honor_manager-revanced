using System.Drawing;

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
        /// <summary>窗体底色（Fluent 的 mica 近似色）</summary>
        public static readonly Color WindowBackground = Color.FromArgb(243, 243, 243);

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

        // ---- 字体 ----
        private const string PreferFont = "微软雅黑";
        private const string FallbackFont = "Segoe UI";

        /// <summary>根据字号取得正文字体（自动挑选可用字体）</summary>
        public static Font Font(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font(FontFamilyName, size, style);
        }

        /// <summary>可用字体族名（微软雅黑优先，回退 Segoe UI）</summary>
        public static string FontFamilyName
        {
            get
            {
                foreach (FontFamily f in FontFamily.Families)
                {
                    if (f.Name == PreferFont) return PreferFont;
                }
                foreach (FontFamily f in FontFamily.Families)
                {
                    if (f.Name == FallbackFont) return FallbackFont;
                }
                return FontFamily.GenericSansSerif.Name;
            }
        }

        /// <summary>标题字体（16pt 半粗）</summary>
        public static Font TitleFont { get { return Font(13.5f, FontStyle.Bold); } }

        /// <summary>小标题字体</summary>
        public static Font SubtitleFont { get { return Font(10f, FontStyle.Bold); } }

        /// <summary>正文字体</summary>
        public static Font BodyFont { get { return Font(9.5f); } }

        /// <summary>说明文字（略小、次要色）</summary>
        public static Font CaptionFont { get { return Font(8.5f); } }
    }
}
