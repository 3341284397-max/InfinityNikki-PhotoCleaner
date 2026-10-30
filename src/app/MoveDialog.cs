using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NikkiPhotoCleaner
{
    /// <summary>
    /// 「确认移动」窗口。
    ///
    /// 以前这里是 MessageBox，名字只能列前 12 个，剩下的写成「以及另外 N 张」。
    /// 一次移动上百张时，第 13 张以后你就没法核对了 —— 而这正是最需要看一眼的时候。
    /// 现在改成带滚动列表的窗口：有多少张列多少张，列表自己滚，窗口高度不变。
    /// </summary>
    internal class MoveDialog : Form
    {
        private readonly List<PhotoItem> _items;
        private readonly string _pending;
        private readonly int _tooNew;
        private readonly int _minAgeDays;
        private readonly float _scale;

        private BufferedListView _list;
        private ListHeader _listHeader;
        private Panel _foot;
        private Label _lblSum;
        private SoftButton _btnOk;
        private SoftButton _btnCancel;

        private int S(int v) { return (int)Math.Round(v * _scale); }

        public MoveDialog(List<PhotoItem> items, string pending, int tooNew, int minAgeDays)
        {
            _items = items;
            _pending = pending;
            _tooNew = tooNew;
            _minAgeDays = minAgeDays;
            _scale = DeviceDpi / 96f;

            Text = L10n.T("确认移动");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Theme.WindowIcon; } catch { }

            var clientW = S(L10n.IsEnglish ? 720 : 640);

            // 说明会随语言、DPI 折成两三行，按实测高度算，别写死
            var sub = L10n.T("这只是移动，不是删除。移动后文件夹会自动打开，核对完 Ctrl+A 全选、按 Delete 才真正删掉；移错了点「移回原目录」可以全部移回。");
            var subH = TextRenderer.MeasureText(sub, Theme.F(8.6f),
                new Size(clientW - S(48), 0), TextFormatFlags.WordBreak).Height;
            var headH = S(18) + S(30) + S(6) + subH + S(10);

            var infoH = S(54);

            // 行高 38，加表头 30；列表高度按张数收缩，但留上下限，别让 3 张时窗口空一大片。
            // 末尾那 30 要给卡片内边距和行间距留余量，不然差几像素就会多出一条滚动条。
            var rows = Math.Min(_items.Count, 8);
            var listH = S(30) + rows * S(38) + S(30);
            listH = Math.Max(S(130), Math.Min(listH, S(366)));

            var warnH = 0;
            string warn = null;
            if (_tooNew > 0)
            {
                warn = L10n.F("其中 {0} 张拍摄不足 {1} 天，可能只是还没同步到游戏相册。移动前请再看一眼；不需要移动的话点「取消」，回列表取消勾选。",
                              _tooNew, _minAgeDays);
                // 高度交给 NotePanel 自己算：它知道自己的内边距，外面按估算值铺就会裁掉最后一行
                warnH = NotePanel.HeightFor(warn, clientW - S(48)) + S(8);
            }

            var footH = S(84);

            ClientSize = new Size(clientW, headH + infoH + listH + warnH + footH + S(16));
            Build(headH, infoH, listH, warnH, footH, sub, warn);
        }

        /// <summary>对话框也要染标题栏，否则主窗口和对话框一个窗口两种底色。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.TintCaption(this);
        }

        private void Build(int headH, int infoH, int listH, int warnH, int footH, string sub, string warn)
        {
            // ---- 列表（Fill，先加，最后才分配剩余空间）----
            var bodyWrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(S(24), 0, S(24), S(8))
            };

            var card = new CardPanel
            {
                Dock = DockStyle.Fill,
                Inset = 0,
                Radius = Theme.RCard,
                PadX = S(12),
                PadY = S(8)
            };
            card.ApplyPadding();
            card.Padding = new Padding(S(10), S(6), S(10), S(8));
            card.Height = listH;

            _list = new BufferedListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = false,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                HideSelection = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = Theme.F(9f),
                OwnerDraw = true,
                HeaderStyle = ColumnHeaderStyle.None
            };
            _list.Columns.Add(L10n.T("文件名"), 300);
            _list.Columns.Add(L10n.T("状态"), S(78));
            _list.Columns.Add(L10n.T("大小"), S(74));

            var il = new ImageList { ImageSize = new Size(1, S(38)), ColorDepth = ColorDepth.Depth32Bit };
            il.Images.Add(new Bitmap(1, S(38)));
            _list.SmallImageList = il;

            _listHeader = new ListHeader(new[]
            {
                L10n.T("文件名"), L10n.T("状态"), L10n.T("大小")
            }, 2)
            { Dock = DockStyle.Top, Target = _list };

            _list.DrawColumnHeader += (s, e) => { e.DrawDefault = false; };
            _list.DrawItem += DialogDrawItem;
            _list.DrawSubItem += DialogDrawSubItem;
            _list.Resize += (s, e) => { LayoutListColumns(); RoundedClip.Apply(_list, 10); };

            _list.BeginUpdate();
            foreach (var p in _items)
            {
                var it = new ListViewItem(p.Name);
                it.SubItems.Add(StatusText.Name(p.Status));
                it.SubItems.Add(RecordCleaner.Size(p.Size));
                it.Tag = p;
                it.ToolTipText = p.Name;
                _list.Items.Add(it);
            }
            _list.EndUpdate();

            card.Controls.Add(_list);
            card.Controls.Add(_listHeader);
            bodyWrap.Controls.Add(card);

            // ---- 目标路径 ----
            var info = new Panel { Dock = DockStyle.Top, Height = infoH, BackColor = Theme.Bg, Padding = new Padding(S(24), 0, S(24), S(4)) };
            var infoCard = new CardPanel
            {
                Dock = DockStyle.Fill,
                Inset = 0,
                Radius = Theme.RSm,
                PadX = S(14),
                PadY = S(8)
            };
            infoCard.ApplyPadding();

            var lblCap = new Label
            {
                Text = L10n.T("移入"),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.4f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent
            };
            var lblPath = new Label
            {
                Text = _pending,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = Theme.F(8.8f),
                ForeColor = Theme.Text2,
                BackColor = Color.Transparent
            };
            infoCard.Controls.Add(lblPath);
            infoCard.Controls.Add(lblCap);
            infoCard.Resize += (s, e) =>
            {
                var capW = TextRenderer.MeasureText(lblCap.Text, lblCap.Font).Width + S(6);
                var inner = infoCard.Width - infoCard.Padding.Left - infoCard.Padding.Right;
                var y = (infoCard.Height - S(22)) / 2;
                lblCap.SetBounds(infoCard.Padding.Left, y, capW, S(22));
                lblPath.SetBounds(infoCard.Padding.Left + capW + S(10),
                                  y, Math.Max(S(60), inner - capW - S(10)), S(22));
            };
            info.Controls.Add(infoCard);

            // ---- 标题 ----
            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = headH,
                BackColor = Theme.Bg,
                Padding = new Padding(S(24), S(18), S(24), S(2))
            };
            // Dock 由 z-order 决定：后添加的先停靠。这里先加说明（Fill）再加标题（Top），
            // 反过来的话 Fill 会铺满整个面板，标题压上去正好盖住说明第一行。
            head.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = sub,
                Padding = new Padding(0, S(4), 0, 0),
                Font = Theme.F(8.6f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent
            });
            head.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = S(30),
                Text = L10n.F("下面这 {0} 张照片将被移入待清理文件夹", _items.Count),
                Font = Theme.F(12.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            });

            // ---- 太新的提醒 ----
            Control warnCard = null;
            if (warnH > 0)
            {
                var wrap = new Panel { Dock = DockStyle.Bottom, Height = warnH, BackColor = Theme.Bg, Padding = new Padding(S(24), 0, S(24), S(8)) };
                var note = new NotePanel { Dock = DockStyle.Fill, Text = warn, Accent = true };
                wrap.Controls.Add(note);
                warnCard = wrap;
            }

            // ---- 底栏 ----
            _foot = new Panel { Dock = DockStyle.Bottom, Height = footH, BackColor = Theme.Bg, Padding = new Padding(S(24), 0, S(24), 0) };
            _foot.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawLine(pen, S(24), 0, _foot.Width - S(24), 0);
            };

            var totalBytes = _items.Sum(p => p.Size);
            _lblSum = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.6f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent,
                Text = L10n.F("共 {0} 张，合计 {1}", _items.Count, RecordCleaner.Size(totalBytes))
            };

            _btnOk = new SoftButton(L10n.F("移动 {0} 张", _items.Count), BtnKind.Primary, S(104));
            _btnOk.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            _btnCancel = new SoftButton(L10n.T("取消"), BtnKind.Ghost, S(84));
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            // SoftButton 不是 IButtonControl，接不上 Form.CancelButton，Esc 自己接
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
            };

            _foot.Controls.Add(_lblSum);
            _foot.Controls.Add(_btnOk);
            _foot.Controls.Add(_btnCancel);
            _foot.Resize += (s, e) => LayoutFoot();

            // 加顺序决定停靠顺序：Fill 打头，其余后加的贴外沿
            Controls.Add(bodyWrap);
            Controls.Add(info);
            Controls.Add(head);
            if (warnCard != null) Controls.Add(warnCard);
            Controls.Add(_foot);

            LayoutFoot();
        }

        private void LayoutFoot()
        {
            if (_foot == null || _btnOk == null) return;
            var gap = S(8);
            var btnH = S(34);
            var y = (Math.Max(btnH, _foot.Height) - btnH) / 2;

            var right = _foot.Width - S(24);
            _btnOk.SetBounds(right - _btnOk.Width, y, _btnOk.Width, btnH);
            right -= _btnOk.Width + gap;
            _btnCancel.SetBounds(right - _btnCancel.Width, y, _btnCancel.Width, btnH);
            right -= _btnCancel.Width + gap;

            _lblSum.SetBounds(S(24), 0, Math.Max(S(80), right - S(24)), _foot.Height);
        }

        private void LayoutListColumns()
        {
            if (_list == null || _list.Columns.Count < 3) return;

            var statusW = TextRenderer.MeasureText(StatusText.Name(StatusText.Suspect), Theme.F(8.2f)).Width + S(24);
            var sizeW = TextRenderer.MeasureText("000.0 MB", Theme.F(8.6f)).Width + S(20);
            var scroll = SystemInformation.VerticalScrollBarWidth;

            _list.BeginUpdate();
            try
            {
                _list.Columns[1].Width = statusW;
                _list.Columns[2].Width = sizeW;
                var nameW = _list.ClientSize.Width - statusW - sizeW - scroll - S(4);
                _list.Columns[0].Width = Math.Max(S(140), nameW);
            }
            finally { _list.EndUpdate(); }
        }

        // ==================== 列表自绘 ====================

        private void DialogDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            var g = e.Graphics;
            var row = new Rectangle(1, e.Bounds.Top, _list.ClientSize.Width - 2, e.Bounds.Height - 1);
            if (e.Item.Selected) Theme.FillRound(g, row, 8, Theme.GreenSoft);
        }

        private void DialogDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = false;
            var g = e.Graphics;
            var p = e.Item.Tag as PhotoItem;
            if (p == null) return;

            var cell = e.Bounds;
            var mid = cell.Top + cell.Height / 2;

            if (e.ColumnIndex == 1)
            {
                Color bg, fore, border;
                Theme.StatusPalette(p.Status, out bg, out fore, out border);
                var text = StatusText.Name(p.Status);
                var tw = TextRenderer.MeasureText(text, Theme.F(8.2f)).Width;
                var pill = new Rectangle(cell.Left + 2, mid - 11, Math.Min(tw + 20, cell.Width - 6), 22);
                Theme.DrawRound(g, pill, 11, bg, border);
                Theme.Str(g, text, Theme.F(8.2f), fore, pill, Theme.CenterMid);
                return;
            }

            if (e.ColumnIndex == 2)
            {
                var r = new Rectangle(cell.Left, cell.Top, cell.Width - 12, cell.Height);
                Theme.Str(g, RecordCleaner.Size(p.Size), Theme.F(8.6f), Theme.Text3, r, Theme.RightMid);
                return;
            }

            var rn = new Rectangle(cell.Left + 4, cell.Top, cell.Width - 8, cell.Height);
            Theme.Str(g, p.Name, Theme.F(8.9f), e.Item.Selected ? Theme.Text : Theme.Text2, rn, Theme.LeftMid);
        }
    }

    /// <summary>
    /// 一条圆角提示条，文字自动折行。
    /// 用它而不是 Label：Label 的 AutoSize 在多行 + 不同 DPI 下会把最后一行吃掉。
    /// </summary>
    internal class NotePanel : Control
    {
        public bool Accent;
        private readonly Font _font;

        public NotePanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            _font = Theme.F(8.6f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _font != null) _font.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>按给定宽度算出这个提示条需要多高（含内边距）。外面据此分配高度，不然会裁掉最后一行。</summary>
        public static int HeightFor(string text, int width)
        {
            var inner = Math.Max(40, width - 3 - 28);   // 与 OnPaint 的文字区算法保持一致
            using (var f = Theme.F(8.6f))
            {
                var h = TextRenderer.MeasureText(text ?? "", f, new Size(inner, 0),
                    TextFormatFlags.WordBreak).Height;
                return h + 20 + 3;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bg = Accent ? Theme.AmberSoft : Theme.CardSoft;
            var line = Accent ? Theme.AmberLine : Theme.LineSoft;
            var fore = Accent ? Theme.AmberDeep : Theme.Text2;

            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            Theme.FillRound(g, r, Theme.RSm, bg);
            using (var p = Theme.Round(r, Theme.RSm))
            using (var pen = new Pen(line, 1f))
                g.DrawPath(pen, p);

            var text = new Rectangle(r.Left + 14, r.Top + 10, r.Width - 28, r.Height - 20);
            TextRenderer.DrawText(g, Text ?? "", _font, text, fore,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }
}
