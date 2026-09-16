using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ECController.Theme
{
    /// <summary>圆角与绘制工具的公共助手。</summary>
    internal static class Draw
    {
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>抗锯齿绘制开关（绘制完务必恢复）。</summary>
        public static void WithSmoothing(Graphics g, Action<Graphics> body)
        {
            SmoothingMode old = g.SmoothingMode;
            TextRenderingHint oldText = g.TextRenderingHint;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            try { body(g); }
            finally
            {
                g.SmoothingMode = old;
                g.TextRenderingHint = oldText;
            }
        }

        /// <summary>把矩形内缩指定像素。</summary>
        public static Rectangle Inset(Rectangle r, int px)
        {
            return new Rectangle(r.X + px, r.Y + px, Math.Max(0, r.Width - px * 2), Math.Max(0, r.Height - px * 2));
        }

        /// <summary>垂直居中绘制单行文字。</summary>
        public static void DrawTextCentered(Graphics g, string text, Font font, Color color, Rectangle bounds,
            TextFormatFlags extra = TextFormatFlags.HorizontalCenter)
        {
            TextRenderer.DrawText(g, text, font, bounds, color,
                extra | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>
    /// Fluent 卡片容器：圆角、白底、极轻描边，可选标题。
    /// 不派生于 GroupBox，因此完全掌控外观。
    /// </summary>
    public class FluentCard : Panel
    {
        private string _title = string.Empty;

        public FluentCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = FluentTheme.WindowBackground;
            Padding = new Padding(FluentTheme.CardPadding);
        }

        /// <summary>卡片标题（为空则不占标题高度）。</summary>
        public string Title
        {
            get { return _title; }
            set { _title = value ?? string.Empty; UpdatePadding(); Invalidate(); }
        }

        /// <summary>标题区域占用高度。</summary>
        public int TitleHeight { get { return string.IsNullOrEmpty(_title) ? 0 : 30; } }

        private void UpdatePadding()
        {
            int top = FluentTheme.CardPadding + TitleHeight;
            Padding = new Padding(FluentTheme.CardPadding, top, FluentTheme.CardPadding, FluentTheme.CardPadding);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw.WithSmoothing(e.Graphics, g =>
            {
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Draw.RoundedRect(r, FluentTheme.CornerRadius))
                {
                    using (SolidBrush b = new SolidBrush(FluentTheme.CardBackground))
                        g.FillPath(b, path);
                    using (Pen p = new Pen(FluentTheme.CardBorder, 1f))
                        g.DrawPath(p, path);
                }

                if (!string.IsNullOrEmpty(_title))
                {
                    Rectangle tr = new Rectangle(FluentTheme.CardPadding, FluentTheme.CardPadding - 4,
                        Width - FluentTheme.CardPadding * 2, 24);
                    Draw.DrawTextCentered(g, _title, FluentTheme.SubtitleFont, FluentTheme.TextPrimary, tr,
                        TextFormatFlags.Left);
                }
            });
        }
    }

    /// <summary>Fluent 按钮。Accent=true 时为主操作按钮（强调色实心）。</summary>
    public class FluentButton : Button
    {
        private bool _hover;
        private bool _pressed;
        private bool _accent;

        public FluentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Font = FluentTheme.BodyFont;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        /// <summary>是否为主操作按钮（强调色）。</summary>
        public bool Accent
        {
            get { return _accent; }
            set { _accent = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        /// <summary>Fluent 不用虚线焦点框，键盘可用性不受影响。</summary>
        protected override bool ShowFocusCues { get { return false; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool on = Enabled;

            Color back, border, fore;
            if (_accent)
            {
                back = !on ? FluentTheme.AccentDisabled
                     : _pressed ? FluentTheme.AccentPressed
                     : _hover ? FluentTheme.AccentHover
                     : FluentTheme.Accent;
                border = Color.Empty;
                fore = on ? FluentTheme.TextOnAccent : Color.FromArgb(160, 160, 160);
            }
            else
            {
                back = !on ? FluentTheme.SubtlePressed
                     : _pressed ? FluentTheme.SubtlePressed
                     : _hover ? FluentTheme.SubtleHover
                     : FluentTheme.ButtonBackground;
                border = !on ? FluentTheme.CardBorder
                       : _hover ? FluentTheme.InputBorderHover
                       : FluentTheme.ButtonBorder;
                fore = on ? FluentTheme.TextPrimary : FluentTheme.TextDisabled;
            }

            Draw.WithSmoothing(e.Graphics, g =>
            {
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Draw.RoundedRect(r, FluentTheme.InputCornerRadius))
                {
                    using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, path);
                    if (border != Color.Empty)
                        using (Pen p = new Pen(border, 1f)) g.DrawPath(p, path);
                }
                Draw.DrawTextCentered(e.Graphics, Text, Font, fore, r);
            });
        }
    }

    /// <summary>Fluent 复选框：自绘方框 + 勾，无原生立体外观。</summary>
    public class FluentCheckBox : CheckBox
    {
        private bool _hover;
        private const int BoxSize = 18;

        public FluentCheckBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Font = FluentTheme.BodyFont;
            AutoSize = false;
            Height = 24;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        /// <summary>Fluent 不用虚线焦点框。</summary>
        protected override bool ShowFocusCues { get { return false; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool on = Enabled;
            Color fore = on ? FluentTheme.TextPrimary : FluentTheme.TextDisabled;

            Draw.WithSmoothing(e.Graphics, g =>
            {
                int top = (Height - BoxSize) / 2;
                Rectangle box = new Rectangle(1, top, BoxSize - 1, BoxSize - 1);

                Color fill, border;
                if (Checked)
                {
                    fill = on ? FluentTheme.Accent : FluentTheme.AccentDisabled;
                    border = Color.Empty;
                }
                else
                {
                    fill = FluentTheme.InputBackground;
                    border = on ? (_hover ? FluentTheme.InputBorderHover : FluentTheme.InputBorder)
                               : FluentTheme.CardBorder;
                }

                using (GraphicsPath path = Draw.RoundedRect(box, 4))
                {
                    using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                    if (border != Color.Empty)
                        using (Pen p = new Pen(border, 1f)) g.DrawPath(p, path);
                }

                if (Checked)
                {
                    // 勾：两段折线，白色 1.6px 圆头
                    using (Pen p = new Pen(Color.White, 1.8f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        p.LineJoin = LineJoin.Round;
                        float cx = box.X + box.Width / 2f;
                        float cy = box.Y + box.Height / 2f;
                        g.DrawLines(p, new[]
                        {
                            new PointF(cx - 3.6f, cy + 0.2f),
                            new PointF(cx - 1.1f, cy + 2.8f),
                            new PointF(cx + 3.8f, cy - 2.9f)
                        });
                    }
                }

                Rectangle textRect = new Rectangle(box.Right + 8, 0, Math.Max(0, Width - box.Right - 8), Height);
                Draw.DrawTextCentered(g, Text, Font, fore, textRect, TextFormatFlags.Left);
            });
        }
    }

    /// <summary>
    /// Fluent 下拉框：在原生 ComboBox 上做 Flat 化 + 自绘右侧箭头与边框，
    /// 保住原生下拉列表行为（避免自绘列表带来的输入法/滚动问题）。
    /// </summary>
    public class FluentComboBox : ComboBox
    {
        private bool _hover;

        public FluentComboBox()
        {
            FlatStyle = FlatStyle.Flat;
            BackColor = FluentTheme.InputBackground;
            ForeColor = FluentTheme.TextPrimary;
            Font = FluentTheme.BodyFont;
            DropDownStyle = ComboBoxStyle.DropDownList;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // 0x000F = WM_PAINT，绘制完成后补上箭头与边框
            if (m.Msg == 0x000F)
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                    DrawChrome(g);
            }
        }

        private void DrawChrome(Graphics g)
        {
            Draw.WithSmoothing(g, gg =>
            {
                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                using (Pen p = new Pen(_hover ? FluentTheme.InputBorderHover : FluentTheme.InputBorder, 1f))
                    gg.DrawRectangle(p, r);

                // 右侧下拉箭头（V 形折线）
                float cx = Width - 16f;
                float cy = Height / 2f;
                using (Pen p = new Pen(FluentTheme.TextSecondary, 1.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    p.LineJoin = LineJoin.Round;
                    gg.DrawLines(p, new[]
                    {
                        new PointF(cx - 4f, cy - 2f),
                        new PointF(cx, cy + 2f),
                        new PointF(cx + 4f, cy - 2f)
                    });
                }
            });
        }
    }

    /// <summary>Fluent 数字输入框：统一字体与配色。</summary>
    public class FluentNumericUpDown : NumericUpDown
    {
        public FluentNumericUpDown()
        {
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = FluentTheme.InputBackground;
            ForeColor = FluentTheme.TextPrimary;
            Font = FluentTheme.BodyFont;
        }
    }

    /// <summary>Fluent 分组说明文字（小标题，用于选项区标题）。</summary>
    public class FluentSectionLabel : Label
    {
        public FluentSectionLabel()
        {
            AutoSize = false;
            Font = FluentTheme.CaptionFont;
            ForeColor = FluentTheme.TextSecondary;
            BackColor = Color.Transparent;
        }
    }

    /// <summary>Fluent 单选按钮：自绘圆环 + 实心圆点。</summary>
    public class FluentRadioButton : RadioButton
    {
        private bool _hover;
        private const int DotSize = 18;

        public FluentRadioButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Font = FluentTheme.BodyFont;
            AutoSize = false;
            Height = 24;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        /// <summary>Fluent 不用虚线焦点框。</summary>
        protected override bool ShowFocusCues { get { return false; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool on = Enabled;
            Color fore = on ? FluentTheme.TextPrimary : FluentTheme.TextDisabled;

            Draw.WithSmoothing(e.Graphics, g =>
            {
                int top = (Height - DotSize) / 2;
                Rectangle box = new Rectangle(1, top, DotSize - 1, DotSize - 1);

                Color ring = Checked
                    ? (on ? FluentTheme.Accent : FluentTheme.AccentDisabled)
                    : (on ? (_hover ? FluentTheme.InputBorderHover : FluentTheme.InputBorder)
                          : FluentTheme.CardBorder);

                using (Pen p = new Pen(ring, Checked ? 1.6f : 1f))
                    g.DrawEllipse(p, box);

                if (Checked)
                {
                    int inset = 5;
                    Rectangle inner = new Rectangle(box.X + inset, box.Y + inset,
                        box.Width - inset * 2, box.Height - inset * 2);
                    using (SolidBrush b = new SolidBrush(on ? FluentTheme.Accent : FluentTheme.AccentDisabled))
                        g.FillEllipse(b, inner);
                }

                Rectangle textRect = new Rectangle(box.Right + 8, 0, Math.Max(0, Width - box.Right - 8), Height);
                Draw.DrawTextCentered(g, Text, Font, fore, textRect, TextFormatFlags.Left);
            });
        }
    }

    /// <summary>
    /// Fluent 化只读文本框：无边框、浅底，建议放进 FluentCard 里获得圆角描边。
    /// 保留原生 TextBox 以保证可选中、可滚动、可复制。
    /// </summary>
    public class FluentTextBox : TextBox
    {
        public FluentTextBox()
        {
            BorderStyle = BorderStyle.None;
            BackColor = FluentTheme.CardBackground;
            ForeColor = FluentTheme.TextPrimary;
            Font = FluentTheme.BodyFont;
            Multiline = true;
            ReadOnly = true;
            WordWrap = true;
            ScrollBars = ScrollBars.Vertical;
        }

        /// <summary>
        /// 只读框获得焦点时 Windows 会默认全选，视觉上一片蓝色高亮，很不 Fluent。
        /// 这里清掉初始选区；鼠标拖选、Ctrl+A、复制仍照常可用。
        /// </summary>
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            SelectionStart = 0;
            SelectionLength = 0;
        }
    }
}
