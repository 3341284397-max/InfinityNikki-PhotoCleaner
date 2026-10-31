using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace NikkiPhotoCleaner
{
    /// <summary>
    /// 界面的视觉基座：配色、圆角、自定义按钮 / 卡片 / 统计块 / 胶囊开关。
    /// 与网页版共用同一套色板（米白 + 淡黄 + 露草绿），让 exe 和网页看起来是同一个产品。
    /// 全部自绘：WinForms 原生控件的灰边框、方角、蓝色选中在这套配色里都很突兀。
    /// </summary>
    internal static class Theme
    {
        /// <summary>
        /// 程序图标。csproj 的 ApplicationIcon 已经把 app.ico 嵌进 exe，
        /// 从 exe 里取回来即可，不必再做一份嵌入资源。
        /// </summary>
        public static Icon WindowIcon
        {
            get
            {
                try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
                catch { return SystemIcons.Application; }
            }
        }

        // ---------- 底色 ----------
        public static readonly Color Bg = Color.FromArgb(0xFA, 0xF7, 0xEF);
        public static readonly Color BgWarm = Color.FromArgb(0xF6, 0xF1, 0xE3);
        public static readonly Color Card = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color CardWarm = Color.FromArgb(0xFF, 0xFD, 0xF6);
        public static readonly Color CardSoft = Color.FromArgb(0xFB, 0xF8, 0xF0);

        // ---------- 线条 ----------
        public static readonly Color Line = Color.FromArgb(0xEA, 0xE4, 0xD4);
        public static readonly Color LineSoft = Color.FromArgb(0xF2, 0xED, 0xE0);

        // ---------- 文字 ----------
        public static readonly Color Text = Color.FromArgb(0x3B, 0x42, 0x3A);
        public static readonly Color Text2 = Color.FromArgb(0x79, 0x82, 0x7A);
        public static readonly Color Text3 = Color.FromArgb(0xA6, 0xAD, 0xA4);

        // ---------- 露草绿 ----------
        public static readonly Color Green = Color.FromArgb(0x74, 0xAE, 0x8E);
        public static readonly Color GreenDeep = Color.FromArgb(0x4E, 0x84, 0x67);
        public static readonly Color GreenDark = Color.FromArgb(0x3D, 0x6B, 0x53);
        public static readonly Color GreenSoft = Color.FromArgb(0xE8, 0xF2, 0xEA);
        public static readonly Color GreenLine = Color.FromArgb(0xC9, 0xE0, 0xD2);

        // ---------- 淡黄 ----------
        public static readonly Color Yellow = Color.FromArgb(0xF3, 0xDF, 0xA2);
        public static readonly Color YellowSoft = Color.FromArgb(0xFC, 0xF5, 0xDF);
        public static readonly Color YellowLine = Color.FromArgb(0xF0, 0xE2, 0xBC);
        public static readonly Color YellowDeep = Color.FromArgb(0x84, 0x68, 0x3A);

        // ---------- 状态色 ----------
        public static readonly Color Clay = Color.FromArgb(0xC4, 0x7F, 0x6C);
        public static readonly Color ClaySoft = Color.FromArgb(0xF9, 0xEA, 0xE4);
        public static readonly Color ClayLine = Color.FromArgb(0xEB, 0xC9, 0xBC);
        public static readonly Color ClayDark = Color.FromArgb(0x9E, 0x5B, 0x48);
        public static readonly Color Amber = Color.FromArgb(0xC7, 0x9A, 0x45);
        public static readonly Color AmberSoft = Color.FromArgb(0xFA, 0xF1, 0xDC);
        public static readonly Color AmberLine = Color.FromArgb(0xEB, 0xD9, 0xB0);
        public static readonly Color AmberDeep = Color.FromArgb(0x84, 0x66, 0x28);

        // ---------- 圆角 ----------
        public const int RSm = 8;
        public const int R = 12;
        public const int RCard = 16;

        // ---------- 字体 ----------
        private static string _family;
        public static string Family
        {
            get
            {
                if (_family == null) _family = L10n.IsEnglish ? "Segoe UI" : "Microsoft YaHei UI";
                return _family;
            }
        }
        public static void ResetFamily() { _family = null; }

        public static Font F(float size) { return new Font(Family, size); }
        public static Font F(float size, FontStyle style) { return new Font(Family, size, style); }

        // ---------- 绘图基元 ----------

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return p;
            if (radius <= 0) { p.AddRectangle(r); return p; }
            var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Round(r, radius))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border.A > 0)
                    using (var pen = new Pen(border, 1f)) g.DrawPath(pen, p);
            }
        }

        /// <summary>把矩形按左上角对齐，压成「抗锯齿后不留虚边」的坐标。</summary>
        public static Rectangle Deflate(Rectangle r, int dx, int dy)
        {
            return new Rectangle(r.X + dx, r.Y + dy, Math.Max(0, r.Width - dx * 2), Math.Max(0, r.Height - dy * 2));
        }

        public static void Str(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(s)) return;
            TextRenderer.DrawText(g, s, f, r, c, flags);
        }

        public const TextFormatFlags LeftMid =
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags RightMid =
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        public const TextFormatFlags CenterMid =
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

        // ---------- 状态色板：与网页版徽章一致 ----------

        public static void StatusPalette(string status, out Color bg, out Color fore, out Color border)
        {
            switch (status)
            {
                case StatusText.InAlbum:
                    bg = GreenSoft; fore = GreenDark; border = GreenLine; return;
                case StatusText.Deleted:
                    bg = ClaySoft; fore = ClayDark; border = ClayLine; return;
                case StatusText.Suspect:
                    bg = AmberSoft; fore = AmberDeep; border = AmberLine; return;
                case StatusText.Ignored:
                    // 中性灰，比「太新」再淡一档：它代表「程序不再管这张了」，不该有任何情绪色
                    bg = LineSoft; fore = Text2; border = Line; return;
                case StatusText.Pending:
                    // 待清理视图里的那批：已经搬出截图目录，同样用中性色
                    bg = CardSoft; fore = Text2; border = Line; return;
                default:
                    bg = Color.FromArgb(0xF0, 0xF0, 0xEC);
                    fore = Text3;
                    border = Color.FromArgb(0xE2, 0xE2, 0xDA);
                    return;
            }
        }

        /// <summary>状态圆点颜色，用于统计卡左侧色条。</summary>
        public static Color StatusAccent(string status)
        {
            switch (status)
            {
                case StatusText.InAlbum: return Green;
                case StatusText.Deleted: return Clay;
                case StatusText.Suspect: return Amber;
                default: return Color.FromArgb(0xB9, 0xBE, 0xB6);
            }
        }
    }

    // ============================================================
    //  圆角卡片容器
    // ============================================================

    internal class CardPanel : Panel
    {
        /// <summary>视觉内缩，相当于外边距：卡片画在这个内缩范围里，子控件也按它让位。</summary>
        public int Inset = 0;
        public int Radius = Theme.RCard;
        public Color Fill = Theme.Card;
        public Color Border = Theme.Line;
        public int PadX = 16;
        public int PadY = 12;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
        }

        public void ApplyPadding()
        {
            Padding = new Padding(Inset + PadX, Inset + PadY, Inset + PadX, Inset + PadY);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(Inset, Inset, Width - Inset * 2 - 1, Height - Inset * 2 - 1);
            if (r.Width <= 0 || r.Height <= 0) return;
            Theme.DrawRound(g, r, Radius, Fill, Border);
        }
    }

    // ============================================================
    //  按钮：胶囊形，三档配色
    // ============================================================

    internal enum BtnKind { Primary, Ghost, Soft, Danger }

    internal class SoftButton : Control
    {
        private bool _hover, _down;
        private BtnKind _kind = BtnKind.Ghost;
        private int _minWidth = 0;
        private int _padX = 18;

        public BtnKind Kind
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        public int PadX { get { return _padX; } set { _padX = value; } }

        public SoftButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.F(9f);
            Height = 34;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        public SoftButton(string text, BtnKind kind, int minWidth) : this()
        {
            Text = text;
            _kind = kind;
            _minWidth = minWidth;
            Width = Math.Max(minWidth, PreferWidth());
            Height = 34;
        }

        public int MinWidth
        {
            get { return _minWidth; }
            set { _minWidth = value; if (Width < value) Width = value; }
        }

        private int PreferWidth()
        {
            var w = TextRenderer.MeasureText(Text ?? "", Font).Width + _padX * 2;
            return Math.Max(_minWidth, w);
        }

        /// <summary>文案变化（例如带上张数）后重新按文字算宽。</summary>
        public void FitWidth()
        {
            var w = Math.Max(_minWidth, PreferWidth());
            if (Width != w) Width = w;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            FitWidth();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, fore, border;

            if (!Enabled)
            {
                fill = _kind == BtnKind.Primary ? Color.FromArgb(0xD8, 0xE3, 0xDA) : Color.FromArgb(0xF6, 0xF3, 0xEA);
                fore = Color.FromArgb(0xB3, 0xB8, 0xB1);
                border = _kind == BtnKind.Ghost || _kind == BtnKind.Soft ? Theme.LineSoft : Color.Transparent;
            }
            else
            {
                switch (_kind)
                {
                    case BtnKind.Primary:
                        fill = _down ? Theme.GreenDark : (_hover ? Theme.GreenDeep : Theme.Green);
                        fore = Color.White;
                        border = Color.Transparent;
                        break;
                    case BtnKind.Danger:
                        fill = _down ? Color.FromArgb(0xF2, 0xD3, 0xC9) : (_hover ? Color.FromArgb(0xFB, 0xDF, 0xD6) : Theme.ClaySoft);
                        fore = Theme.ClayDark;
                        border = Theme.ClayLine;
                        break;
                    case BtnKind.Soft:
                        fill = _down ? Theme.Yellow : (_hover ? Color.FromArgb(0xFD, 0xF0, 0xC9) : Theme.YellowSoft);
                        fore = Theme.YellowDeep;
                        border = Theme.YellowLine;
                        break;
                    default: // Ghost
                        fill = _down ? Theme.GreenSoft : (_hover ? Theme.CardWarm : Theme.Card);
                        fore = _hover ? Theme.GreenDark : Theme.Text2;
                        border = _hover ? Theme.GreenLine : Theme.Line;
                        break;
                }
            }

            Theme.FillRound(g, r, Height / 2, fill);
            if (border.A > 0)
            {
                using (var p = Theme.Round(r, Height / 2))
                using (var pen = new Pen(border, 1f))
                    g.DrawPath(pen, p);
            }

            if (Focused && Enabled)
            {
                var fr = new Rectangle(2, 2, Width - 5, Height - 5);
                using (var p = Theme.Round(fr, (Height - 5) / 2))
                using (var pen = new Pen(Color.FromArgb(90, Theme.GreenDeep), 1f) { DashStyle = DashStyle.Dot })
                    g.DrawPath(pen, p);
            }

            Theme.Str(g, Text, Font, fore, r, Theme.CenterMid);
        }
    }

    // ============================================================
    //  统计小卡
    // ============================================================

    internal class StatCard : Control
    {
        private string _count = "0";
        private string _label = "";
        private Color _accent = Theme.Green;
        private bool _hover;

        public string Count { get { return _count; } set { _count = value ?? "0"; Invalidate(); } }
        public string Label { get { return _label; } set { _label = value ?? ""; Invalidate(); } }
        public Color Accent { get { return _accent; } set { _accent = value; Invalidate(); } }

        /// <summary>可点击时给出悬停反馈，让「点卡片＝批量勾选」这件事自己能说明白。</summary>
        public bool Hoverable
        {
            get;
            set;
        }

        public StatCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (!Hoverable) return;
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!Hoverable) return;
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.DrawRound(g, r, Theme.R, Theme.Card, _hover ? Theme.GreenLine : Theme.Line);

            // 左侧色条
            var barH = Math.Max(10, Height - 26);
            Theme.FillRound(g, new Rectangle(0, (Height - barH) / 2, 3, barH), 2, _accent);

            var padL = 16;
            var inner = new Rectangle(padL, 4, Width - padL - 12, Height - 8);

            // 数字与标签按字体实际高度堆叠，不能按百分比切——
            // 卡片矮一点时数字顶部就会被裁掉半个字。
            using (var numFont = Theme.F(15f, FontStyle.Bold))
            using (var lblFont = Theme.F(8.3f))
            {
                var numH = (int)Math.Ceiling(numFont.GetHeight(g));
                var lblH = (int)Math.Ceiling(lblFont.GetHeight(g)) + 1;
                var gap = 5;   // 数字与标签之间留一口气，贴着会显得糊在一起
                var total = numH + gap + lblH;

                // 整体在卡片内垂直居中；卡片实在太矮就贴顶，宁可压底部也不切顶部
                var y = inner.Y + Math.Max(0, (inner.Height - total) / 2);

                Theme.Str(g, _count, numFont, _accent,
                    new Rectangle(inner.X, y, inner.Width, numH), Theme.LeftMid);
                Theme.Str(g, _label, lblFont, Theme.Text3,
                    new Rectangle(inner.X, y + numH + gap, inner.Width, lblH), Theme.LeftMid);
            }
        }
    }

    // ============================================================
    //  胶囊开关（替代 CheckBox）
    // ============================================================

    internal class PillToggle : Control
    {
        private bool _checked;
        private bool _hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public PillToggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            Font = Theme.F(8.8f);
            Height = 30;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        public PillToggle(string text) : this() { Text = text; FitWidth(); }

        public void FitWidth()
        {
            var track = TrackSize();
            var w = TextRenderer.MeasureText(Text ?? "", Font).Width + 12 + track.Width + 9 + 12;
            if (Width != w) Width = w;
        }

        private Size TrackSize()
        {
            // 轨道尺寸跟着控件高度走，150% 缩放下不会缩成一个小圆点
            var h = Math.Max(14, (int)Math.Round(Height * 0.60));
            var w = (int)Math.Round(h * 1.75);
            return new Size(w, h);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); FitWidth(); }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Checked = !Checked;
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.DrawRound(g, r, Height / 2,
                _checked ? Theme.GreenSoft : (_hover ? Theme.CardWarm : Theme.Card),
                _checked ? Theme.GreenLine : (_hover ? Theme.GreenLine : Theme.Line));

            // 开关轨道 + 圆钮：比小方框更像「可以拨一下」，也更容易看出当前状态
            var t = TrackSize();
            var track = new Rectangle(11, (Height - t.Height) / 2, t.Width, t.Height);
            Theme.FillRound(g, track, t.Height / 2, _checked ? Theme.Green : Color.FromArgb(0xD5, 0xD9, 0xD0));

            var knobD = t.Height - 4;
            var knobX = _checked ? track.Right - knobD - 2 : track.X + 2;
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, knobX, track.Y + 2, knobD, knobD);

            var tr = new Rectangle(track.Right + 9, 0, Width - track.Right - 9 - 10, Height);
            Theme.Str(g, Text, Font, _checked ? Theme.GreenDark : Theme.Text2, tr, Theme.LeftMid);
        }
    }

    // ============================================================
    //  顶部品牌区（画一朵小花，呼应网页版 logo）
    // ============================================================

    // ============================================================
    //  圆角输入框容器（原生 TextBox 画不出圆角，只能套一层画底板）
    // ============================================================

    internal class PathBox : Panel
    {
        public PathBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.DrawRound(g, r, 9, Theme.CardWarm, Theme.LineSoft);
        }
    }

    // ============================================================
    //  自绘勾选行（清理记录对话框用）
    //  原生 CheckBox 的方块和系统蓝勾在这套配色里太扎眼，
    //  这里画成和照片列表一样的圆形勾选圈。
    // ============================================================

    internal class RoundCheck : Control
    {
        private bool _checked;
        private bool _hover;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public RoundCheck()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.F(9f);
            Height = 30;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) Checked = !Checked;
            base.OnMouseDown(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var d = Math.Max(14, (int)Math.Round(Height * 0.56));
            var box = new Rectangle(0, (Height - d) / 2, d, d);

            using (var b = new SolidBrush(_checked ? Theme.Green : (_hover ? Theme.GreenSoft : Color.White)))
                g.FillEllipse(b, box);
            using (var pen = new Pen(_checked ? Theme.Green : (_hover ? Theme.GreenLine : Theme.Line), 1.3f))
                g.DrawEllipse(pen, box);

            if (_checked)
            {
                using (var pen = new Pen(Color.White, 1.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    var cx = box.X + d / 2f;
                    var cy = box.Y + d / 2f;
                    var k = d / 16f;
                    g.DrawLines(pen, new[]
                    {
                        new PointF(cx - 3.6f * k, cy + 0.3f * k),
                        new PointF(cx - 1.1f * k, cy + 2.8f * k),
                        new PointF(cx + 3.8f * k, cy - 2.6f * k)
                    });
                }
            }

            var tr = new Rectangle(box.Right + 10, 0, Math.Max(10, Width - box.Right - 12), Height);
            Theme.Str(g, Text, Font, _checked ? Theme.Text : Theme.Text2, tr, Theme.LeftMid);
        }
    }

    // ============================================================
    //  列表表头（原生表头是系统灰，跟这套配色不搭，改成自绘一条）
    // ============================================================

    /// <summary>
    /// 开了双缓冲的 ListView。
    /// 自绘列表默认直接往屏幕 DC 上画，滚动和悬停高亮时上一帧会先被擦掉，
    /// 肉眼看就是整列文字在闪、像列表在反复刷新。DoubleBuffered 让每一帧先画到内存位图，
    /// 一次贴上去，闪烁就没有了。
    /// </summary>
    internal class BufferedListView : ListView
    {
        public BufferedListView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;
            UpdateStyles();
        }
    }

    internal class ListHeader : Control
    {
        public ListView Target;
        private readonly string[] _titles;
        private readonly Font _font;
        private readonly int _rightFrom;

        /// <param name="rightFrom">从第几列开始右对齐（主列表是「时间/大小」，对话框里只有「大小」）</param>
        public ListHeader(string[] titles, int rightFrom = 3)
        {
            _rightFrom = rightFrom;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
            _titles = titles;
            Height = 32;
            _font = Theme.F(8.3f);   // 缓存字体：每次重绘 new 一个 Font 会一直漏
            DoubleBuffered = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _font != null) _font.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            if (Target == null || _titles == null) return;

            var x = 0;
            for (var i = 0; i < Target.Columns.Count && i < _titles.Length; i++)
            {
                // 严格按列表实际列宽摆放，不要给最后一列补齐到控件右边——
                // 补了就会和下方同列内容错开，出现「表头的大小跑到最右边」。
                var w = Target.Columns[i].Width;
                var cell = new Rectangle(x + 12, 0, Math.Max(0, w - 16), Height);
                var flags = (i >= _rightFrom) ? Theme.RightMid : Theme.LeftMid;
                Theme.Str(g, _titles[i], _font, Theme.Text3, cell, flags);
                x += w;
            }

            using (var pen = new Pen(Theme.LineSoft, 1f))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    internal static class RoundedClip
    {
        /// <summary>把控件裁成圆角。列表、图片框靠它和卡片圆角对上。</summary>
        public static void Apply(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            var old = c.Region;
            using (var p = Theme.Round(new Rectangle(0, 0, c.Width, c.Height), radius))
                c.Region = new Region(p);
            if (old != null) old.Dispose();
        }
    }

    // ============================================================
    //  顶部品牌区（画一朵小花，呼应网页版 logo）
    // ============================================================

    internal class BrandPanel : Panel
    {
        /// <summary>标题与副标题直接画在面板上：Label 的自动高度在部分 DPI 下会切掉字头。</summary>
        public string Title = "";
        public string Subtitle = "";

        public BrandPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderingHint hint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var size = 34;
            var cy = Height / 2;
            var outer = new Rectangle(0, cy - size / 2, size, size);

            using (var b = new SolidBrush(Theme.YellowSoft)) g.FillEllipse(b, outer);
            using (var pen = new Pen(Color.FromArgb(0xF3, 0xE6, 0xC6), 1f)) g.DrawEllipse(pen, outer);

            var ox = outer.X + outer.Width / 2f;
            var oy = outer.Y + outer.Height / 2f;
            using (var b = new SolidBrush(Theme.Green))
            {
                g.FillEllipse(b, ox - 3.8f, oy - 11f, 7.6f, 10.4f);   // 上
                g.FillEllipse(b, ox - 3.8f, oy + 0.6f, 7.6f, 10.4f);  // 下
                g.FillEllipse(b, ox - 11f, oy - 3.8f, 10.4f, 7.6f);   // 左
                g.FillEllipse(b, ox + 0.6f, oy - 3.8f, 10.4f, 7.6f);  // 右
            }
            using (var b = new SolidBrush(Color.FromArgb(0xE9, 0xB9, 0x49)))
                g.FillEllipse(b, ox - 3.2f, oy - 3.2f, 6.4f, 6.4f);

            // ---- 标题 / 副标题 ----
            using (var tf = Theme.F(14f, FontStyle.Bold))
            using (var sf = Theme.F(8.6f))
            {
                var titleH = (int)Math.Ceiling(tf.GetHeight(g));
                var subH = (int)Math.Ceiling(sf.GetHeight(g));
                var total = titleH + 2 + subH;
                var y0 = Math.Max(2, (Height - total) / 2);

                Theme.Str(g, Title, tf, Theme.Text,
                    new Rectangle(46, y0, Math.Max(0, Width - 46 - 120), titleH), Theme.LeftMid);
                Theme.Str(g, Subtitle, sf, Theme.Text3,
                    new Rectangle(48, y0 + titleH + 2, Math.Max(0, Width - 48 - 120), subH), Theme.LeftMid);
            }

            g.TextRenderingHint = hint;
        }
    }

    // ============================================================
    //  系统标题栏染色
    //  WinForms 的标题栏由 DWM 绘制，自带灰蓝渐变，跟米白配色放一起很突兀。
    //  Windows 11 22H2 起提供 DWMWA_CAPTION_COLOR，把标题栏染成界面同色；
    //  更早的系统调用会失败，直接忽略，保留系统默认外观即可。
    // ============================================================

    internal static class Dwm
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        /// <summary>DWM 要的是 COLORREF（0x00BBGGRR），不是 .NET 的 ARGB。</summary>
        private static int Ref(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        public static void TintCaption(Form f, Color caption, Color text)
        {
            if (f == null || !f.IsHandleCreated) return;
            try
            {
                var cap = Ref(caption);
                var txt = Ref(text);
                // DWMWA_COLOR_NONE：去掉窗口外圈那条系统强调色细边，
                // 否则米白界面四周会镶一圈蓝紫色，比原生标题栏还扎眼。
                var brd = unchecked((int)0xFFFFFFFE);
                DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref cap, sizeof(int));
                DwmSetWindowAttribute(f.Handle, DWMWA_TEXT_COLOR, ref txt, sizeof(int));
                DwmSetWindowAttribute(f.Handle, DWMWA_BORDER_COLOR, ref brd, sizeof(int));
            }
            catch { }
        }

        public static void TintCaption(Form f)
        {
            TintCaption(f, Theme.Bg, Theme.Text);
        }
    }
}
