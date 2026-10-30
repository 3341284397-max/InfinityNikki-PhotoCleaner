using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NikkiPhotoCleaner
{
    // ============================================================
    //  记录清理：统计与清除工具自身产生的记录文件
    //  原则：只碰使用数据目录与空的待清理文件夹，绝不碰照片
    // ============================================================
    internal class RecordItem
    {
        public string Key;
        public string Title;
        public string Note;        // 右侧说明
        public long Bytes;
        public int Count;
        public bool Risky;         // 危险项（删了有后果）
        public bool DefaultOn;     // 默认是否勾选
        public string WarnText;    // 勾选危险项时的二次确认文案
    }

    internal static class RecordCleaner
    {
        public const string KReport = "report";
        public const string KThumb = "thumb";
        public const string KEmpty = "empty";
        public const string KMove = "move";
        public const string KLedger = "ledger";

        /// <summary>统计当前各类记录的占用情况</summary>
        public static List<RecordItem> Survey(string screenShot)
        {
            var list = new List<RecordItem>();

            // 1. 报告
            var rep = Files(AppPaths.ReportDir, "*.html");
            list.Add(new RecordItem
            {
                Key = KReport,
                Title = L10n.T("网页报告"),
                Note = rep.Count > 3 ? L10n.T("将保留最近 3 份") : L10n.T("可随时重新生成"),
                Bytes = rep.Sum(f => SafeLen(f)),
                Count = rep.Count,
                DefaultOn = rep.Count > 3
            });

            // 2. 缩略图缓存
            var thumb = Files(AppPaths.ThumbDir, "*.jpg");
            list.Add(new RecordItem
            {
                Key = KThumb,
                Title = L10n.T("缩略图缓存"),
                Note = L10n.T("下次生成报告会自动重建"),
                Bytes = thumb.Sum(f => SafeLen(f)),
                Count = thumb.Count,
                DefaultOn = thumb.Count > 0
            });

            // 3. 待清理空文件夹
            var empties = EmptyPendingDirs(screenShot);
            list.Add(new RecordItem
            {
                Key = KEmpty,
                Title = L10n.T("待清理空文件夹"),
                Note = L10n.T("里面已没有照片"),
                Bytes = 0,
                Count = empties.Count,
                DefaultOn = empties.Count > 0
            });

            // 4. 移动记录（危险：删后无法还原）
            var hasMove = File.Exists(AppPaths.LastMovePath);
            list.Add(new RecordItem
            {
                Key = KMove,
                Title = L10n.T("移动记录"),
                Note = hasMove ? L10n.T("删后「移回原目录」失效") : L10n.T("暂无"),
                Bytes = hasMove ? SafeLen(AppPaths.LastMovePath) : 0,
                Count = hasMove ? 1 : 0,
                Risky = true,
                DefaultOn = false,
                WarnText = L10n.T("删除后，「移回原目录」将无法再把照片移回原位。\n如果照片已经删掉了，这一项可以安全清理；如果还想保留还原能力，请先不要清。\n\n建议先打开该文件夹确认：如果待确认文件夹已经空了，说明照片已被你删除，记录就没用了。")
            });

            // 5. 判定台账（危险：删后判定重置）
            var hasLedger = File.Exists(AppPaths.LedgerPath);
            list.Add(new RecordItem
            {
                Key = KLedger,
                Title = L10n.T("判定台账"),
                Note = hasLedger ? L10n.T("删后判定全部重置为「疑似」") : L10n.T("暂无"),
                Bytes = hasLedger ? SafeLen(AppPaths.LedgerPath) : 0,
                Count = hasLedger ? 1 : 0,
                Risky = true,
                DefaultOn = false,
                WarnText = L10n.T("台账记录着哪些照片「曾经出现在相册里」，是判断照片是否被游戏内删除的唯一依据。\n\n删除后，所有照片都会变成「疑似」，工具需要重新积累记录才能再次准确判断。\n除非你想完全重置，否则请不要清理这一项。")
            });

            return list;
        }

        /// <summary>执行清理，返回一句话结果</summary>
        public static string Clean(List<RecordItem> picked, string screenShot)
        {
            var done = new List<string>();
            var freed = 0L;
            int errs = 0;

            foreach (var it in picked)
            {
                if (it.Count == 0) continue;

                switch (it.Key)
                {
                    case KReport:
                    {
                        var files = Files(AppPaths.ReportDir, "*.html")
                                    .OrderByDescending(f => SafeTime(f)).ToList();
                        var kill = files.Skip(3).ToList();
                        foreach (var f in kill)
                        {
                            try { freed += SafeLen(f); File.Delete(f); } catch { errs++; }
                        }
                        done.Add(L10n.F("报告 {0} 份", kill.Count));
                        break;
                    }

                    case KThumb:
                    {
                        int n = 0;
                        foreach (var f in Files(AppPaths.ThumbDir, "*.jpg"))
                        {
                            try { freed += SafeLen(f); File.Delete(f); n++; } catch { errs++; }
                        }
                        done.Add(L10n.F("缩略图 {0} 个", n));
                        break;
                    }

                    case KEmpty:
                    {
                        int n = 0;
                        foreach (var d in EmptyPendingDirs(screenShot))
                        {
                            // false = 只删空目录；万一这会儿又进了照片，会抛异常被跳过，绝不误删
                            try { Directory.Delete(d, false); n++; } catch { errs++; }
                        }
                        done.Add(L10n.F("空文件夹 {0} 个", n));
                        break;
                    }

                    case KMove:
                    {
                        try { freed += SafeLen(AppPaths.LastMovePath); File.Delete(AppPaths.LastMovePath); done.Add(L10n.T("移动记录")); }
                        catch { errs++; }
                        break;
                    }

                    case KLedger:
                    {
                        try
                        {
                            freed += SafeLen(AppPaths.LedgerPath);
                            File.Delete(AppPaths.LedgerPath);
                            done.Add(L10n.T("判定台账"));
                        }
                        catch { errs++; }
                        break;
                    }
                }
            }

            if (done.Count == 0) return L10n.T("没有需要清理的内容。");

            var msg = L10n.T("已清理：") + string.Join(L10n.T("、"), done);
            if (freed > 0) msg += "\n" + L10n.T("释放空间：") + Size(freed);
            if (errs > 0) msg += "\n" + L10n.F("有 {0} 个文件被占用或权限不足，未能删除。", errs);
            return msg;
        }

        // ---------------- 辅助 ----------------

        private static List<string> Files(string dir, string pattern)
        {
            var r = new List<string>();
            try
            {
                if (!Directory.Exists(dir)) return r;
                r.AddRange(Directory.GetFiles(dir, pattern));
            }
            catch { }
            return r;
        }

        /// <summary>待清理文件夹，以及旧版本遗留的 _待确认删除_* 空文件夹</summary>
        private static List<string> EmptyPendingDirs(string screenShot)
        {
            var r = new List<string>();
            if (string.IsNullOrEmpty(screenShot) || !Directory.Exists(screenShot)) return r;
            try
            {
                var candidates = new List<DirectoryInfo>();
                var cur = new DirectoryInfo(Mover.PendingDir(screenShot));
                if (cur.Exists) candidates.Add(cur);
                candidates.AddRange(new DirectoryInfo(screenShot).GetDirectories("_待确认删除_*"));

                foreach (var d in candidates)
                {
                    try
                    {
                        if (d.GetFileSystemInfos().Length == 0) r.Add(d.FullName);
                    }
                    catch { }
                }
            }
            catch { }
            return r;
        }

        private static long SafeLen(string p)
        {
            try { var f = new FileInfo(p); return f.Exists ? f.Length : 0; }
            catch { return 0; }
        }

        private static DateTime SafeTime(string p)
        {
            try { return File.GetLastWriteTime(p); } catch { return DateTime.MinValue; }
        }

        public static string Size(long b)
        {
            if (b >= 1024 * 1024 * 1024) return string.Format("{0:N2} GB", b / 1024.0 / 1024.0 / 1024.0);
            if (b >= 1024 * 1024) return string.Format("{0:N1} MB", b / 1024.0 / 1024.0);
            if (b >= 1024) return string.Format("{0:N0} KB", b / 1024.0);
            return b + " B";
        }

        /// <summary>弹出清理窗口；返回是否执行了清理</summary>
        public static bool ShowDialog(IWin32Window owner, string screenShot)
        {
            using (var dlg = new CleanDialog(screenShot))
                return dlg.ShowDialog(owner) == DialogResult.OK;
        }
    }

    // ============================================================
    //  清理窗口
    // ============================================================
    internal class CleanDialog : Form
    {
        private readonly string _screenShot;
        private readonly List<RecordItem> _items;
        private readonly Dictionary<string, RoundCheck> _checks = new Dictionary<string, RoundCheck>();
        private readonly float _scale;

        private Label _lblSum;
        private SoftButton _btnOk;
        private SoftButton _btnCancel;
        private Panel _foot;

        private int S(int v) { return (int)Math.Round(v * _scale); }

        public CleanDialog(string screenShot)
        {
            _screenShot = screenShot;
            _items = RecordCleaner.Survey(screenShot);
            _scale = DeviceDpi / 96f;
            Text = L10n.T("清理工具记录");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            try { Icon = Theme.WindowIcon; } catch { }

            var clientW = S(L10n.IsEnglish ? 660 : 600);
            var rowH = S(48);
            var footH = S(96);

            // 说明文字的行数中英文不一样、不同 DPI 下也不一样，写死高度必然在某一种情况下裁掉一行。
            // 这里按实际排版量出来的高度算，多留一行也不会挤。
            var subText = L10n.T("这些文件只用于记录判定依据和还原信息，清理它们不会影响游戏，也不会动到任何照片。");
            var subH = TextRenderer.MeasureText(subText, Theme.F(8.6f),
                new Size(clientW - S(48), 0), TextFormatFlags.WordBreak).Height;
            var headH = S(18) + S(30) + S(6) + subH + S(6);

            // 多留 S(20) 给卡片内边距（上下各 S(8)），否则最后一行会被裁掉两个像素
            ClientSize = new Size(clientW, headH + _items.Count * rowH + footH + S(20));

            Build(headH, rowH, footH);
            RefreshSum();
        }

        /// <summary>对话框也要染标题栏，否则主窗口和对话框一个窗口两种底色。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.TintCaption(this);
        }

        private void Build(int headH, int rowH, int footH)
        {
            // ---- 顶部说明 ----
            var head = new Panel
            {
                Dock = DockStyle.Top,
                Height = headH,
                BackColor = Theme.Bg,
                Padding = new Padding(S(24), S(18), S(24), S(4))
            };
            // Dock 的分配顺序由 z-order 决定：后添加的先占位。
            // 所以必须先加 Fill 的说明，再加 Top 的标题 —— 反过来的话 Fill 会铺满整个面板，
            // 标题再压上去，正好盖住说明的第一行。
            head.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = L10n.T("这些文件只用于记录判定依据和还原信息，清理它们不会影响游戏，也不会动到任何照片。"),
                Padding = new Padding(0, S(6), 0, 0),
                Font = Theme.F(8.6f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent
            });
            head.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = S(30),
                Text = L10n.T("清理工具自己产生的记录"),
                Font = Theme.F(13f, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent
            });

            // ---- 列表：整体装进一张圆角白卡 ----
            var bodyCard = new CardPanel
            {
                Dock = DockStyle.Fill,
                Inset = 0,
                Radius = Theme.RCard,
                PadX = S(16),
                PadY = S(8)
            };
            bodyCard.ApplyPadding();
            bodyCard.Margin = new Padding(S(24), 0, S(24), 0);

            var bodyWrap = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(S(24), 0, S(24), S(6)) };
            bodyCard.Dock = DockStyle.Fill;

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = _items.Count,
                BackColor = Theme.Card
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(L10n.IsEnglish ? 214 : 176)));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(92)));

            for (var i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                body.RowStyles.Add(new RowStyle(SizeType.Absolute, rowH));

                var chk = new RoundCheck
                {
                    Text = it.Title,
                    Checked = it.DefaultOn,
                    Dock = DockStyle.Fill,
                    Margin = Padding.Empty,
                    Font = Theme.F(9f)
                };
                chk.CheckedChanged += (s, e) => RefreshSum();
                _checks[it.Key] = chk;

                var note = new Label
                {
                    Text = it.Note,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = Theme.F(8.5f),
                    ForeColor = it.Risky ? Theme.AmberDeep : Theme.Text3,
                    BackColor = Color.Transparent,
                    Margin = Padding.Empty
                };

                var size = new Label
                {
                    Text = it.Count == 0 ? "—" : (it.Bytes > 0 ? RecordCleaner.Size(it.Bytes) : L10n.F("{0} 个", it.Count)),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = Theme.F(8.5f),
                    ForeColor = it.Risky ? Theme.AmberDeep : Theme.Text2,
                    BackColor = Color.Transparent,
                    Margin = Padding.Empty
                };

                body.Controls.Add(chk, 0, i);
                body.Controls.Add(note, 1, i);
                body.Controls.Add(size, 2, i);
            }

            bodyCard.Controls.Add(body);
            bodyWrap.Controls.Add(bodyCard);

            // ---- 底部 ----
            _foot = new Panel { Dock = DockStyle.Bottom, Height = footH, BackColor = Theme.Bg, Padding = new Padding(S(24), 0, S(24), 0) };
            _foot.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Line, 1f))
                    e.Graphics.DrawLine(pen, S(24), 0, _foot.Width - S(24), 0);
            };

            _lblSum = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.8f),
                ForeColor = Theme.Text2,
                BackColor = Color.Transparent
            };

            _btnOk = new SoftButton(L10n.T("清理所选"), BtnKind.Danger, S(104));
            _btnOk.Click += (s, e) => DoClean();

            _btnCancel = new SoftButton(L10n.T("取消"), BtnKind.Ghost, S(84));
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            // SoftButton 不是 IButtonControl，接不上 Form.CancelButton，Esc 自己接一下
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };

            _foot.Controls.Add(_lblSum);
            _foot.Controls.Add(_btnOk);
            _foot.Controls.Add(_btnCancel);
            _foot.Resize += (s, e) => LayoutFoot();

            Controls.Add(bodyWrap);
            Controls.Add(_foot);
            Controls.Add(head);

            LayoutFoot();
        }

        /// <summary>底栏：说明靠左，两个按钮从右往左贴边（和主窗口一致的手工排法）。</summary>
        private void LayoutFoot()
        {
            if (_foot == null || _btnOk == null) return;
            var gap = S(8);
            var y = S(30);
            var btnH = S(34);

            var right = _foot.Width - S(24);
            _btnOk.SetBounds(right - _btnOk.Width, y, _btnOk.Width, btnH);
            right -= _btnOk.Width + gap;
            if (_btnCancel != null)
            {
                _btnCancel.SetBounds(right - _btnCancel.Width, y, _btnCancel.Width, btnH);
                right -= _btnCancel.Width + gap;
            }

            _lblSum.SetBounds(S(24), S(6), Math.Max(S(80), right - S(24)), S(28));
        }

        private void RefreshSum()
        {
            var picked = Picked();
            long bytes = picked.Sum(i => i.Bytes);
            int cnt = picked.Count(i => i.Count > 0);

            if (cnt == 0)
            {
                _lblSum.Text = L10n.T("未选择任何项目。");
                _lblSum.ForeColor = Theme.Text3;
                _btnOk.Enabled = false;
                return;
            }

            _btnOk.Enabled = true;
            _lblSum.ForeColor = Theme.Text2;
            _lblSum.Text = L10n.F("已选 {0} 项", cnt) +
                           (bytes > 0 ? L10n.F("，可释放约 {0}", RecordCleaner.Size(bytes))
                                      : L10n.T("（仅删除文件夹，不占空间）"));
        }

        private List<RecordItem> Picked()
        {
            return _items.Where(i => _checks[i.Key].Checked && i.Count > 0).ToList();
        }

        private void DoClean()
        {
            var picked = Picked();
            if (picked.Count == 0) return;

            // 危险项二次确认
            foreach (var it in picked.Where(i => i.Risky))
            {
                var r = MessageBox.Show(this, it.WarnText, L10n.F("确认清理「{0}」", it.Title),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (r != DialogResult.OK) return;
            }

            var msg = RecordCleaner.Clean(picked, _screenShot);
            MessageBox.Show(this, msg, L10n.T("清理完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
