using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NikkiPhotoCleaner
{
    /// <summary>
    /// 一张可选中的位置卡片：左边一个单选圆点，标题一行、路径一行。
    ///
    /// 没用原生 RadioButton 是因为它在这套配色里显得很突兀（系统灰蓝的圆点 + 系统字体），
    /// 而这里要显示「标题 + 具体路径」两行，原生控件也摆不出层次。
    /// </summary>
    internal class OptionCard : Control
    {
        private readonly float _scale;
        private readonly Font _fTitle;
        private readonly Font _fPath;

        public bool Selected;
        public bool Hovered;

        /// <summary>点第三张卡（自选目录）时要弹文件夹选择框，由外面接。</summary>
        public event EventHandler Picked;

        private int S(int v) { return (int)Math.Round(v * _scale); }

        public OptionCard(float scale)
        {
            _scale = scale;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            _fTitle = Theme.F(9.6f, FontStyle.Bold);
            _fPath = Theme.F(8.4f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_fTitle != null) _fTitle.Dispose();
                if (_fPath != null) _fPath.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hovered = false; Invalidate(); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            if (Picked != null) Picked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var on = Enabled && Selected;
            var bg = !Enabled ? Theme.BgWarm
                   : on ? Theme.GreenSoft
                   : Hovered ? Theme.CardSoft
                   : Theme.Card;
            var line = !Enabled ? Theme.LineSoft
                     : on ? Theme.GreenLine
                     : Theme.Line;

            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            Theme.DrawRound(g, r, Theme.R, bg, line);

            // 单选圆点
            var d = S(15);
            var dot = new Rectangle(r.Left + S(14), r.Top + (r.Height - d) / 2, d, d);
            using (var pen = new Pen(on ? Theme.Green : (Enabled ? Theme.Text3 : Theme.Line), 1.4f * _scale))
                g.DrawEllipse(pen, dot);
            if (on)
            {
                var inner = S(7);
                using (var b = new SolidBrush(Theme.Green))
                    g.FillEllipse(b, dot.Left + (d - inner) / 2, dot.Top + (d - inner) / 2, inner, inner);
            }

            var textX = dot.Right + S(14);
            var textW = Math.Max(S(40), r.Right - textX - S(14));

            var titleRect = new Rectangle(textX, r.Top + S(10), textW, S(22));
            TextRenderer.DrawText(g, Text, _fTitle,
                titleRect, Enabled ? Theme.Text : Theme.Text3,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (!string.IsNullOrEmpty(SubText))
            {
                var subRect = new Rectangle(textX, titleRect.Bottom + S(1), textW, S(20));
                TextRenderer.DrawText(g, SubText, _fPath,
                    subRect, Enabled ? Theme.Text3 : Theme.Text3,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                    TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        /// <summary>卡片标题</summary>
        public string SubText { get; set; }
    }

    /// <summary>
    /// 定位到游戏目录之后问一次：使用数据要不要挪去和游戏放在一起。
    ///
    /// 为什么挪到这里问：建界面之前用户还没指定 ScreenShot 目录，推荐位置根本算不出来，
    /// 三张卡里只有「和 exe 同目录」是确定的 —— 那等于问了一个当时答不上来的问题。
    /// 现在拿到真实路径才问，推荐那一栏是真的路径，不选也只是维持现状。
    ///
    /// 为什么不默认帮你搬：搬动数据会换掉台账所在的地方，这种事必须让用户点一下头。
    /// </summary>
    internal class FirstRunDialog : Form
    {
        private readonly float _scale;
        private readonly string _gameBase;
        private readonly string _currentBase;
        private readonly bool _en;

        private OptionCard _cardGame, _cardCurrent, _cardCustom;
        private string _customBase;
        private Panel _foot;
        private SoftButton _btnOk, _btnDefault;
        private Label _lblFoot;

        /// <summary>选中的父目录（数据文件夹会建在它下面）。</summary>
        public string ChosenBase { get; private set; }

        private int S(int v) { return (int)Math.Round(v * _scale); }

        /// <param name="screenShot">已经定位到的 ScreenShot 目录；给 null 时自己去猜一次（截图入口用）。</param>
        public FirstRunDialog(string screenShot = null)
        {
            _scale = DeviceDpi / 96f;
            _en = L10n.IsEnglish;
            _currentBase = AppPaths.ToolRoot;

            Text = L10n.T("使用数据放在哪里");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Theme.WindowIcon; } catch { }

            // 有了 ScreenShot 就直接按它推「与游戏并列」那一级，纯字符串运算，不扫盘；
            // 没给才退回自动找（有 12 秒上限，只应发生在截图入口）。
            if (screenShot != null)
            {
                _gameBase = DataLocation.SiblingOf(screenShot);
            }
            else
            {
                Cursor = Cursors.WaitCursor;
                _gameBase = DataLocation.SuggestNearGame();
                Cursor = Cursors.Default;
            }

            Build();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.TintCaption(this);
        }

        private void Build()
        {
            var W = S(_en ? 680 : 620);
            var pad = S(26);
            var innerW = W - pad * 2;

            var title = L10n.T("使用数据要不要挪个位置");
            var sub = L10n.T("程序已经在 exe 旁边建好了「使用数据」文件夹 —— 相册台账、你标过的「忽略」、设置都在里面。"
                           + "现在定位到游戏目录了，所以能给一个更合适的位置：和游戏文件夹放在一起，"
                           + "既不会被游戏更新校验碰到，也不会一直留在桌面/下载文件夹里。"
                           + "不挪也行，之后在右上角「数据位置」里随时能改。");

            var subH = TextRenderer.MeasureText(sub, Theme.F(8.6f), new Size(innerW, 0),
                TextFormatFlags.WordBreak).Height;
            var headH = S(18) + S(30) + S(6) + subH + S(16);

            var cardH = S(62);
            var gap = S(8);
            var cardBlockH = cardH * 3 + gap * 2 + S(10);
            var footH = S(78);

            ClientSize = new Size(W, headH + cardBlockH + footH);

            // ---- 标题区 ----
            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = headH,
                BackColor = Theme.Bg,
                Padding = new Padding(pad, S(18), pad, 0)
            };
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
                Text = title,
                Font = Theme.F(13.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            });

            // ---- 三张卡 ----
            // 不给 body 设 Padding：Dock=Fill + Padding 时子控件的坐标原点在 padding 内侧，
            // 再按 ClientSize 算宽度就会右边溢出。这里统一在 LayoutCards 里算。
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg
            };

            var en = _en;

            // ① 和游戏放一起
            var gamePath = _gameBase == null ? null : DataLocation.DataPathFor(_gameBase, en);
            _cardGame = new OptionCard(_scale)
            {
                Text = L10n.T("搬到游戏旁边（推荐）"),
                SubText = gamePath ?? L10n.T("这个游戏目录推不出并列位置，用下面两个选项"),
                Enabled = gamePath != null
            };

            // ② 维持现状：数据此刻就在这儿，选它等于「别问了」
            _cardCurrent = new OptionCard(_scale)
            {
                Text = L10n.T("先不动它"),
                SubText = AppPaths.DataDir
            };

            // ③ 自己选
            _cardCustom = new OptionCard(_scale)
            {
                Text = L10n.T("我自己选一个位置…"),
                SubText = L10n.T("点这张卡会弹出文件夹选择框")
            };

            foreach (var c in new[] { _cardGame, _cardCurrent, _cardCustom }) body.Controls.Add(c);

            _cardGame.Picked += (s, e) => Select(_cardGame);
            _cardCurrent.Picked += (s, e) => Select(_cardCurrent);
            _cardCustom.Picked += (s, e) =>
            {
                if (PickFolder()) Select(_cardCustom);
            };

            _pad = pad;
            _cardH = cardH;
            _cardGap = gap;
            body.Resize += (s, e) => LayoutCards();
            body.HandleCreated += (s, e) => LayoutCards();

            // ---- 底栏 ----
            _foot = new Panel { Dock = DockStyle.Bottom, Height = footH, BackColor = Theme.Bg, Padding = new Padding(pad, 0, pad, 0) };
            _foot.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawLine(pen, pad, 0, _foot.Width - pad, 0);
            };

            _lblFoot = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.4f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent,
                Text = L10n.T("只管程序自己的数据，不动游戏文件，也不动照片。")
            };

            _btnDefault = new SoftButton(L10n.T("先不挪"), BtnKind.Ghost, S(104));
            _btnDefault.Click += (s, e) => { ChosenBase = _currentBase; DialogResult = DialogResult.OK; Close(); };

            _btnOk = new SoftButton(L10n.T("就用这里"), BtnKind.Primary, S(104));
            _btnOk.PadX = 16;
            _btnOk.FitWidth();
            _btnOk.Click += (s, e) => Accept();

            _btnDefault.PadX = 16;
            _btnDefault.FitWidth();

            _foot.Controls.Add(_lblFoot);
            _foot.Controls.Add(_btnOk);
            _foot.Controls.Add(_btnDefault);
            _foot.Resize += (s, e) => LayoutFoot();

            Controls.Add(body);
            Controls.Add(head);
            Controls.Add(_foot);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { ChosenBase = null; DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter) Accept();
            };

            // 默认停在推荐的那张；推不出并列位置就退到「先不动它」（风险最小的选项）
            Select(_cardGame.Enabled ? _cardGame : _cardCurrent);
            LayoutFoot();
            LayoutCards();
        }

        private int _pad, _cardH, _cardGap;

        /// <summary>
        /// 三张卡按顺序竖排。不要用 Visible 判断「这张卡要不要画」——
        /// 窗体还没显示时 Visible 恒为 false，卡就会一直停在 0×0 上（踩过）。
        /// </summary>
        private void LayoutCards()
        {
            if (_cardGame == null) return;

            var w = Math.Max(S(200), ClientSize.Width - _pad * 2);
            var y = 0;
            foreach (var c in new[] { _cardGame, _cardCurrent, _cardCustom })
            {
                c.SetBounds(_pad, y, w, _cardH);
                y += _cardH + _cardGap;
            }
        }

        private void LayoutFoot()
        {
            if (_foot == null || _btnOk == null) return;
            var gap = S(8);
            var btnH = S(34);
            var y = (Math.Max(btnH, _foot.Height) - btnH) / 2;
            var right = _foot.Width - S(26);

            _btnOk.SetBounds(right - _btnOk.Width, y, _btnOk.Width, btnH);
            right -= _btnOk.Width + gap;
            _btnDefault.SetBounds(right - _btnDefault.Width, y, _btnDefault.Width, btnH);
            right -= _btnDefault.Width + gap;

            _lblFoot.SetBounds(S(26), 0, Math.Max(S(60), right - S(26)), _foot.Height);
        }

        private void Select(OptionCard card)
        {
            foreach (var c in new[] { _cardGame, _cardCurrent, _cardCustom })
            {
                if (c == null) continue;
                c.Selected = ReferenceEquals(c, card);
                c.Invalidate();
            }
        }

        private bool PickFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = L10n.T("选一个文件夹，程序会在它里面建「使用数据」文件夹");
                fbd.ShowNewFolderButton = true;
                try { fbd.SelectedPath = AppPaths.ToolRoot; } catch { }

                if (fbd.ShowDialog(this) != DialogResult.OK) return false;

                _customBase = fbd.SelectedPath;
                _cardCustom.SubText = DataLocation.DataPathFor(_customBase, _en) ?? _customBase;
                _cardCustom.Invalidate();
                return true;
            }
        }

        /// <summary>当前选中的父目录。</summary>
        private string CurrentBase()
        {
            if (_cardGame.Selected) return _gameBase;
            if (_cardCurrent.Selected) return _currentBase;
            return _customBase;
        }

        private void Accept()
        {
            var baseDir = CurrentBase();
            if (string.IsNullOrEmpty(baseDir))
            {
                MessageBox.Show(this, L10n.T("还没选位置。点第三张卡片挑一个文件夹吧。"),
                    L10n.T("还差一步"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dir = DataLocation.DataPathFor(baseDir, _en);
            if (!CanWrite(dir))
            {
                MessageBox.Show(this,
                    L10n.F("这个位置写不进去：\n{0}\n\n可能没有权限（比如程序放在 C:\\Program Files 下），换一个位置，或者指到你的用户目录里。", dir),
                    L10n.T("换个位置"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ChosenBase = baseDir;
            DialogResult = DialogResult.OK;
            Close();
        }

        internal static bool CanWrite(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return false;
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>
    /// 「数据位置」：看一眼数据在哪、挪走、删掉，以及彻底卸载。
    ///
    /// 这个程序没有安装过程，所以也没有卸载程序。删掉 exe 就等于卸载，
    /// 但数据文件夹会留在原地 —— 这里就是给它收尾的地方。
    /// </summary>
    internal class DataLocationDialog : Form
    {
        private readonly float _scale;
        private readonly bool _en;

        private Panel _foot;
        private SoftButton _btnClose, _btnMove, _btnDelete;
        private Label _lblWhere;
        private readonly string _screenShot;

        private int S(int v) { return (int)Math.Round(v * _scale); }

        /// <summary>做过删除/搬移之后置位，主窗口据此决定要不要重扫。</summary>
        public bool Changed { get; private set; }

        public DataLocationDialog(string screenShot = null)
        {
            _scale = DeviceDpi / 96f;
            _en = L10n.IsEnglish;
            _screenShot = screenShot;

            Text = L10n.T("使用数据");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Theme.WindowIcon; } catch { }

            Build();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.TintCaption(this);
        }

        private void Build()
        {
            var W = S(_en ? 700 : 620);
            var pad = S(26);
            var innerW = W - pad * 2;

            var title = L10n.T("使用数据");
            var sub = L10n.T("台账、你标过的「忽略」、设置和缩略图缓存都在这个文件夹里。"
                           + "照片本身不在里面 —— 只有你主动移进来的「待清理照片」是例外。"
                           + "没有安装过程就没有卸载程序：删掉 exe 之后，"
                           + "这里这个文件夹和 %APPDATA% 里那条位置记录，都要在这里清掉。");

            var subH = TextRenderer.MeasureText(sub, Theme.F(8.6f), new Size(innerW, 0),
                TextFormatFlags.WordBreak).Height;
            var headH = S(18) + S(30) + S(6) + subH + S(14);

            var whereH = S(58);
            var footH = S(78);

            ClientSize = new Size(W, headH + whereH + footH);

            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = headH,
                BackColor = Theme.Bg,
                Padding = new Padding(pad, S(18), pad, 0)
            };
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
                Text = title,
                Font = Theme.F(13.5f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            });

            // ---- 当前路径 ----
            var wrap = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Bg,
                Padding = new Padding(pad, 0, pad, S(10))
            };
            var card = new CardPanel
            {
                Dock = DockStyle.Fill,
                Inset = 0,
                Radius = Theme.RSm,
                PadX = S(14),
                PadY = S(8)
            };
            card.ApplyPadding();

            var cap = new Label
            {
                Text = L10n.T("数据文件夹"),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.4f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent
            };
            _lblWhere = new Label
            {
                Text = AppPaths.DataDir,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Font = Theme.F(8.8f),
                ForeColor = Theme.Text2,
                BackColor = Color.Transparent
            };
            var btnOpen = new SoftButton(L10n.T("打开"), BtnKind.Ghost, S(64)) { Height = S(26) };
            btnOpen.Font = Theme.F(8.4f);
            btnOpen.Click += (s, e) => OpenFolder(AppPaths.DataDir);

            card.Controls.Add(_lblWhere);
            card.Controls.Add(cap);
            card.Controls.Add(btnOpen);
            card.Resize += (s, e) =>
            {
                var capW = TextRenderer.MeasureText(cap.Text, cap.Font).Width + S(6);
                var bw = btnOpen.Width;
                var inner = card.Width - card.Padding.Left - card.Padding.Right;
                var y = (card.Height - S(26)) / 2;
                cap.SetBounds(card.Padding.Left, y, capW, S(26));
                btnOpen.SetBounds(card.Width - card.Padding.Right - bw, y, bw, S(26));
                _lblWhere.SetBounds(card.Padding.Left + capW + S(10), y,
                    Math.Max(S(80), inner - capW - bw - S(24)), S(26));
            };
            wrap.Controls.Add(card);

            // ---- 底栏 ----
            _foot = new Panel { Dock = DockStyle.Bottom, Height = footH, BackColor = Theme.Bg, Padding = new Padding(pad, 0, pad, 0) };
            _foot.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawLine(pen, pad, 0, _foot.Width - pad, 0);
            };

            _btnMove = new SoftButton(L10n.T("更改位置…"), BtnKind.Ghost, S(104));
            // 「删除使用数据」和「彻底卸载」做的是同一件事，留两个按钮只会让人以为不一样：
            // 删掉数据却留着「数据放在哪」这条记录，重启后原地又长回来，那就不叫删干净。
            // 所以合成一个，连位置记录一起清掉，重启会重新问你要放哪 —— 这也是最直观的证据。
            _btnDelete = new SoftButton(L10n.T("删除使用数据…"), BtnKind.Danger, S(132));
            _btnClose = new SoftButton(L10n.T("关闭"), BtnKind.Primary, S(80));

            _btnMove.Click += (s, e) => MoveElsewhere();
            _btnDelete.Click += (s, e) => DeleteData();
            _btnClose.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            foreach (var b in new Control[] { _btnMove, _btnDelete, _btnClose })
            {
                // 多给一点内边距再重算宽度：中文「删除使用数据…」和英文「Delete app data…」
                // 都会把省略号顶到边框上，看起来像被切掉
                var sb = b as SoftButton;
                if (sb != null) { sb.PadX = 16; sb.FitWidth(); }
                _foot.Controls.Add(b);
            }
            _foot.Resize += (s, e) => LayoutFoot();

            Controls.Add(wrap);
            Controls.Add(head);
            Controls.Add(_foot);

            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.OK; Close(); }
            };

            LayoutFoot();
        }

        private void LayoutFoot()
        {
            if (_foot == null || _btnClose == null) return;
            var gap = S(8);
            var btnH = S(34);
            var y = (Math.Max(btnH, _foot.Height) - btnH) / 2;

            var right = _foot.Width - S(26);
            _btnClose.SetBounds(right - _btnClose.Width, y, _btnClose.Width, btnH);
            right -= _btnClose.Width + gap;

            var x = S(26);
            _btnMove.SetBounds(x, y, _btnMove.Width, btnH);
            x += _btnMove.Width + gap;
            _btnDelete.SetBounds(x, y, _btnDelete.Width, btnH);
        }

        private static void OpenFolder(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        /// <summary>
        /// 把现有数据搬到新位置。
        /// 复制 → 核对 → 删旧，三步都做完才算成功；旧目录删不掉会登记到待删清单。
        /// </summary>
        private void MoveElsewhere()
        {
            string baseDir;
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = L10n.T("选一个文件夹，数据文件夹会被搬进去");
                fbd.ShowNewFolderButton = true;
                try { fbd.SelectedPath = AppPaths.ToolRoot; } catch { }
                if (fbd.ShowDialog(this) != DialogResult.OK) return;
                baseDir = fbd.SelectedPath;
            }

            if (string.IsNullOrEmpty(baseDir)) return;

            var cur = AppPaths.ToolRoot;
            try
            {
                if (string.Equals(Path.GetFullPath(baseDir).TrimEnd('\\', '/'),
                                  Path.GetFullPath(cur).TrimEnd('\\', '/'),
                                  StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, L10n.T("新位置和现在的位置是同一个地方。"),
                        L10n.T("没动"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            catch { }

            var dst = DataLocation.DataPathFor(baseDir, AppPaths.DataIsEnglish);
            if (string.IsNullOrEmpty(dst)) return;

            if (!FirstRunDialog.CanWrite(baseDir))
            {
                MessageBox.Show(this, L10n.F("这个位置写不进去：\n{0}", baseDir),
                    L10n.T("换个位置"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var existing = 0;
            try { existing = Directory.GetFileSystemEntries(dst).Length; } catch { }
            if (existing > 0)
            {
                MessageBox.Show(this,
                    L10n.F("那个位置上已经有一个数据文件夹了，里面还有 {0} 项内容。\n\n"
                         + "两份台账混在一起会把「已删除」判断搞乱，所以这里不覆盖。\n"
                         + "要么换个空位置，要么先把旧的删掉再来。", existing),
                    L10n.T("这里已经有数据了"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int copied;
            string err, leftover;
            Cursor = Cursors.WaitCursor;
            UseWaitCursor = true;
            Application.DoEvents();
            var ok = DataLocation.MoveDataTo(baseDir, out copied, out err, out leftover);
            UseWaitCursor = false;
            Cursor = Cursors.Default;

            if (!ok)
            {
                MessageBox.Show(this, err, L10n.T("没有搬成"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DataLocation.SwitchTo(baseDir);
            Changed = true;

            var msg = L10n.F("已经把 {0} 个文件搬到：\n{1}", copied, dst);
            if (leftover != null)
                msg += "\n\n" + L10n.F("旧文件夹还有东西被别的程序占着，暂时没删掉：\n{0}\n"
                                     + "已经记下来了，下次启动会先把它删干净。", leftover);
            else
                msg += "\n\n" + L10n.T("旧文件夹已经删掉了，现在只剩这一份，两边的台账不会再打架。");

            MessageBox.Show(this, msg, L10n.T("搬完了"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 删掉数据文件夹（连文件夹本身），并把「数据放在哪」这条记录一起清掉，然后退出程序。
        ///
        /// 为什么必须退出：文件被自己占着就删不干净。1.8 的版本删完之后主窗口又扫了一次，
        /// 扫描第一件事就是写台账，于是文件夹原地重建、只剩一份空台账 ——
        /// 用户看到的「删不干净」就是这么来的。
        /// </summary>
        private void DeleteData()
        {
            var dir = AppPaths.DataDir;
            if (!DataLocation.IsDeletableDataDir(dir))
            {
                MessageBox.Show(this, L10n.F("这个目录看起来不像是本程序的数据文件夹，为安全起见不删：\n{0}", dir),
                    L10n.T("没删"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int files; long bytes;
            DataLocation.Measure(dir, out files, out bytes);

            var pendingDir = string.IsNullOrEmpty(_screenShot)
                ? Path.Combine(AppPaths.ToolRoot, AppPaths.PendingFolderName)
                : Mover.PendingDir(_screenShot);
            int photos = 0;
            try { if (Directory.Exists(pendingDir)) photos = Directory.GetFiles(pendingDir).Length; } catch { }

            var msg = L10n.F("要删掉这个文件夹吗？\n\n{0}\n\n"
                           + "里面有 {1} 个文件，共 {2}。整个文件夹会被删掉 —— "
                           + "台账、你标过的「忽略」、设置、缩略图缓存都会消失。\n\n"
                           + "照片本身不受影响。删完程序会退出；下次启动会重新问你要把数据放在哪。",
                             dir, files, RecordCleaner.Size(bytes));
            if (photos > 0)
                msg += L10n.F("\n\n待清理文件夹里还有 {0} 张照片，那是你的照片，这里不会动它。", photos);

            var r = MessageBox.Show(this, msg, L10n.T("确认删除使用数据"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            string err;
            var gone = DataLocation.TryDeleteDataDir(dir, out err);

            // 位置记录一定要清：留着它，下次启动就会在同一个地方重建一份，
            // 用户从外观上分不出「删过」和「没删过」。
            DataLocation.TryNotRemember();

            if (!gone)
            {
                // 还有文件被占用：先登记，退出后再删。此时把「删除失败」说成失败，
                // 但实际上下次启动就会补掉，所以要把这句话讲清楚。
                DataLocation.QueueRemoval(dir);
                MessageBox.Show(this,
                    L10n.F("还有文件正在被占用，这次没删完：\n{0}\n\n"
                         + "已经记下来了。程序这就退出，下次启动会先把剩下的删干净。", err),
                    L10n.T("没删干净"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show(this,
                    L10n.T("已经删干净了，文件夹本身也没留下。\n\n"
                         + "程序现在退出。下次启动会重新问你要把数据放在哪 —— 这就是删成功的证明。"),
                    L10n.T("删除完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Application.Exit();
        }
    }
}
