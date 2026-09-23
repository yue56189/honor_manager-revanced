using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ECController.Theme
{
    /// <summary>圆角与绘制工具的公共助手，附带绘制对象缓存。</summary>
    internal static class Draw
    {
        // ------------------------------------------------------------------
        // 缓存说明（重要）
        //
        // 自绘控件的 OnPaint 每次都会跑，里面 new 出来的 GraphicsPath / Pen / SolidBrush
        // 全是 GDI+ 的原生对象。一张窗体有几十个自绘控件，一帧就是上百次原生分配。
        // 这些对象的"形状"在整个进程里就那么几种，缓存起来一次建好即可。
        //
        // ⚠ 拿到缓存对象后**不要套 using**，也不要修改它的属性。
        //   using 会在块结束时 Dispose，把缓存对象销毁，下一次绘制就抛
        //   "使用已释放的对象"。原先这里都是 `using (Pen p = new Pen(...))` 的写法，
        //   改成缓存后必须同步去掉 using。
        // ------------------------------------------------------------------

        private struct PathKey : IEquatable<PathKey>
        {
            private readonly int _x, _y, _w, _h, _r;

            public PathKey(Rectangle rect, int radius)
            {
                _x = rect.X; _y = rect.Y; _w = rect.Width; _h = rect.Height; _r = radius;
            }

            public bool Equals(PathKey other)
            {
                return _x == other._x && _y == other._y && _w == other._w &&
                       _h == other._h && _r == other._r;
            }

            public override bool Equals(object obj)
            {
                return obj is PathKey && Equals((PathKey)obj);
            }

            public override int GetHashCode()
            {
                return ((_x * 397 ^ _y) * 397 ^ _w) * 397 ^ _h * 31 ^ _r;
            }
        }

        private static readonly Dictionary<PathKey, GraphicsPath> PathCache =
            new Dictionary<PathKey, GraphicsPath>();

        private static readonly Dictionary<int, Pen> PenCache = new Dictionary<int, Pen>();

        private static readonly Dictionary<int, SolidBrush> BrushCache =
            new Dictionary<int, SolidBrush>();

        /// <summary>缓存上限。超过就整体清掉重建，避免极端情况下无限增长。</summary>
        private const int CacheLimit = 192;

        /// <summary>
        /// 圆角矩形路径。结果被缓存，**不要用 using 包**，也不要修改返回值。
        /// </summary>
        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            PathKey key = new PathKey(r, radius);

            GraphicsPath cached;
            if (PathCache.TryGetValue(key, out cached))
                return cached;

            if (PathCache.Count >= CacheLimit)
            {
                foreach (GraphicsPath p in PathCache.Values)
                    p.Dispose();

                PathCache.Clear();
            }

            GraphicsPath path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(r);
            }
            else
            {
                int d = radius * 2;
                if (d > r.Width) d = r.Width;
                if (d > r.Height) d = r.Height;
                if (d <= 0)
                {
                    path.AddRectangle(r);
                }
                else
                {
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                }
            }

            PathCache[key] = path;

            return path;
        }

        /// <summary>纯色填充刷。结果被缓存，**不要用 using 包**。</summary>
        public static SolidBrush Fill(Color color)
        {
            int key = color.ToArgb();

            SolidBrush cached;
            if (BrushCache.TryGetValue(key, out cached))
                return cached;

            if (BrushCache.Count >= CacheLimit)
            {
                foreach (SolidBrush b in BrushCache.Values)
                    b.Dispose();

                BrushCache.Clear();
            }

            SolidBrush brush = new SolidBrush(color);
            BrushCache[key] = brush;

            return brush;
        }

        /// <summary>实线画笔（平头）。结果被缓存，**不要用 using 包**。</summary>
        public static Pen Stroke(Color color, float width)
        {
            // width 只用到 1 / 1.6 / 1.8 这几个值，量化到 0.1px 做 key 足够
            int key = color.ToArgb() * 31 + (int)Math.Round(width * 10);

            Pen cached;
            if (PenCache.TryGetValue(key, out cached))
                return cached;

            if (PenCache.Count >= CacheLimit)
            {
                foreach (Pen p in PenCache.Values)
                    p.Dispose();

                PenCache.Clear();
            }

            Pen pen = new Pen(color, width);
            PenCache[key] = pen;

            return pen;
        }

        /// <summary>
        /// 圆头圆角画笔（画勾、折线箭头用）。
        /// 与 <see cref="Stroke"/> 分开缓存，因为线帽是画笔自身的属性，不能混用。
        /// </summary>
        public static Pen StrokeRound(Color color, float width)
        {
            // 用负数区间避开与 Stroke 的 key 相撞
            int key = -(color.ToArgb() * 31 + (int)Math.Round(width * 10) + 1);

            Pen cached;
            if (PenCache.TryGetValue(key, out cached))
                return cached;

            if (PenCache.Count >= CacheLimit)
            {
                foreach (Pen p in PenCache.Values)
                    p.Dispose();

                PenCache.Clear();
            }

            Pen pen = new Pen(color, width);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;

            PenCache[key] = pen;

            return pen;
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
    /// 自绘控件的背景清理。
    ///
    /// 为什么必须显式做这件事：<c>ButtonBase</c>（Button / CheckBox / RadioButton 的基类）
    /// 在构造时打开了 <c>ControlStyles.Opaque</c>。该样式表示"控件自己负责整个区域，
    /// 不要帮我擦背景"；而 WinForms 只有在 Opaque 关闭时才会调用 OnPaintBackground。
    /// 一旦某控件同时是 Opaque 且 BackColor 为 Transparent，就没有任何一方负责擦除，
    /// 开启双缓冲后图面会被反复复用——上一帧、甚至别的窗口留下的像素会一直残留在
    /// 控件区域里，看起来就是"元素重叠、文字重影"。
    ///
    /// 因此自绘控件统一：关掉 Opaque，并在这里用父级的有效底色铺满自身区域。
    /// </summary>
    internal static class FluentBackground
    {
        /// <summary>用父控件的底色铺满控件区域（父级为空的极端情况回退到窗体底色）。</summary>
        public static void Fill(Control c, Graphics g)
        {
            // 向上找第一个"真正有底色"的祖先：
            //   · 遇到窗体 —— 身后是窗体的**渐变**底，要按控件所在的纵向区段取色；
            //   · 遇到不透明的普通容器 —— 直接用它的 BackColor。
            // 不能只看直接父级：父级若是 Transparent，取到的 A=0 会造成纯色补丁，
            // 而且透明容器本身还会引发重复绘制（见 MainForm.Designer.cs 里 pnlBootGroups 的注释）。
            Control ancestor = c.Parent;
            Form form = null;
            Color solid = Color.Empty;

            while (ancestor != null)
            {
                Form asForm = ancestor as Form;

                if (asForm != null)
                {
                    form = asForm;
                    break;
                }

                if (ancestor.BackColor.A != 0)
                {
                    solid = ancestor.BackColor;
                    break;
                }

                ancestor = ancestor.Parent;
            }

            if (form != null && !form.IsDisposed)
            {
                Rectangle full = form.ClientRectangle;

                if (full.Height > 0)
                {
                    // 线性渐变在任意纵向区段上仍是线性渐变，所以按控件上下沿各自
                    // 对应的比例取端点色即可，不需要做坐标变换或贴位图。
                    Point origin = form.PointToClient(c.PointToScreen(Point.Empty));
                    double top = origin.Y / (double)full.Height;
                    double bottom = (origin.Y + c.Height) / (double)full.Height;

                    Color c1 = FluentTheme.Blend(FluentTheme.WindowBackgroundTop,
                        FluentTheme.WindowBackground, top);
                    Color c2 = FluentTheme.Blend(FluentTheme.WindowBackgroundTop,
                        FluentTheme.WindowBackground, bottom);

                    using (LinearGradientBrush gradient = new LinearGradientBrush(
                        c.ClientRectangle, c1, c2, LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(gradient, c.ClientRectangle);
                    }

                    return;
                }
            }

            if (solid.A == 0)
                solid = FluentTheme.WindowBackground;

            g.FillRectangle(Draw.Fill(solid), c.ClientRectangle);
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
            // 卡片自绘的是白底圆角，BackColor 必须与之一致：
            // 子控件的透明合成取的是父级 BackColor，不一致就会在控件区域留下灰色补丁。
            BackColor = FluentTheme.CardBackground;
            Padding = new Padding(FluentTheme.CardPadding);
        }

        /// <summary>圆角之外要露出窗体底色，所以先铺父级颜色，再由 OnPaint 画圆角白底。</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FluentBackground.Fill(this, e.Graphics);
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
                GraphicsPath path = Draw.RoundedRect(r, FluentTheme.CornerRadius);

                g.FillPath(Draw.Fill(FluentTheme.CardBackground), path);
                g.DrawPath(Draw.Stroke(FluentTheme.CardBorder, 1f), path);

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
            // ButtonBase 默认开着 Opaque（表示"不用帮我擦背景"），
            // 与 Transparent 叠加会让圆角外残留上一帧像素，必须显式关掉。
            SetStyle(ControlStyles.Opaque, false);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Font = FluentTheme.BodyFont;
            Cursor = Cursors.Hand;
            Height = 34;
        }

        /// <summary>圆角外露出父级底色，避免按钮四角出现方块。</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FluentBackground.Fill(this, e.Graphics);
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
                GraphicsPath path = Draw.RoundedRect(r, FluentTheme.InputCornerRadius);

                g.FillPath(Draw.Fill(back), path);

                if (border != Color.Empty)
                    g.DrawPath(Draw.Stroke(border, 1f), path);

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
            // 见 FluentBackground 注释：Opaque + Transparent = 没人擦背景，出重影。
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            Font = FluentTheme.BodyFont;
            AutoSize = false;
            Height = 24;
            Cursor = Cursors.Hand;
        }

        /// <summary>自绘控件不擦背景就会留下上一帧的残影，这里铺上父级底色。</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FluentBackground.Fill(this, e.Graphics);
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

                GraphicsPath path = Draw.RoundedRect(box, 4);

                g.FillPath(Draw.Fill(fill), path);

                if (border != Color.Empty)
                    g.DrawPath(Draw.Stroke(border, 1f), path);

                if (Checked)
                {
                    // 勾：两段折线，白色 1.6px 圆头
                    float cx = box.X + box.Width / 2f;
                    float cy = box.Y + box.Height / 2f;
                    g.DrawLines(Draw.StrokeRound(Color.White, 1.8f), new[]
                    {
                        new PointF(cx - 3.6f, cy + 0.2f),
                        new PointF(cx - 1.1f, cy + 2.8f),
                        new PointF(cx + 3.8f, cy - 2.9f)
                    });
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
        /// <summary>右端需要盖掉原生绘制、留给自绘箭头的宽度。</summary>
        private const int ArrowAreaWidth = 30;

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
            // 0x000F = WM_PAINT，绘制完成后补上箭头与边框。
            // 原生 ComboBox 不设 UserPaint，WinForms 不会调 OnPaint，只能在这里补画。
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
                // 原生 ComboBox 会在右端自己画一个下拉按钮和箭头，与这里的自绘 V 叠在一起
                // 变成"两个箭头"。先用控件底色把右端这一条盖掉，再画我们自己的边框和箭头。
                gg.FillRectangle(Draw.Fill(FluentTheme.InputBackground), new Rectangle(
                    Math.Max(0, Width - ArrowAreaWidth), 1,
                    Math.Max(0, ArrowAreaWidth - 1), Math.Max(0, Height - 2)));

                Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
                gg.DrawRectangle(Draw.Stroke(
                    _hover ? FluentTheme.InputBorderHover : FluentTheme.InputBorder, 1f), r);

                // 右侧下拉箭头（V 形折线）
                float cx = Width - 16f;
                float cy = Height / 2f;
                gg.DrawLines(Draw.StrokeRound(FluentTheme.TextSecondary, 1.6f), new[]
                {
                    new PointF(cx - 4f, cy - 2f),
                    new PointF(cx, cy + 2f),
                    new PointF(cx + 4f, cy - 2f)
                });
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
            // 同 FluentCheckBox：必须关掉 ButtonBase 带来的 Opaque。
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Transparent;
            Font = FluentTheme.BodyFont;
            AutoSize = false;
            Height = 24;
            Cursor = Cursors.Hand;
        }

        /// <summary>自绘控件不擦背景就会留下上一帧的残影，这里铺上父级底色。</summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FluentBackground.Fill(this, e.Graphics);
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

                g.DrawEllipse(Draw.Stroke(ring, Checked ? 1.6f : 1f), box);

                if (Checked)
                {
                    int inset = 5;
                    Rectangle inner = new Rectangle(box.X + inset, box.Y + inset,
                        box.Width - inset * 2, box.Height - inset * 2);
                    g.FillEllipse(Draw.Fill(on ? FluentTheme.Accent : FluentTheme.AccentDisabled), inner);
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
