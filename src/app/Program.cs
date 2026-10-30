using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NikkiPhotoCleaner
{
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int pid);

        [STAThread]
        private static void Main()
        {
            // 隐藏自检入口：--selftest <截图目录> [clean]
            // 供开发核对「记录统计 / 清理」逻辑，不影响正常双击使用
            var argv = Environment.GetCommandLineArgs();

            // 界面语言：--lang zh|en > 设置文件 Language= > 系统语言
            L10n.Init(ArgValue(argv, "--lang"));

            if (argv.Length >= 3 && argv[1] == "--selftest")
            {
                RunSelfTest(argv[2], argv.Length >= 4 && argv[3] == "clean");
                return;
            }
            if (argv.Length >= 3 && argv[1] == "--testmove")
            {
                RunMoveTest(argv[2]);
                return;
            }
            if (argv.Length >= 3 && argv[1] == "--shot")
            {
                RunShot(argv[2], argv.Length >= 4 && argv[3] == "demo");
                return;
            }
            if (argv.Length >= 4 && argv[1] == "--shotdlg")
            {
                RunDialogShot(argv[2], argv[3]);
                return;
            }
            if (argv.Length >= 3 && argv[1] == "--shotreal")
            {
                RunRealShot(argv[2], argv.Length >= 4 && argv[3] == "demo");
                return;
            }
            if (argv.Length >= 4 && argv[1] == "--shotdlgreal")
            {
                RunDialogRealShot(argv[2], argv[3]);
                return;
            }
            if (argv.Length >= 3 && argv[1] == "--shotall")
            {
                RunSelectAllShot(argv[2], argv.Length >= 4 && argv[3] == "demo");
                return;
            }
            if (argv.Length >= 3 && argv[1] == "--shotmove")
            {
                int n;
                if (!int.TryParse(argv.Length >= 4 ? argv[3] : "40", out n) || n <= 0) n = 40;
                RunMoveDialogShot(argv[2], n);
                return;
            }

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static string ArgValue(string[] argv, string name)
        {
            for (var i = 1; i < argv.Length - 1; i++)
                if (string.Equals(argv[i], name, StringComparison.OrdinalIgnoreCase))
                    return argv[i + 1];
            return null;
        }

        /// <summary>
        /// 隐藏入口：--shot 输出.png [demo]
        /// 把窗口画成 PNG，用来核对界面改动。demo 会灌 12 条假数据，不开游戏也能看效果。
        /// </summary>
        internal static void RunShot(string path, bool demo)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var f = new MainForm())
            {
                f.Headless = true;
                f.DemoMode = demo;
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-6000, 0);
                f.Show();
                Application.DoEvents();
                System.Threading.Thread.Sleep(300);
                Application.DoEvents();

                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// 隐藏入口：--shotall 输出.png [demo]
        /// 打开窗口后先按一次「全选」，再截图 —— 用来确认全选按钮、计数文字与勾选圈同时到位。
        /// </summary>
        internal static void RunSelectAllShot(string path, bool demo)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var f = new MainForm())
            {
                f.Headless = true;
                f.DemoMode = demo;
                f.StartPosition = FormStartPosition.Manual;

                var wa = Screen.PrimaryScreen.WorkingArea;
                f.Location = new Point(
                    wa.Left + Math.Max(0, (wa.Width - f.Width) / 2),
                    wa.Top + Math.Max(0, (wa.Height - f.Height) / 2));
                f.TopMost = true;
                f.Show();

                for (var i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }

                f.SelectAllForTest();
                for (var i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }

                var box = FrameBounds(f);
                using (var bmp = new Bitmap(box.Width, box.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(box.Left, box.Top, 0, 0, box.Size);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// 隐藏入口：--shotreal 输出.png [demo]
        /// 真实屏幕截取。DrawToBitmap 走 WM_PRINT，拿不到 DWM 绘制的标题栏，
        /// 而标题栏染色恰恰只能在 DWM 合成后才看得见 —— 核对配色时用这个。
        /// </summary>
        internal static void RunRealShot(string path, bool demo)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var f = new MainForm())
            {
                f.Headless = true;
                f.DemoMode = demo;
                f.StartPosition = FormStartPosition.Manual;

                var wa = Screen.PrimaryScreen.WorkingArea;
                f.Location = new Point(
                    wa.Left + Math.Max(0, (wa.Width - f.Width) / 2),
                    wa.Top + Math.Max(0, (wa.Height - f.Height) / 2));
                f.TopMost = true;
                f.Show();

                // 等 DWM 完成合成，标题栏染色才会体现出来
                for (var i = 0; i < 16; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(45);
                }

                // Form.Left/Top 含 DWM 投影用的不可见边框（约 8px），直接从那里截
                // 会把桌面壁纸带进来。取扩展边框边界才是肉眼可见的范围。
                var box = FrameBounds(f);

                using (var bmp = new Bitmap(box.Width, box.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(box.Left, box.Top, 0, 0, box.Size);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct WinRect
        {
            public int Left, Top, Right, Bottom;
        }
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out WinRect value, int size);

        /// <summary>窗口肉眼可见的边界（不含投影预留的透明边框）。</summary>
        private static Rectangle FrameBounds(Form f)
        {
            try
            {
                WinRect r;
                const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
                if (DwmGetWindowAttribute(f.Handle, DWMWA_EXTENDED_FRAME_BOUNDS, out r,
                        System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinRect))) == 0)
                {
                    var box = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    // 扩展边框在部分 DPI 下会比实际可见区域大 1~2px，
                    // 不往内收就会把桌面或下层窗口带进底边。
                    box.Inflate(-1, -1);
                    if (box.Width > 100 && box.Height > 100) return box;
                }
            }
            catch { }
            return new Rectangle(f.Left, f.Top, f.Width, f.Height);
        }

        /// <summary>
        /// 隐藏入口：--shotdlgreal 输出.png 目录
        /// 对话框的真实截屏。对话框同样染了标题栏，得走 DWM 才看得见效果。
        /// </summary>
        internal static void RunDialogRealShot(string path, string screenShot)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var f = new CleanDialog(screenShot))
            {
                f.StartPosition = FormStartPosition.Manual;
                var wa = Screen.PrimaryScreen.WorkingArea;
                f.Location = new Point(
                    wa.Left + Math.Max(0, (wa.Width - f.Width) / 2),
                    wa.Top + Math.Max(0, (wa.Height - f.Height) / 2));
                f.TopMost = true;
                f.Show();

                for (var i = 0; i < 16; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(45);
                }

                var box = FrameBounds(f);
                using (var bmp = new Bitmap(box.Width, box.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(box.Left, box.Top, 0, 0, box.Size);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// 隐藏入口：--shotmove 输出.png [张数]
        /// 把「确认移动」对话框渲染成 PNG，用来核对滚动列表与各种张数下的高度。
        /// </summary>
        internal static void RunMoveDialogShot(string path, int count)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var t0 = new DateTime(2026, 10, 20, 21, 24, 47);
            var items = new List<PhotoItem>();
            for (var i = 0; i < count; i++)
            {
                var t = t0.AddMinutes(-37 * i);
                items.Add(new PhotoItem
                {
                    Name = t.ToString("yyyy_MM_dd_HH_mm_ss") + "_" + (2143244 - i * 7),
                    FullPath = @"D:\Games\X6Game\ScreenShot\" + t.ToString("yyyy_MM_dd_HH_mm_ss") + ".jpeg",
                    Size = 1180000 + i * 41000L,
                    Time = t,
                    Status = (i % 6 == 5) ? StatusText.Suspect : StatusText.Deleted,
                    MinAgeDays = 2
                });
            }

            using (var f = new MoveDialog(items, @"D:\Games\X6Game\ScreenShot\无限暖暖清理助手_待清理照片", 3, 2))
            {
                f.StartPosition = FormStartPosition.Manual;
                var wa = Screen.PrimaryScreen.WorkingArea;
                f.Location = new Point(
                    wa.Left + Math.Max(0, (wa.Width - f.Width) / 2),
                    wa.Top + Math.Max(0, (wa.Height - f.Height) / 2));
                f.TopMost = true;
                f.Show();

                for (var i = 0; i < 16; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(45);
                }

                var box = FrameBounds(f);
                using (var bmp = new Bitmap(box.Width, box.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(box.Left, box.Top, 0, 0, box.Size);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        /// <summary>隐藏入口：--shotdlg 输出.png 目录 —— 把「清理记录」对话框渲染成 PNG。</summary>
        internal static void RunDialogShot(string path, string screenShot)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var f = new CleanDialog(screenShot))
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-6000, 0);
                f.Show();
                Application.DoEvents();
                System.Threading.Thread.Sleep(200);
                Application.DoEvents();

                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }

        private static void RunSelfTest(string screenShot, bool doClean)
        {
            try { AttachConsole(-1); } catch { }
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }

            var sb = new StringBuilder();
            var items = RecordCleaner.Survey(screenShot);

            sb.AppendLine("=== 记录统计 ===");
            foreach (var i in items)
                sb.AppendLine(string.Format("{0,-8} 数量={1,-4} 大小={2,-10} 危险={3,-6} 默认={4,-6} {5}",
                    i.Key, i.Count, RecordCleaner.Size(i.Bytes), i.Risky, i.DefaultOn, i.Note));

            if (doClean)
            {
                var picked = items.Where(i => !i.Risky && i.Count > 0).ToList();
                sb.AppendLine();
                sb.AppendLine("=== 清理安全项 ===");
                sb.AppendLine(picked.Count == 0 ? "无可清理项" : RecordCleaner.Clean(picked, screenShot));
            }

            var text = sb.ToString();
            Console.WriteLine(text);

            // 同时落盘，便于重定向捕获（WinExe 无控制台时也能验证）
            try
            {
                AppPaths.EnsureDirs();
                File.WriteAllText(Path.Combine(AppPaths.DataDir, "_selftest.txt"), text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Console.WriteLine("写自检文件失败：" + ex.GetType().Name + " " + ex.Message);
            }
        }

        /// <summary>移动 / 重名 / 还原 的端到端自检</summary>
        internal static string RunMoveTest(string screenShot)
        {
            try { AttachConsole(-1); } catch { }
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }

            var sb = new StringBuilder();
            try
            {
                var res = Scanner.Run(screenShot, 0);
                sb.AppendLine("=== 扫描 ===");
                foreach (var p in res.Items) sb.AppendLine("  " + p.Status.PadRight(6) + p.Name);
                sb.AppendLine("  闸门=" + (res.Blockers.Count == 0 ? "无" : string.Join(" / ", res.Blockers)));
                sb.AppendLine("  提示=" + (res.Hints.Count == 0 ? "无" : string.Join(" / ", res.Hints)));

                var target = res.Items.Where(p => p.Status == StatusText.Deleted).ToList();
                sb.AppendLine("  可移动(已删除)=" + target.Count);

                if (target.Count == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("  没有「已删除」项，本轮只建立台账。请让照片从相册缓存消失后再跑一次。");
                }
                else
                {

                sb.AppendLine();
                sb.AppendLine("=== 第一轮移动 ===");
                var r1 = Mover.Move(target, screenShot);
                sb.AppendLine("  Ok=" + r1.Ok + " 移入=" + r1.Moved.Count + " 失败=" + r1.Failed.Count + " 改名=" + r1.RenamedCount);
                foreach (var f in r1.Failed) sb.AppendLine("    ! " + f);
                sb.AppendLine("  目录=" + r1.PendingDir);
                sb.AppendLine("  目录内文件=" + (Directory.Exists(r1.PendingDir) ? Directory.GetFiles(r1.PendingDir).Length : -1));
                sb.AppendLine("  ScreenShot 剩余=" + Directory.GetFiles(screenShot, "*.jpeg").Length);

                sb.AppendLine();
                sb.AppendLine("=== 第二轮：同名冲突 ===");
                if (r1.Pairs.Count == 0)
                {
                    sb.AppendLine("  第一轮没有成功移动，跳过");
                }
                else
                {
                    var back = Path.Combine(screenShot, r1.Pairs[0].Value);
                    File.Copy(Path.Combine(r1.PendingDir, r1.Pairs[0].Key), back, true);
                    var res2 = Scanner.Run(screenShot, 0);
                    var t2 = res2.Items.Where(p => p.Status == StatusText.Deleted).ToList();
                    var r2 = Mover.Move(t2, screenShot);
                    sb.AppendLine("  移入=" + r2.Moved.Count + " 改名=" + r2.RenamedCount + " 失败=" + r2.Failed.Count);
                    foreach (var kv in r2.Pairs) sb.AppendLine("    " + kv.Value + "  ->  " + kv.Key);
                    sb.AppendLine("  目录内文件=" + Directory.GetFiles(r2.PendingDir).Length + "（应为 2，未被覆盖）");
                }

                sb.AppendLine();
                sb.AppendLine("=== 记录内容 ===");
                if (File.Exists(AppPaths.LastMovePath))
                    foreach (var line in File.ReadAllLines(AppPaths.LastMovePath))
                        sb.AppendLine("  " + line.Replace("\t", " ¦ "));

                sb.AppendLine();
                sb.AppendLine("=== 还原 ===");
                var rec = Mover.ReadRecord();
                sb.AppendLine("  记录条数=" + rec.Count + " 源=" + rec.Source);
                int failed;
                int ok = Mover.Restore(rec, out failed);
                sb.AppendLine("  还原=" + ok + " 失败=" + failed);
                sb.AppendLine("  ScreenShot 现有 jpeg=" + Directory.GetFiles(screenShot, "*.jpeg").Length);
                sb.AppendLine("  待清理目录还在=" + Directory.Exists(r1.PendingDir));

                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("异常：" + ex);
            }

            var text = sb.ToString();
            try { Console.WriteLine(text); } catch { }
            try
            {
                AppPaths.EnsureDirs();
                File.WriteAllText(Path.Combine(AppPaths.DataDir, "_testmove.txt"), text, new UTF8Encoding(false));
            }
            catch { }
            return text;
        }
    }

    // ============================================================
    //  路径与常量
    // ============================================================
    internal static class AppPaths
    {
        public const string DataFolderZh = "无限暖暖清理助手_使用数据";
        public const string DataFolderEn = "NikkiCleaner_Data";

        public const string PendingZh = "无限暖暖清理助手_待清理照片";
        public const string PendingEn = "NikkiCleaner_ToDelete";

        public static readonly string ToolRoot = ResolveRoot();

        /// <summary>
        /// 数据目录用英文名还是中文名。只看「已经存在哪一个」，不看当前界面语言：
        /// 目录一旦建好就锁定，之后切换语言不会换目录，台账和还原记录也就不会断。
        /// 两个都存在时中文优先（旧版本用户升级上来的情况）。
        /// </summary>
        public static readonly bool DataIsEnglish = ResolveDataIsEnglish();

        private static bool ResolveDataIsEnglish()
        {
            // 1) 已经存在的目录优先。这一步是「切换语言不会换目录」的根本保证。
            try
            {
                if (Directory.Exists(Path.Combine(ToolRoot, DataFolderZh))) return false;
                if (Directory.Exists(Path.Combine(ToolRoot, DataFolderEn))) return true;
            }
            catch { }

            // 2) 全新环境：命令行 --lang 显式指定优先，其次跟随系统语言
            try
            {
                var argv = Environment.GetCommandLineArgs();
                for (var i = 1; i < argv.Length - 1; i++)
                {
                    if (string.Equals(argv[i], "--lang", StringComparison.OrdinalIgnoreCase))
                        return argv[i + 1].Trim().ToLowerInvariant().StartsWith("en");
                }
            }
            catch { }

            try
            {
                return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "zh";
            }
            catch { return false; }
        }

        /// <summary>
        /// 正常情况取 exe 所在目录；设置环境变量 NIKKI_TOOL_ROOT 可指向别处，
        /// 便于在隔离目录里跑回归测试而不污染真实数据。
        /// </summary>
        private static string ResolveRoot()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("NIKKI_TOOL_ROOT");
                if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
                    return env.TrimEnd('\\', '/');
            }
            catch { }
            return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        }

        public static readonly string DataDir = Path.Combine(ToolRoot, DataIsEnglish ? DataFolderEn : DataFolderZh);

        // 子项名字跟着数据目录走，不跟着界面语言走：保证切换语言后仍在读写同一批文件
        public static readonly string ThumbDir = Path.Combine(DataDir, DataIsEnglish ? "thumbnails" : "缩略图缓存");
        public static readonly string ReportDir = Path.Combine(DataDir, DataIsEnglish ? "reports" : "网页报告");
        public static readonly string LedgerPath = Path.Combine(DataDir, DataIsEnglish ? "album-ledger.tsv" : "相册台账.tsv");
        public static readonly string ConfigPath = Path.Combine(DataDir, DataIsEnglish ? "settings.txt" : "设置.txt");
        public static readonly string LastMovePath = Path.Combine(DataDir, DataIsEnglish ? "last-move.txt" : "最近一次移动记录.txt");

        /// <summary>待清理文件夹名。已存在哪个用哪个，避免切换语言后「看不见」之前归拢的照片。</summary>
        public static string PendingFolderName
        {
            get { return L10n.IsEnglish ? PendingEn : PendingZh; }
        }

        public static void EnsureDirs()
        {
            MigrateLegacy();
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(ThumbDir);
            Directory.CreateDirectory(ReportDir);
        }

        /// <summary>
        /// 旧版本的 _数据\ 目录自动搬家，保住已有的台账历史。
        /// 只在新目录尚不存在时执行一次，失败不影响启动。
        /// </summary>
        private static void MigrateLegacy()
        {
            try
            {
                var old = Path.Combine(ToolRoot, "_数据");
                if (!Directory.Exists(old) || Directory.Exists(DataDir)) return;

                Directory.CreateDirectory(DataDir);

                CopyIfExists(Path.Combine(old, "台账.tsv"), LedgerPath);
                CopyIfExists(Path.Combine(old, "配置.txt"), ConfigPath);
                CopyIfExists(Path.Combine(old, "最近一次移动.txt"), LastMovePath);

                CopyDir(Path.Combine(old, "报告"), ReportDir);
                CopyDir(Path.Combine(old, "缩略图"), ThumbDir);

                // 原目录整体改名留作备份，便于用户回退
                try { Directory.Move(old, Path.Combine(ToolRoot, "_旧数据_可删除")); } catch { }
            }
            catch { }
        }

        private static void CopyIfExists(string from, string to)
        {
            try { if (File.Exists(from) && !File.Exists(to)) File.Copy(from, to, false); } catch { }
        }

        private static void CopyDir(string from, string to)
        {
            try
            {
                if (!Directory.Exists(from)) return;
                Directory.CreateDirectory(to);
                foreach (var f in Directory.GetFiles(from))
                {
                    var t = Path.Combine(to, Path.GetFileName(f));
                    if (!File.Exists(t)) File.Copy(f, t, false);
                }
            }
            catch { }
        }
    }

    internal static class StatusText
    {
        // 内部码与界面语言无关：比较逻辑用它，显示时才翻译。
        // 这样切换语言不需要重新扫描，台账和记录里也不会混进某种语言的文本。
        public const string InAlbum = "InAlbum";
        public const string Deleted = "Deleted";
        public const string Suspect = "Suspect";
        public const string TooNew  = "TooNew";

        public static string Name(string s)
        {
            switch (s)
            {
                case InAlbum: return L10n.T("在册");
                case Deleted: return L10n.T("已删除");
                case Suspect: return L10n.T("疑似");
                case TooNew:  return L10n.T("太新");
                default:      return s;
            }
        }

        /// <summary>判定理由。由状态派生，切换语言后重新取一次即可变成英文。</summary>
        public static string Reason(string status, int minAgeDays)
        {
            switch (status)
            {
                case InAlbum: return L10n.T("游戏相册中仍存在");
                case Deleted: return L10n.T("曾出现在相册中，现已从相册缓存消失");
                case TooNew:  return L10n.F("拍摄不足 {0} 天，可能还没同步到相册；要清理请手动勾选", minAgeDays);
                default:      return L10n.T("相册缓存中从未见过（可能删过，也可能尚未同步）");
            }
        }

        public static int Order(string s)
        {
            switch (s)
            {
                case Deleted: return 0;
                case Suspect: return 1;
                case TooNew:  return 2;
                default:      return 3;
            }
        }

        public static Color ForeColor(string s)
        {
            // 与网页版徽章同一套柔和色，避免原来的高饱和红/橙在米白底上刺眼
            switch (s)
            {
                case Deleted: return Theme.ClayDark;
                case Suspect: return Theme.AmberDeep;
                case InAlbum: return Theme.GreenDark;
                default:      return Theme.Text3;
            }
        }
    }

    // ============================================================
    //  数据模型
    // ============================================================
    internal class PhotoItem
    {
        public string Name;
        public string FullPath;
        public long Size;
        public DateTime Time;
        public double AgeDays;
        public string Status;
        /// <summary>扫描时的「太新」阈值，用于按当前语言生成判定理由</summary>
        public int MinAgeDays;
    }

    internal class LedgerEntry
    {
        public string FirstSeen;
        public string LastSeen;
        public bool EverInAlbum;
    }

    // ============================================================
    //  台账
    // ============================================================
    internal class Ledger
    {
        public readonly Dictionary<string, LedgerEntry> Map =
            new Dictionary<string, LedgerEntry>(StringComparer.OrdinalIgnoreCase);

        public bool Exists { get { return File.Exists(AppPaths.LedgerPath); } }

        public static Ledger Load()
        {
            var l = new Ledger();
            if (!File.Exists(AppPaths.LedgerPath)) return l;
            try
            {
                foreach (var line in File.ReadAllLines(AppPaths.LedgerPath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var f = line.Split('\t');
                    if (f.Length < 4) continue;
                    l.Map[f[0]] = new LedgerEntry
                    {
                        FirstSeen = f[1],
                        LastSeen = f[2],
                        EverInAlbum = f[3] == "1"
                    };
                }
            }
            catch { }
            return l;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 文件名\t首次进入相册\t最后见到\t是否在册");
            foreach (var k in Map.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var v = Map[k];
                sb.AppendLine(string.Join("\t", new[]
                {
                    k, v.FirstSeen, v.LastSeen, v.EverInAlbum ? "1" : "0"
                }));
            }
            // 目录可能还没建（例如被外部清理掉），这里自保一次
            var dir = Path.GetDirectoryName(AppPaths.LedgerPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(AppPaths.LedgerPath, sb.ToString(), new UTF8Encoding(false));
        }
    }

    // ============================================================
    //  目录定位
    // ============================================================
    internal static class GameLocator
    {
        public static bool IsScreenShotDir(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path)) return false;
            var x6 = Directory.GetParent(path.TrimEnd('\\', '/'));
            if (x6 == null || !string.Equals(x6.Name, "X6Game", StringComparison.OrdinalIgnoreCase)) return false;
            return Directory.Exists(Path.Combine(x6.FullName, "Saved", "GamePlayPhotos"));
        }

        public static string FromConfig()
        {
            return Config.GameDir;
        }

        public static void Save(string path)
        {
            Config.SaveGameDir(path);
        }

        public static string Auto()
        {
            var cfg = FromConfig();
            if (cfg != null) return cfg;

            var root = AppPaths.ToolRoot;
            foreach (var c in new[]
            {
                root,
                Path.Combine(root, "ScreenShot"),
                Path.Combine(root, "X6Game", "ScreenShot")
            })
            {
                if (IsScreenShotDir(c)) return c;
            }

            var sw = Stopwatch.StartNew();
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
            {
                if (sw.Elapsed.TotalSeconds > 12) break;
                DirectoryInfo[] lv1;
                try { lv1 = new DirectoryInfo(drive.RootDirectory.FullName).GetDirectories(); }
                catch { continue; }

                foreach (var d1 in lv1)
                {
                    if (sw.Elapsed.TotalSeconds > 12) break;
                    foreach (var suffix in new[] { "X6Game\\ScreenShot", "InfinityNikki\\X6Game\\ScreenShot" })
                    {
                        var cand = Path.Combine(d1.FullName, suffix);
                        if (IsScreenShotDir(cand)) return cand;
                    }
                    DirectoryInfo[] lv2;
                    try { lv2 = d1.GetDirectories(); } catch { continue; }
                    foreach (var d2 in lv2)
                    {
                        if (sw.Elapsed.TotalSeconds > 12) break;
                        foreach (var suffix in new[] { "X6Game\\ScreenShot", "InfinityNikki\\X6Game\\ScreenShot" })
                        {
                            var cand = Path.Combine(d2.FullName, suffix);
                            if (IsScreenShotDir(cand)) return cand;
                        }
                    }
                }
            }
            return null;
        }

        public static string AskUser(IWin32Window owner)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = L10n.T("请选择：无限暖暖\\X6Game\\ScreenShot 文件夹");
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                if (IsScreenShotDir(dlg.SelectedPath)) return dlg.SelectedPath;
                MessageBox.Show(owner, L10n.T("所选目录看起来不对。\n它的上一级文件夹名必须是 X6Game。"),
                    L10n.T("目录无效"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }
    }

    // ============================================================
    //  扫描与判定
    // ============================================================
    internal class ScanResult
    {
        public List<PhotoItem> Items = new List<PhotoItem>();
        public int AlbumCount;
        public int AlbumDirs;
        /// <summary>硬性阻断：命中后禁止移动</summary>
        public List<string> Blockers = new List<string>();
        /// <summary>温和提示：只展示，不阻断操作</summary>
        public List<string> Hints = new List<string>();
    }

    internal static class Scanner
    {
        public static ScanResult Run(string screenShotDir, int minAgeDays)
        {
            var res = new ScanResult();
            var now = DateTime.Now;

            // 1) 相册缓存名单
            var albumNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var x6 = Directory.GetParent(screenShotDir.TrimEnd('\\'));
            if (x6 != null)
            {
                var gp = Path.Combine(x6.FullName, "Saved", "GamePlayPhotos");
                if (Directory.Exists(gp))
                {
                    foreach (var uid in new DirectoryInfo(gp).GetDirectories())
                    {
                        foreach (var sub in uid.GetDirectories())
                        {
                            if (!sub.Name.StartsWith("NikkiPhotos_", StringComparison.OrdinalIgnoreCase)) continue;
                            res.AlbumDirs++;
                            foreach (var f in sub.GetFiles())
                                albumNames.Add(f.Name);
                        }
                    }
                }
            }
            res.AlbumCount = albumNames.Count;

            // 2) 本地照片
            var ledger = Ledger.Load();
            var extOk = new[] { ".jpg", ".jpeg", ".png" };
            var files = new DirectoryInfo(screenShotDir).GetFiles()
                .Where(f => extOk.Contains(f.Extension.ToLowerInvariant()))
                .ToList();

            foreach (var f in files)
            {
                var inAlbum = albumNames.Contains(f.Name);
                var ever = ledger.Map.ContainsKey(f.Name) && ledger.Map[f.Name].EverInAlbum;

                string status;
                if (inAlbum) status = StatusText.InAlbum;
                else if (ever) status = StatusText.Deleted;
                else status = StatusText.Suspect;

                var age = (now - f.LastWriteTime).TotalDays;
                if (status != StatusText.InAlbum && age < minAgeDays) status = StatusText.TooNew;

                // 更新台账
                if (inAlbum)
                {
                    LedgerEntry e;
                    if (!ledger.Map.TryGetValue(f.Name, out e))
                    {
                        e = new LedgerEntry { FirstSeen = now.ToString("yyyy-MM-dd HH:mm:ss") };
                        ledger.Map[f.Name] = e;
                    }
                    e.EverInAlbum = true;
                    e.LastSeen = now.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else if (ledger.Map.ContainsKey(f.Name))
                {
                    ledger.Map[f.Name].LastSeen = now.ToString("yyyy-MM-dd HH:mm:ss");
                }

                res.Items.Add(new PhotoItem
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    Size = f.Length,
                    Time = f.LastWriteTime,
                    AgeDays = Math.Round(age, 1),
                    Status = status,
                    MinAgeDays = minAgeDays
                });
            }
            ledger.Save();

            res.Items = res.Items
                .OrderBy(p => StatusText.Order(p.Status))
                .ThenByDescending(p => p.Time)
                .ToList();

            // 3) 安全闸门
            if (res.AlbumCount == 0)
                res.Blockers.Add(L10n.T("游戏内相册缓存为空。可能是你清空过缓存，此时比对结果不可信。"));
            if (res.Items.Count >= 5)
            {
                var bad = res.Items.Count(p => p.Status == StatusText.Deleted || p.Status == StatusText.Suspect);
                if ((double)bad / res.Items.Count > 0.7)
                    res.Blockers.Add(L10n.T("超过 70% 的本地照片被判定为待删，比例异常，建议先人工抽查。"));
            }

            // 游戏是否运行只作提示，不阻断：文件若真被占用，移动会失败并如实报告
            if (Process.GetProcessesByName("InfinityNikki").Length > 0)
                res.Hints.Add(L10n.T("游戏正在运行：刚在游戏内删除的照片，本地缓存可能还没刷新，建议稍等几秒再重新扫描。"));

            return res;
        }
    }

    // ============================================================
    //  移动 / 还原（与界面解耦，便于自检与回归测试）
    // ============================================================
    internal class MoveResult
    {
        public string PendingDir;
        public List<string> Moved = new List<string>();      // 原文件名
        public readonly List<KeyValuePair<string, string>> Pairs =
            new List<KeyValuePair<string, string>>();        // 实际名 -> 原名
        public List<string> Failed = new List<string>();
        public string Error;

        public bool Ok { get { return Error == null; } }
        public int RenamedCount { get { return Pairs.Count(p => !string.Equals(p.Key, p.Value, StringComparison.OrdinalIgnoreCase)); } }
    }

    internal class MoveRecord
    {
        public string Source;
        public string Target;
        public readonly List<KeyValuePair<string, string>> Pairs =
            new List<KeyValuePair<string, string>>();        // 实际名 -> 原名
        public int Count { get { return Pairs.Count; } }
    }

    internal static class Mover
    {
        /// <summary>
        /// 待清理文件夹路径。三个原则：
        /// 1) 已经存在的那个（中文名或英文名）优先，切换语言不会「看不见」之前归拢的照片；
        /// 2) 都不存在时按当前界面语言新建；
        /// 3) 两个都存在时中文优先，行为确定，不会一半在中文目录一半在英文目录。
        /// </summary>
        public static string PendingDir(string screenShot)
        {
            if (string.IsNullOrEmpty(screenShot)) return AppPaths.PendingFolderName;

            var zh = Path.Combine(screenShot, AppPaths.PendingZh);
            var en = Path.Combine(screenShot, AppPaths.PendingEn);
            try
            {
                if (Directory.Exists(zh)) return zh;
                if (Directory.Exists(en)) return en;
            }
            catch { }

            return Path.Combine(screenShot, AppPaths.PendingFolderName);
        }

        public static MoveResult Move(List<PhotoItem> target, string screenShot)
        {
            var r = new MoveResult { PendingDir = PendingDir(screenShot) };
            if (target == null || target.Count == 0) { r.Error = L10n.T("没有选中任何照片。"); return r; }

            try { Directory.CreateDirectory(r.PendingDir); }
            catch (Exception ex)
            {
                r.Error = L10n.T("无法创建待清理文件夹：") + "\n" + r.PendingDir + "\n\n" + ex.Message;
                return r;
            }

            var batch = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            foreach (var p in target)
            {
                try
                {
                    if (!File.Exists(p.FullPath)) { r.Failed.Add(p.Name + L10n.T(" — 文件已不存在")); continue; }

                    var dest = UniqueName(r.PendingDir, p.Name, batch);
                    File.Move(p.FullPath, dest);

                    var actual = Path.GetFileName(dest);
                    r.Moved.Add(p.Name);
                    r.Pairs.Add(new KeyValuePair<string, string>(actual, p.Name));
                }
                catch (Exception ex)
                {
                    r.Failed.Add(p.Name + " — " + ex.Message);
                }
            }

            if (r.Pairs.Count > 0) WriteRecord(screenShot, r);
            return r;
        }

        /// <summary>目标已存在同名文件时追加批次号 / 序号，避免覆盖</summary>
        private static string UniqueName(string dir, string name, string batch)
        {
            var dest = Path.Combine(dir, name);
            if (!File.Exists(dest)) return dest;

            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            dest = Path.Combine(dir, stem + "_" + batch + ext);
            var n = 2;
            while (File.Exists(dest))
            {
                dest = Path.Combine(dir, stem + "_" + batch + "_" + n + ext);
                n++;
            }
            return dest;
        }

        /// <summary>
        /// 追加写入记录：待清理文件夹是固定的，多次移动会叠在一起，
        /// 记录必须累积，否则上一批照片就再也还原不回来了。
        /// </summary>
        private static void WriteRecord(string screenShot, MoveResult r)
        {
            try
            {
                var dir = Path.GetDirectoryName(AppPaths.LastMovePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var merged = new List<KeyValuePair<string, string>>(r.Pairs);
                try
                {
                    foreach (var kv in ReadRecord().Pairs)
                    {
                        if (!merged.Any(x => string.Equals(x.Key, kv.Key, StringComparison.OrdinalIgnoreCase)))
                            merged.Add(kv);
                    }
                }
                catch { }

                var lines = new List<string>
                {
                    "# " + L10n.F("本次移动记录 {0}（共 {1} 条，累积）",
                                  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), merged.Count),
                    "# " + L10n.T("源目录") + "\t" + screenShot,
                    "# " + L10n.T("目标目录") + "\t" + r.PendingDir
                };
                lines.AddRange(merged.Select(kv => "MOVE\t" + kv.Key + "\t" + kv.Value));
                File.WriteAllLines(AppPaths.LastMovePath, lines, new UTF8Encoding(false));
            }
            catch { }
        }

        public static MoveRecord ReadRecord()
        {
            var rec = new MoveRecord();
            if (!File.Exists(AppPaths.LastMovePath)) return rec;

            try
            {
                foreach (var line in File.ReadAllLines(AppPaths.LastMovePath, Encoding.UTF8))
                {
                    if (line.StartsWith("#"))
                    {
                        // 兼容中英文两种记录头（旧版本写的是中文，新版本按界面语言写）
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"^#\s*(?:源目录|Source)\s*\t(.+)$");
                        if (m.Success) { rec.Source = m.Groups[1].Value.Trim(); continue; }
                        m = System.Text.RegularExpressions.Regex.Match(line, @"^#\s*(?:目标目录|Target)\s*\t(.+)$");
                        if (m.Success) { rec.Target = m.Groups[1].Value.Trim(); continue; }
                        continue;
                    }
                    if (!line.StartsWith("MOVE\t")) continue;

                    var f = line.Split('\t');
                    if (f.Length >= 3) rec.Pairs.Add(new KeyValuePair<string, string>(f[1], f[2]));
                    else if (f.Length == 2) rec.Pairs.Add(new KeyValuePair<string, string>(f[1], f[1]));
                }
            }
            catch { }
            return rec;
        }

        /// <summary>把上次移动的照片移回原位。返回还原张数与失败数。</summary>
        public static int Restore(MoveRecord rec, out int failed)
        {
            failed = 0;
            var ok = 0;
            if (rec == null || rec.Target == null || rec.Source == null) return 0;

            foreach (var kv in rec.Pairs)
            {
                var from = Path.Combine(rec.Target, kv.Key);
                if (!File.Exists(from)) continue;

                var origin = string.IsNullOrEmpty(kv.Value) ? kv.Key : kv.Value;
                var to = Path.Combine(rec.Source, origin);
                if (File.Exists(to))
                {
                    to = Path.Combine(rec.Source,
                        Path.GetFileNameWithoutExtension(origin) + L10n.T("_还原") + DateTime.Now.ToString("HHmmss") +
                        Path.GetExtension(origin));
                }

                try { File.Move(from, to); ok++; }
                catch { failed++; }
            }

            // 待清理文件夹空了就删掉，避免留下一堆空目录
            try
            {
                if (Directory.Exists(rec.Target) &&
                    Directory.GetFiles(rec.Target).Length == 0 &&
                    Directory.GetDirectories(rec.Target).Length == 0)
                    Directory.Delete(rec.Target);
            }
            catch { }

            // 全部归还完毕，记录已无用处，清掉避免下次误操作
            if (ok > 0 && failed == 0)
            {
                try { File.Delete(AppPaths.LastMovePath); } catch { }
            }

            return ok;
        }
    }

    // ============================================================
    //  主窗体
    // ============================================================
    internal class MainForm : Form
    {
        private const int MinAgeDays = 3;

        private string _screenShot;
        private ScanResult _result;
        private List<PhotoItem> _view = new List<PhotoItem>();

        /// <summary>用户当前勾选要移入待清理文件夹的照片（按文件名记，切换筛选不丢）</summary>
        private readonly HashSet<string> _checked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Label _lblPath;
        private TextBox _txtPath;
        private SoftButton _btnPick;
        private SoftButton _btnRescan;
        private SoftButton _btnLang;
        private PillToggle _chkOnlyPending;
        private ListView _list;
        private ListHeader _listHeader;
        private Panel _listTools;
        private Label _lblSel;
        private SoftButton _btnSelAll;
        private StatCard _statKeep, _statDel, _statSus, _statNew;
        private PictureBox _preview;
        private Label _lblInfo;
        private Label _lblStat;
        private SoftButton _btnReport;
        private SoftButton _btnMove;
        private SoftButton _btnOpenPending;
        private SoftButton _btnRestore;
        private SoftButton _btnClean;
        private Label _lblHint;
        private ProgressBar _bar;
        private SplitContainer _split;

        /// <summary>鼠标悬停的行号，用于列表的柔和高亮</summary>
        private int _hotRow = -1;

        private Panel _bottomBar;
        private Control[] _bottomBtns;

        /// <summary>--shot 截图模式：不弹窗、不扫描真实目录</summary>
        internal bool Headless;
        internal bool DemoMode;

        private static readonly Color Bg = Theme.Bg;
        private static readonly Color Line = Theme.Line;

        /// <summary>英文界面用 Segoe UI，中文界面用微软雅黑，字重与行距都更合适</summary>
        private static string UiFontName
        {
            get { return Theme.Family; }
        }

        public MainForm()
        {
            AppPaths.EnsureDirs();
            BuildUi();
            Load += (s, e) => InitSplit();
            Shown += (s, e) => StartScan(true);
        }

        /// <summary>把系统灰蓝标题栏染成界面底色，Win10 上该调用会失败，静默保留原样。</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dwm.TintCaption(this);
        }

        private void BuildUi()
        {
            Text = L10n.T("无限暖暖 · 本地照片清理助手");

            // 字体按点值渲染，会跟着系统 DPI 放大；窗口如果还固定 1180×780，
            // 在 125%/150% 缩放下内容就会被挤成一团。这里按 DPI 同比例放大，
            // 再用工作区封顶，避免在高缩放的小屏上超出屏幕。
            var dpiScale = DeviceDpi / 96f;
            var wa = Screen.PrimaryScreen != null
                ? Screen.PrimaryScreen.WorkingArea
                : new Rectangle(0, 0, 1280, 800);
            var winW = Math.Min((int)(1180 * dpiScale), (int)(wa.Width * 0.90));
            var winH = Math.Min((int)(780 * dpiScale), (int)(wa.Height * 0.92));
            if (winW < 960) winW = 960;
            if (winH < 640) winH = 640;

            ClientSize = new Size(winW, winH);
            MinimumSize = new Size(Math.Min((int)(1000 * dpiScale), winW), Math.Min((int)(680 * dpiScale), winH));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Font = Theme.F(9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            // 用 exe 自带的「大喵扫地」图标，任务栏和标题栏都是它
            try { Icon = Theme.WindowIcon; } catch { }

            // 底下的大容器：统一给四周留白，卡片之间靠 CardPanel.Inset 拉开间距
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(22, 8, 22, 16) };

            // ================= 顶部品牌 =================
            var header = new BrandPanel
            {
                Dock = DockStyle.Top,
                Height = 64,
                Padding = new Padding(0),
                Title = L10n.T("本地照片清理助手"),
                Subtitle = L10n.T("只识别与归拢，绝不自动删除照片")
            };

            _btnLang = new SoftButton(L10n.IsEnglish ? "中文" : "English", BtnKind.Ghost, 74) { Height = 28 };
            _btnLang.Font = Theme.F(8.6f);
            _btnLang.Click += (s, e) => SwitchLanguage();

            var tipLang = new ToolTip();
            tipLang.SetToolTip(_btnLang, L10n.T("切换界面语言。已经生成的数据文件夹和照片位置不会改变。"));

            header.Controls.Add(_btnLang);
            header.Resize += (s, e) =>
            {
                _btnLang.Left = Math.Max(0, header.Width - _btnLang.Width);
                _btnLang.Top = 19;
            };

            // ================= 目录行 =================
            var pathCard = new CardPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                Inset = 6,
                Radius = Theme.R,
                PadX = 14,
                PadY = 0
            };
            pathCard.ApplyPadding();

            _lblPath = new Label
            {
                Text = L10n.T("截图目录"),
                AutoSize = false,
                Font = Theme.F(8.8f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var pathBox = new PathBox();
            _txtPath = new TextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.CardWarm,
                ForeColor = Theme.Text,
                Font = Theme.F(8.8f),
                Dock = DockStyle.Fill
            };
            pathBox.Controls.Add(_txtPath);

            _btnPick = Btn(L10n.T("重新选择"), BtnKind.Ghost, 88);
            _btnRescan = Btn(L10n.T("重新扫描"), BtnKind.Primary, 94);
            _btnPick.Click += (s, e) => PickFolder();
            _btnRescan.Click += (s, e) => StartScan(false);

            pathCard.Controls.Add(_lblPath);
            pathCard.Controls.Add(pathBox);
            pathCard.Controls.Add(_btnPick);
            pathCard.Controls.Add(_btnRescan);

            pathCard.Resize += (s, e) => LayoutPathRow(pathCard, pathBox);
            pathCard.Layout += (s, e) => LayoutPathRow(pathCard, pathBox);

            // ================= 统计卡 =================
            var statsRow = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Theme.Bg };
            _statKeep = new StatCard { Accent = Theme.StatusAccent(StatusText.InAlbum), Label = L10n.T("仍在相册") };
            _statDel = new StatCard { Accent = Theme.StatusAccent(StatusText.Deleted), Label = L10n.T("游戏里已删") };
            _statSus = new StatCard { Accent = Theme.StatusAccent(StatusText.Suspect), Label = L10n.T("无法确认") };
            _statNew = new StatCard { Accent = Theme.StatusAccent(StatusText.TooNew), Label = L10n.T("太新") };
            statsRow.Controls.Add(_statKeep);
            statsRow.Controls.Add(_statDel);
            statsRow.Controls.Add(_statSus);
            statsRow.Controls.Add(_statNew);
            statsRow.Resize += (s, e) => LayoutStats(statsRow);

            // ================= 工具条 =================
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.Bg };

            _chkOnlyPending = new PillToggle(L10n.T("只看待处理项")) { Location = new Point(6, 6) };
            _chkOnlyPending.CheckedChanged += (s, e) => RebindList();

            _lblStat = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Font = Theme.F(8.8f),
                ForeColor = Theme.Text2,
                BackColor = Color.Transparent,
                Text = ""
            };
            toolbar.Controls.Add(_lblStat);
            toolbar.Controls.Add(_chkOnlyPending);
            toolbar.Resize += (s, e) =>
            {
                _lblStat.SetBounds(toolbar.Width - 6 - Math.Max(120, toolbar.Width / 2), 6,
                    Math.Max(120, toolbar.Width / 2), 30);
            };

            var tip = new ToolTip();
            tip.SetToolTip(_chkOnlyPending, L10n.T("只列出「已删除 / 疑似 / 太新」三类，隐藏仍在相册里的照片"));

            // ================= 中间：列表 + 预览 =================
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 10,
                BackColor = Theme.Bg
            };
            _split.Panel1.BackColor = Theme.Bg;
            _split.Panel2.BackColor = Theme.Bg;

            var listCard = new CardPanel
            {
                Dock = DockStyle.Fill,
                Inset = 6,
                Radius = Theme.R,
                PadX = 0,
                PadY = 0
            };
            listCard.ApplyPadding();
            listCard.Padding = new Padding(7, 12, 7, 7);

            // 表头必须和列一一对应：第 0 列是勾选圈，标题留空
            _listHeader = new ListHeader(new[]
            {
                "", L10n.T("状态"), L10n.T("文件名"), L10n.T("拍摄时间"), L10n.T("大小")
            })
            { Dock = DockStyle.Top, Target = null };

            _list = new BufferedListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                CheckBoxes = false,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                Font = Theme.F(9f),
                OwnerDraw = true,
                HeaderStyle = ColumnHeaderStyle.None
            };

            // 空的第一列只放自绘复选框；给一个 1px 高的 ImageList 只为把行高撑开
            _list.Columns.Add("", 40);
            _list.Columns.Add(L10n.T("状态"), 88);
            _list.Columns.Add(L10n.T("文件名"), 300);
            _list.Columns.Add(L10n.T("拍摄时间"), 152);
            _list.Columns.Add(L10n.T("大小"), 70);

            var il = new ImageList { ImageSize = new Size(1, 38), ColorDepth = ColorDepth.Depth32Bit };
            il.Images.Add(new Bitmap(1, 38));
            _list.SmallImageList = il;

            _listHeader.Target = _list;

            // 勾选工具条：列表上方一条窄行，左边报数，右边一键勾选全部 / 全部取消。
            // 上百张照片逐个点勾选圈不现实，得有个总开关。
            _listTools = new Panel { Dock = DockStyle.Top, Height = S(34), BackColor = Theme.Card };

            _lblSel = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.4f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent,
                Text = ""
            };

            _btnSelAll = new SoftButton(L10n.T("全选"), BtnKind.Ghost, S(74));
            _btnSelAll.Font = Theme.F(8.4f);
            _btnSelAll.Height = S(26);
            _btnSelAll.PadX = 12;
            _btnSelAll.FitWidth();
            _btnSelAll.Click += (s, e) => ToggleSelectAll();

            _listTools.Controls.Add(_lblSel);
            _listTools.Controls.Add(_btnSelAll);
            _listTools.Resize += (s, e) => LayoutListTools();

            _list.SelectedIndexChanged += (s, e) => ShowPreview();
            _list.DoubleClick += (s, e) => OpenSelectedOriginal();
            _list.MouseUp += ListMouseUp;
            _list.MouseDown += ListMouseDown;
            _list.MouseMove += ListMouseMove;
            _list.MouseLeave += (s, e) =>
            {
                if (_hotRow == -1) return;
                var r = _hotRow;
                _hotRow = -1;
                InvalidateRow(r);
            };
            _list.KeyDown += (s, e) =>
            {
                // Ctrl+A 直接勾选全部，比「先全选行、再点按钮」顺手
                if (e.Control && e.KeyCode == Keys.A)
                {
                    ToggleSelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            _list.DrawColumnHeader += ListDrawHeader;
            _list.DrawItem += ListDrawItem;
            _list.DrawSubItem += ListDrawSubItem;
            _list.Resize += (s, e) => { LayoutColumns(); RoundedClip.Apply(_list, 10); };

            listCard.Controls.Add(_list);
            listCard.Controls.Add(_listHeader);
            listCard.Controls.Add(_listTools);   // 后加的先停靠：工具条在最上面，其次表头，剩下给列表
            _split.Panel1.Controls.Add(listCard);

            var previewCard = new CardPanel
            {
                Dock = DockStyle.Fill,
                Inset = 6,
                Radius = Theme.R,
                PadX = 0,
                PadY = 0
            };
            previewCard.ApplyPadding();
            previewCard.Padding = new Padding(9, 9, 9, 9);

            _preview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Theme.CardWarm
            };

            _lblInfo = new Label
            {
                Dock = DockStyle.Bottom,
                AutoSize = false,
                // 英文的 Reason 往往要折两行，多留一行高度，否则最后一句会被裁掉
                Height = L10n.IsEnglish ? 134 : 112,
                Padding = new Padding(14, 12, 14, 12),
                BackColor = Theme.CardSoft,
                ForeColor = Theme.Text2,
                Font = Theme.F(8.5f),
                Text = ""
            };

            previewCard.Controls.Add(_preview);
            previewCard.Controls.Add(_lblInfo);
            _preview.Resize += (s, e) => RoundedClip.Apply(_preview, 10);
            _split.Panel2.Controls.Add(previewCard);

            // ================= 底部操作条 =================
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 66, BackColor = Theme.Bg };

            _btnReport = Btn(L10n.T("生成网页报告"), BtnKind.Ghost, 112);
            _btnMove = Btn(L10n.T("移入待清理文件夹"), BtnKind.Primary, 146);
            _btnOpenPending = Btn(L10n.T("打开待清理文件夹"), BtnKind.Ghost, 136);
            _btnRestore = Btn(L10n.T("移回原目录"), BtnKind.Ghost, 104);
            _btnClean = Btn(L10n.T("清理记录"), BtnKind.Danger, 96);

            _btnReport.Click += (s, e) => GenerateReport();
            _btnMove.Click += (s, e) => MoveToPending();
            _btnOpenPending.Click += (s, e) => OpenLatestPending();
            _btnRestore.Click += (s, e) => RestoreLast();
            _btnClean.Click += (s, e) => CleanRecords();

            // 不用 FlowLayoutPanel：RightToLeft 下它的实际占宽和子控件宽度对不上，
            // 会白吃掉一截宽度把左边的提示文字挤掉。这里直接手工从右往左摆。
            _bottomBtns = new Control[] { _btnMove, _btnReport, _btnOpenPending, _btnRestore, _btnClean };
            foreach (var b in _bottomBtns)
            {
                var sb = b as SoftButton;
                if (sb != null) { sb.PadX = 12; sb.FitWidth(); }   // 收窄内边距后要重算宽度
                bottom.Controls.Add(b);
            }

            _lblHint = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.5f),
                ForeColor = Theme.Text3,
                BackColor = Color.Transparent,
                // 不加 AutoEllipsis：底部按钮组会吃掉不少宽度，长提示宁可折成两行，
                // 也不要在中间截成「…判定为「已…」这样半句话。
                Text = L10n.T("双击打开原图 · 右键定位文件")
            };

            bottom.Controls.Add(_lblHint);
            _bottomBar = bottom;
            bottom.Resize += (s, e) => LayoutBottom();

            _bar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 3,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 26,
                Visible = false
            };

            // Dock 顺序：先加的贴内层，后加的贴外沿
            root.Controls.Add(_split);
            root.Controls.Add(_bar);
            root.Controls.Add(bottom);
            root.Controls.Add(toolbar);
            root.Controls.Add(statsRow);
            root.Controls.Add(pathCard);
            root.Controls.Add(header);

            Controls.Add(root);
        }

        /// <summary>底部操作条：按钮从右往左贴边，提示文字吃掉剩下的宽度。</summary>
        private void LayoutBottom()
        {
            if (_bottomBar == null || _bottomBtns == null) return;

            var gap = (int)Math.Round(8 * (DeviceDpi / 96f));
            var right = _bottomBar.Width - 4;
            var y = (Math.Max(34, _bottomBar.Height) - 34) / 2;
            var btnH = 34;

            // 数组里第一个是最右边的主动作，依次往左
            foreach (var b in _bottomBtns)
            {
                b.SetBounds(right - b.Width, y, b.Width, btnH);
                right -= b.Width + gap;
            }

            var hintW = Math.Max(60, right - 6);
            _lblHint.SetBounds(4, 4, hintW, Math.Max(28, _bottomBar.Height - 8));
        }

        /// <summary>目录行的手工布局：标签固定、按钮靠右、输入框吃中间剩余宽度。</summary>
        private void LayoutPathRow(CardPanel card, Panel pathBox)
        {
            var padL = card.Padding.Left;
            var padR = card.Padding.Right;
            var innerW = card.Width - padL - padR;
            if (innerW <= 0) return;

            var h = 30;
            var top = (card.Height - h) / 2;

            // 标签宽度按文字实测，别写死——中英文、不同 DPI 下都会被截成「截图目」
            var labelW = TextRenderer.MeasureText(_lblPath.Text, _lblPath.Font).Width + 6;
            _lblPath.SetBounds(padL, top, labelW, h);

            _btnRescan.Height = 30;
            _btnPick.Height = 30;
            var bx = padL + innerW - _btnRescan.Width;
            _btnRescan.SetBounds(bx, top, _btnRescan.Width, h);
            bx -= _btnPick.Width + 8;
            _btnPick.SetBounds(bx, top, _btnPick.Width, h);

            var boxX = padL + labelW + 14;
            var boxW = Math.Max(80, bx - 10 - boxX);
            pathBox.SetBounds(boxX, top, boxW, h);
            _txtPath.SetBounds(11, (h - _txtPath.Height) / 2 + 1, Math.Max(10, boxW - 22), _txtPath.Height);
        }

        private void LayoutStats(Panel row)
        {
            const int gap = 6;
            var cards = new[] { _statKeep, _statDel, _statSus, _statNew };
            var w = (row.Width - gap * 2 * cards.Length) / cards.Length;
            if (w < 40) return;
            var x = gap;
            foreach (var c in cards)
            {
                c.SetBounds(x, 6, w, row.Height - 12);
                x += w + gap * 2;
            }
        }

        private void LayoutColumns()
        {
            if (_list == null || _list.Columns.Count < 5) return;
            var scale = DeviceDpi / 96f;

            // 时间列按实际文字量算，不同 DPI / 字体下都不会被截成「10-20 23…」
            var timeW = TextRenderer.MeasureText("10-20 23:26", Theme.F(8.6f)).Width + 22;
            var baseTimeW = (int)(96 * scale);
            if (timeW < baseTimeW) timeW = baseTimeW;

            var c0 = (int)(34 * scale);
            var c1 = TextRenderer.MeasureText(StatusText.Name(StatusText.Suspect), Theme.F(8.2f)).Width + 30;
            if (c1 < (int)(62 * scale)) c1 = (int)(62 * scale);
            var c4 = TextRenderer.MeasureText("000.0 MB", Theme.F(8.6f)).Width + 24;
            if (c4 < (int)(58 * scale)) c4 = (int)(58 * scale);

            // 一定要预留垂直滚动条宽度：ListView 出现滚动条时不保证触发 Resize，
            // 按「当前」ClientSize 算满会溢出，冒出横向滚动条并把「大小」列裁掉。
            var scroll = SystemInformation.VerticalScrollBarWidth;
            var fixedW = c0 + c1 + timeW + c4;

            // 逐列设宽度，每设一次 ListView 就重排一遍并重画；包在 BeginUpdate 里只重画一次
            _list.BeginUpdate();
            try
            {
                _list.Columns[0].Width = c0;
                _list.Columns[1].Width = c1;
                _list.Columns[3].Width = timeW;
                _list.Columns[4].Width = c4;

                var nameW = _list.ClientSize.Width - fixedW - scroll - 2;
                if (nameW < 120) nameW = 120;
                _list.Columns[2].Width = nameW;
            }
            finally { _list.EndUpdate(); }
        }

        private void SwitchLanguage()
        {
            L10n.Toggle();

            SuspendLayout();
            try
            {
                foreach (var c in Controls.Cast<Control>().ToArray()) c.Dispose();
                Controls.Clear();
                BuildUi();
            }
            finally { ResumeLayout(true); }

            InitSplit();

            if (string.IsNullOrEmpty(_screenShot)) { SetEmpty(L10n.T("尚未指定游戏目录")); return; }

            // 保留用户手动勾选的内容，不能被重新扫描冲掉
            var keep = new HashSet<string>(_checked, StringComparer.OrdinalIgnoreCase);
            StartScan(false);
            _checked.Clear();
            foreach (var n in keep) _checked.Add(n);
            RebindList();
            UpdateStat();
        }

        /// <summary>--shot demo 用：灌一批示意数据，不开游戏也能看真实排版。</summary>
        private void LoadDemoData()
        {
            var specs = new[]
            {
                new object[] { "2026_10_20_21_24_47_2143244.jpeg", StatusText.Deleted, 9.0 },
                new object[] { "2026_10_19_18_03_12_2138771.jpeg", StatusText.Deleted, 10.0 },
                new object[] { "2026_10_17_11_42_58_2129903.jpeg", StatusText.Deleted, 12.0 },
                new object[] { "2026_10_16_09_15_33_2124418.jpeg", StatusText.Deleted, 13.0 },
                new object[] { "2026_10_12_20_31_07_2116730.jpeg", StatusText.Suspect, 17.0 },
                new object[] { "2026_10_11_22_08_44_2115092.jpeg", StatusText.Suspect, 18.0 },
                new object[] { "2026_10_29_08_12_05_2160140.jpeg", StatusText.TooNew, 0.4 },
                new object[] { "2026_10_28_19_55_31_2158802.jpeg", StatusText.TooNew, 1.1 },
                new object[] { "2026_10_24_15_20_09_2151277.jpeg", StatusText.InAlbum, 5.0 },
                new object[] { "2026_10_23_12_47_26_2149864.jpeg", StatusText.InAlbum, 6.0 },
                new object[] { "2026_10_22_17_33_51_2148205.jpeg", StatusText.InAlbum, 7.0 },
                new object[] { "2026_10_21_10_06_14_2146922.jpeg", StatusText.InAlbum, 8.0 }
            };

            var dir = Path.Combine(Path.GetTempPath(), "nikki-demo");
            try { Directory.CreateDirectory(dir); } catch { }
            var items = new List<PhotoItem>();
            foreach (var s in specs)
            {
                var name = (string)s[0];
                var full = Path.Combine(dir, name);
                try
                {
                    if (!File.Exists(full)) DemoImage(full, items.Count);
                }
                catch { }

                items.Add(new PhotoItem
                {
                    Name = name,
                    FullPath = full,
                    Size = 1_240_000 + items.Count * 33_000L,
                    Time = DateTime.Now.AddDays(-(double)s[2]),
                    AgeDays = (double)s[2],
                    Status = (string)s[1]
                });
            }

            _screenShot = dir;
            _txtPath.Text = dir;
            _result = new ScanResult { Items = items, AlbumCount = 44, AlbumDirs = 3 };

            _checked.Clear();
            foreach (var p in items.Where(x => x.Status == StatusText.Deleted)) _checked.Add(p.Name);

            RebindList();
            UpdateStat();
        }

        /// <summary>
        /// 演示模式用的占位图。真实文件不存在的话预览区是空白的，
        /// 排版看不出问题，所以按序号画一张渐变小图落到临时目录。
        /// </summary>
        private static void DemoImage(string path, int idx)
        {
            var palettes = new[]
            {
                new[] { "#F6D9C4", "#E8B49A", "#B9785F" },
                new[] { "#D9E7CF", "#A9C9AC", "#5F8A6E" },
                new[] { "#EFE3B8", "#DCC98A", "#A08B45" },
                new[] { "#DCE0F0", "#B3BEDD", "#66739E" },
                new[] { "#F3DCE2", "#E0B6C4", "#A9707F" },
                new[] { "#D8E8EA", "#A7C6CC", "#5F858C" }
            };

            using (var bmp = new Bitmap(900, 675))
            using (var g = Graphics.FromImage(bmp))
            {
                var p = palettes[idx % palettes.Length];
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (var lg = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, 900, 675),
                    ColorTranslator.FromHtml(p[0]), ColorTranslator.FromHtml(p[1]), 45f))
                    g.FillRectangle(lg, 0, 0, 900, 675);

                // 远山
                using (var hill = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    hill.AddBezier(0, 600, 220, 455, 430, 690, 900, 520);
                    hill.AddLine(900, 675, 0, 675);
                    hill.CloseFigure();
                    using (var b = new SolidBrush(Color.FromArgb(140, ColorTranslator.FromHtml(p[2]))))
                        g.FillPath(b, hill);
                }

                // 光斑
                using (var b = new SolidBrush(Color.FromArgb(110, Color.White)))
                {
                    g.FillEllipse(b, 649, 64, 172, 172);
                    g.FillEllipse(b, 143, 178, 14, 14);
                    g.FillEllipse(b, 186, 208, 8, 8);
                }

                using (var f = new Font("Microsoft YaHei UI", 21f))
                using (var b = new SolidBrush(Color.FromArgb(140, 58, 64, 58)))
                    g.DrawString(L10n.T("示意") + " " + (idx + 1), f, b, 56, 588);

                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
        }

        private void InitSplit()
        {
            try
            {
                var scale = DeviceDpi / 96f;
                LayoutColumns();

                var w = _split.Width;
                if (w < 660) return;
                _split.Panel1MinSize = (int)(360 * scale);
                _split.Panel2MinSize = (int)(240 * scale);
                var d = (int)(w * 0.67);
                var max = w - _split.Panel2MinSize - _split.SplitterWidth;
                if (d > max) d = max;
                if (d > _split.Panel1MinSize) _split.SplitterDistance = d;
            }
            catch { }
        }

        private static SoftButton Btn(string text, BtnKind kind, int minWidth)
        {
            var b = new SoftButton(text, kind, minWidth);
            b.Font = Theme.F(9f);
            return b;
        }

        // ==================== 列表自绘 ====================

        private void ListDrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = false;
        }

        /// <summary>行底板：选中画淡绿圆角块，悬停画更淡的一层。</summary>
        private void ListDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            var g = e.Graphics;
            var row = new Rectangle(1, e.Bounds.Top, _list.ClientSize.Width - 2, e.Bounds.Height - 1);

            if (e.Item.Selected)
                Theme.FillRound(g, row, 8, Theme.GreenSoft);
            else if (e.ItemIndex == _hotRow)
                Theme.FillRound(g, row, 8, Color.FromArgb(0xFB, 0xFA, 0xF5));
        }

        /// <summary>单元格：勾选圈、状态药丸、文件名、时间、大小。</summary>
        private void ListDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = false;
            var g = e.Graphics;
            var p = e.Item.Tag as PhotoItem;
            if (p == null) return;

            var cell = e.Bounds;
            var mid = cell.Top + cell.Height / 2;

            if (e.ColumnIndex == 0)
            {
                var box = new Rectangle(cell.Left + 14, mid - 8, 16, 16);
                var on = _checked.Contains(p.Name);
                Theme.FillRound(g, box, 5, on ? Theme.Green : Color.White);
                using (var path = Theme.Round(box, 5))
                using (var pen = new Pen(on ? Theme.Green : Color.FromArgb(0xD2, 0xD7, 0xCE), 1.2f))
                    g.DrawPath(pen, path);
                if (on)
                {
                    using (var pen = new Pen(Color.White, 1.8f)
                    {
                        StartCap = System.Drawing.Drawing2D.LineCap.Round,
                        EndCap = System.Drawing.Drawing2D.LineCap.Round
                    })
                    {
                        float cx = box.X + box.Width / 2f, cy = box.Y + box.Height / 2f;
                        g.DrawLines(pen, new[]
                        {
                            new PointF(cx - 3.6f, cy + 0.1f),
                            new PointF(cx - 1.1f, cy + 2.7f),
                            new PointF(cx + 3.8f, cy - 2.6f)
                        });
                    }
                }
                return;
            }

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
                var r = new Rectangle(cell.Left + 4, cell.Top, cell.Width - 8, cell.Height);
                Theme.Str(g, p.Name, Theme.F(8.9f), e.Item.Selected ? Theme.Text : Theme.Text2, r, Theme.LeftMid);
                return;
            }

            if (e.ColumnIndex == 3)
            {
                var r = new Rectangle(cell.Left, cell.Top, cell.Width - 8, cell.Height);
                Theme.Str(g, p.Time.ToString("MM-dd HH:mm"), Theme.F(8.6f), Theme.Text3, r, Theme.RightMid);
                return;
            }

            var r2 = new Rectangle(cell.Left, cell.Top, cell.Width - 12, cell.Height);
            Theme.Str(g, FmtSize(p.Size), Theme.F(8.6f), Theme.Text3, r2, Theme.RightMid);
        }

        private void ListMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var hit = _list.GetItemAt(e.X, e.Y);
            if (hit == null) return;

            // 只有点在勾选圈附近才算勾选，避免误触
            var rect = hit.Bounds;
            if (e.X >= rect.Left + 6 && e.X <= rect.Left + 40)
            {
                var p = hit.Tag as PhotoItem;
                if (p == null) return;
                if (_checked.Contains(p.Name)) _checked.Remove(p.Name);
                else _checked.Add(p.Name);
                _list.Invalidate(rect);      // 只刷这一行，别整表重画
                RefreshSelUi();
                UpdateMoveButtonText();
            }
        }

        private void ListMouseMove(object sender, MouseEventArgs e)
        {
            var hit = _list.GetItemAt(e.X, e.Y);
            var idx = hit == null ? -1 : hit.Index;
            if (idx == _hotRow) return;

            // 只重画「刚离开」和「刚进入」两行。以前这里整表 Invalidate，
            // 鼠标一动整列文字就跟着闪一下，看着像列表在不停刷新。
            var old = _hotRow;
            _hotRow = idx;
            InvalidateRow(old);
            InvalidateRow(idx);
        }

        private void InvalidateRow(int idx)
        {
            if (idx < 0 || idx >= _list.Items.Count) return;
            var b = _list.Items[idx].Bounds;
            if (b.Height <= 0) return;
            _list.Invalidate(new Rectangle(0, b.Top - 1, _list.ClientSize.Width, b.Height + 2));
        }

        // ==================== 扫描 ====================
        private void PickFolder()
        {
            var p = GameLocator.AskUser(this);
            if (p != null)
            {
                _screenShot = p;
                GameLocator.Save(p);
                StartScan(false);
            }
        }

        private void StartScan(bool autoLocate)
        {
            if (Headless)
            {
                if (DemoMode) LoadDemoData();
                else SetEmpty(L10n.T("尚未指定游戏目录"));
                return;
            }

            if (autoLocate)
            {
                _screenShot = GameLocator.Auto();
                if (_screenShot == null)
                {
                    var r = MessageBox.Show(this,
                        L10n.T("没能自动找到游戏的 ScreenShot 文件夹。\n要手动选择吗？"),
                        L10n.T("未找到游戏目录"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes) _screenShot = GameLocator.AskUser(this);
                    if (_screenShot == null) { SetEmpty(L10n.T("尚未指定游戏目录")); return; }
                    GameLocator.Save(_screenShot);
                }
            }

            if (_screenShot == null) { SetEmpty(L10n.T("尚未指定游戏目录")); return; }
            _txtPath.Text = _screenShot;

            Cursor = Cursors.WaitCursor;
            UseWaitCursor = true;
            _bar.Visible = true;
            Application.DoEvents();

            try
            {
                _result = Scanner.Run(_screenShot, MinAgeDays);

                // 每次扫描后，把判定为「已删除」的照片自动勾选，用户可自行取消
                _checked.Clear();
                foreach (var p in _result.Items.Where(p => p.Status == StatusText.Deleted))
                    _checked.Add(p.Name);

                RebindList();
                UpdateStat();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L10n.T("扫描失败：") + ex.Message, L10n.T("出错了"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _bar.Visible = false;
                UseWaitCursor = false;
                Cursor = Cursors.Default;
            }
        }

        private void SetEmpty(string hint)
        {
            _result = null;
            _view.Clear();
            _checked.Clear();
            _list.Items.Clear();
            _preview.Image = null;
            _lblStat.Text = "";
            _statKeep.Count = "0";
            _statDel.Count = "0";
            _statSus.Count = "0";
            _statNew.Count = "0";
            _lblHint.Text = hint;
            UpdateButtons();
        }

        private void RebindList()
        {
            if (_result == null) return;

            _view = _chkOnlyPending.Checked
                ? _result.Items.Where(p => p.Status != StatusText.InAlbum).ToList()
                : _result.Items.ToList();

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var p in _view)
            {
                // 第一列留给自绘的勾选圈，文字从第二列开始；显示内容全部由 PhotoItem 现画
                var it = new ListViewItem("");
                it.SubItems.Add(StatusText.Name(p.Status));
                it.SubItems.Add(p.Name);
                it.SubItems.Add(p.Time.ToString("yyyy-MM-dd HH:mm"));
                it.SubItems.Add(FmtSize(p.Size));
                it.Tag = p;
                // 中文界面下文件名列放不下 30+ 字符，悬停给出完整名字
                it.ToolTipText = p.Name;
                _list.Items.Add(it);
            }
            _list.EndUpdate();

            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
            LayoutColumns();
            _list.Invalidate();
            UpdateButtons();
        }

        private void UpdateMoveButtonText()
        {
            var n = _checked.Count;
            _btnMove.Text = n > 0
                ? L10n.F("移入待清理文件夹（{0}）", n)
                : L10n.T("移入待清理文件夹");
            LayoutBottom();
        }

        private int S(int v) { return (int)Math.Round(v * (DeviceDpi / 96f)); }

        /// <summary>工具条：按钮贴右，报数靠左吃掉剩下的宽度。</summary>
        private void LayoutListTools()
        {
            if (_listTools == null || _btnSelAll == null || _lblSel == null) return;

            var b = _btnSelAll;
            var x = _listTools.Width - b.Width - S(2);
            b.SetBounds(x, (_listTools.Height - b.Height) / 2, b.Width, b.Height);
            _lblSel.SetBounds(S(4), 0, Math.Max(S(40), x - S(10)), _listTools.Height);
        }

        /// <summary>刷新「已勾选 12 / 128 张」与全选按钮的措辞。</summary>
        private void RefreshSelUi()
        {
            if (_btnSelAll == null || _lblSel == null) return;

            var total = _view == null ? 0 : _view.Count;
            var n = total == 0 ? 0 : _view.Count(p => _checked.Contains(p.Name));

            // 当前视野里全勾上了，按钮就变成「取消全选」
            _btnSelAll.Text = (total > 0 && n >= total) ? L10n.T("取消全选") : L10n.T("全选");
            _btnSelAll.Enabled = total > 0;
            _btnSelAll.FitWidth();

            _lblSel.Text = total == 0
                ? L10n.T("暂无照片")
                : L10n.F("已勾选 {0} / {1} 张", n, total);

            LayoutListTools();
        }

        /// <summary>
        /// 全选 / 取消全选。只作用于当前筛选出来的行 ——
        /// 被「只看待清理」藏起来的那批照片，不应该被这个按钮顺手勾上。
        /// </summary>
        /// <summary>给截图/自检入口用：等价于点一下「全选」按钮。</summary>
        internal void SelectAllForTest() { ToggleSelectAll(); }

        private void ToggleSelectAll()
        {
            if (_view == null || _view.Count == 0) return;

            var all = _view.Count(p => _checked.Contains(p.Name)) >= _view.Count;
            foreach (var p in _view)
            {
                if (all) _checked.Remove(p.Name);
                else _checked.Add(p.Name);
            }

            _list.Invalidate();     // 双缓冲下这一下就够了，不用逐行刷
            RefreshSelUi();
            UpdateMoveButtonText();
        }

        private void UpdateStat()
        {
            if (_result == null) return;
            int kept = _result.Items.Count(p => p.Status == StatusText.InAlbum);
            int del  = _result.Items.Count(p => p.Status == StatusText.Deleted);
            int sus  = _result.Items.Count(p => p.Status == StatusText.Suspect);
            int nw   = _result.Items.Count(p => p.Status == StatusText.TooNew);

            _lblStat.Text = L10n.F(
                "本地 {0} 张｜在册 {1}｜已删除 {2}｜疑似 {3}｜太新 {4}",
                _result.Items.Count, kept, del, sus, nw);

            _statKeep.Count = kept.ToString();
            _statDel.Count = del.ToString();
            _statSus.Count = sus.ToString();
            _statNew.Count = nw.ToString();

            if (_result.Blockers.Count > 0)
            {
                _lblHint.ForeColor = Theme.ClayDark;
                _lblHint.Text = L10n.F("⚠ {0}（本次仅可查看，禁止移动）", _result.Blockers[0]);
            }
            else if (_result.Hints.Count > 0)
            {
                _lblHint.ForeColor = Theme.AmberDeep;
                _lblHint.Text = "ⓘ " + _result.Hints[0];
            }
            else if (del > 0)
            {
                _lblHint.ForeColor = Theme.GreenDeep;
                _lblHint.Text = L10n.F("已自动勾选 {0} 张判定为「已删除」的照片，核对后可移入待清理文件夹。", del);
            }
            else
            {
                _lblHint.ForeColor = Theme.Text3;
                _lblHint.Text = L10n.T("勾选要清理的照片后点「移入待清理文件夹」；双击可看大图。");
            }
        }

        private void UpdateButtons()
        {
            var hasResult = _result != null;
            _btnMove.Enabled = hasResult;            // 始终可点，点了会给出具体原因
            _btnReport.Enabled = hasResult;
            _btnOpenPending.Enabled = true;
            _btnRestore.Enabled = true;
            UpdateMoveButtonText();
            RefreshSelUi();
        }

        private void AutoFitColumns()
        {
            // 列宽改为固定分配（文件名吃满剩余宽度），不再按内容自动撑开：
            // 自绘行高 38px、药丸宽度固定，跟着内容变会让每行都错位。
            LayoutColumns();
        }

        private void ShowPreview()
        {
            if (_list.SelectedItems.Count == 0)
            {
                _preview.Image = null;
                _lblInfo.ForeColor = Theme.Text3;
                _lblInfo.Text = L10n.T("选中一张照片，这里会显示大图与判定依据");
                return;
            }
            var p = (PhotoItem)_list.SelectedItems[0].Tag;

            try
            {
                _preview.Image = LoadImage(p.FullPath, 900);
            }
            catch { _preview.Image = null; }

            // 文件名在列表里就在旁边，这里不再重复，只留判定真正用得上的信息
            _lblInfo.Text = L10n.F(
                "状态：{0}　{1}\r\n拍摄：{2}\r\n判定：{3}",
                StatusText.Name(p.Status), FmtSize(p.Size),
                p.Time.ToString("yyyy-MM-dd HH:mm:ss"), StatusText.Reason(p.Status, p.MinAgeDays));
            _lblInfo.ForeColor = StatusText.ForeColor(p.Status);
        }

        private static Image LoadImage(string path, int maxSide)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var src = Image.FromStream(fs))
            {
                var ratio = Math.Min((double)maxSide / src.Width, (double)maxSide / src.Height);
                var w = Math.Max(1, (int)(src.Width * ratio));
                var h = Math.Max(1, (int)(src.Height * ratio));
                var bmp = new Bitmap(w, h);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, w, h);
                }
                return bmp;
            }
        }

        private static string FmtSize(long b)
        {
            if (b >= 1024 * 1024) return string.Format("{0:N1} MB", b / 1024.0 / 1024.0);
            if (b >= 1024) return string.Format("{0:N0} KB", b / 1024.0);
            return b + " B";
        }

        // ==================== 交互 ====================
        private void ListMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = _list.GetItemAt(e.X, e.Y);
            if (hit == null) return;
            if (!hit.Selected) { _list.SelectedItems.Clear(); hit.Selected = true; }

            var menu = new ContextMenuStrip();
            menu.Items.Add(L10n.T("打开原图"), null, (s, a) => OpenSelectedOriginal());
            menu.Items.Add(L10n.T("在资源管理器中定位"), null, (s, a) =>
            {
                var p = (PhotoItem)_list.SelectedItems[0].Tag;
                try { Process.Start("explorer.exe", "/select,\"" + p.FullPath + "\""); } catch { }
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L10n.T("复制文件名"), null, (s, a) =>
            {
                var p = (PhotoItem)_list.SelectedItems[0].Tag;
                try { Clipboard.SetText(p.Name); } catch { }
            });
            menu.Show(_list, e.Location);
        }

        private void OpenSelectedOriginal()
        {
            if (_list.SelectedItems.Count == 0) return;
            var p = (PhotoItem)_list.SelectedItems[0].Tag;
            try { Process.Start(p.FullPath); } catch { }
        }

        // ==================== 移动 ====================
        private void MoveToPending()
        {
            if (_result == null)
            {
                MessageBox.Show(this, L10n.T("还没有扫描结果。\n请先点右上角「重新扫描」。"),
                    L10n.T("暂无数据"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_result.Blockers.Count > 0)
            {
                MessageBox.Show(this,
                    L10n.F("安全检查未通过，本次不能移动：\n\n· {0}\n\n这几道检查是为了在你误判时保护照片。按提示处理后点「重新扫描」再试。",
                           string.Join("\n· ", _result.Blockers)),
                    L10n.T("已阻止"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var target = _result.Items.Where(p => _checked.Contains(p.Name)).ToList();

            if (target.Count == 0)
            {
                MessageBox.Show(this, BuildNoTargetMessage(), L10n.T("没有可移动的照片"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var pending = Mover.PendingDir(_screenShot);
            var tooNew = target.Count(p => p.Status == StatusText.TooNew);

            // 以前用 MessageBox：名字最多列 12 个，再多就只剩「以及另外 N 张」。
            // 一次上百张时，第 13 张往后根本没法核对，而那时恰恰最该看一眼。
            // 改成带滚动列表的窗口，有多少列多少，窗口高度不跟着涨。
            using (var dlg = new MoveDialog(target, pending, tooNew, MinAgeDays))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
            }

            var r = Mover.Move(target, _screenShot);
            if (!r.Ok)
            {
                MessageBox.Show(this, r.Error, L10n.T("移动失败"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try { Process.Start("explorer.exe", "\"" + r.PendingDir + "\""); } catch { }

            var done = L10n.F(
                "已移入 {0} 张。\n\n位置：\n{1}\n\n核对无误后，在弹出的文件夹里 Ctrl+A 全选、按 Delete 即可真正删除。\n移错了点「移回原目录」。",
                r.Moved.Count, r.PendingDir);
            if (r.RenamedCount > 0)
                done += L10n.F("\n\n其中 {0} 张因重名加了批次后缀（不影响还原）。", r.RenamedCount);
            if (r.Failed.Count > 0)
                done += L10n.F("\n\n失败 {0} 张：\n{1}", r.Failed.Count, string.Join("\n", r.Failed.Take(5)));

            MessageBox.Show(this, done, L10n.T("完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            StartScan(false);
        }

        /// <summary>一张都没勾时，把「为什么」讲清楚，而不是一声不响</summary>
        private string BuildNoTargetMessage()
        {
            int kept = _result.Items.Count(p => p.Status == StatusText.InAlbum);
            int del  = _result.Items.Count(p => p.Status == StatusText.Deleted);
            int sus  = _result.Items.Count(p => p.Status == StatusText.Suspect);
            int nw   = _result.Items.Count(p => p.Status == StatusText.TooNew);

            var sb = new StringBuilder();
            sb.AppendLine(L10n.T("当前没有勾选任何照片。"));
            sb.AppendLine();
            sb.AppendLine(L10n.T("判定为「已删除」需要同时满足："));
            sb.AppendLine(L10n.T("  1. 这张照片曾经出现在游戏相册的本地缓存里"));
            sb.AppendLine(L10n.T("  2. 现在本地缓存里已经没有它了"));
            sb.AppendLine();
            sb.AppendLine(L10n.F("本次扫描：在册 {0} 张｜已删除 {1} 张｜疑似 {2} 张｜太新 {3} 张", kept, del, sus, nw));

            if (del == 0)
            {
                sb.AppendLine();
                sb.AppendLine(L10n.T("如果你刚在游戏里删过照片："));
                sb.AppendLine(L10n.T("  · 回到游戏主界面或退出游戏，让缓存在本地落盘"));
                sb.AppendLine(L10n.T("  · 再点「重新扫描」，这时才会出现「已删除」项"));
                sb.AppendLine();
                sb.AppendLine(L10n.T("也可以在列表里手动勾选「疑似」的照片——"));
                sb.AppendLine(L10n.T("那等于你确认它已经不需要了，工具不替你判断。"));
            }

            return sb.ToString();
        }

        private void OpenLatestPending()
        {
            if (string.IsNullOrEmpty(_screenShot))
            {
                MessageBox.Show(this, L10n.T("还没有指定游戏截图目录。"), L10n.T("暂无"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dir = Mover.PendingDir(_screenShot);
            if (!Directory.Exists(dir))
            {
                MessageBox.Show(this,
                    L10n.F("还没有待清理文件夹。\n\n它的位置应该是：\n{0}\n\n勾选照片后点「移入待清理文件夹」才会生成。", dir),
                    L10n.T("暂无"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try { Process.Start("explorer.exe", "\"" + dir + "\""); } catch { }
        }

        // ==================== 还原 ====================
        private void RestoreLast()
        {
            if (!File.Exists(AppPaths.LastMovePath))
            {
                MessageBox.Show(this, L10n.T("没有还原记录（尚未执行过移动，或记录被清理过）。"),
                    L10n.T("无法还原"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rec = Mover.ReadRecord();
            if (rec.Target == null || rec.Source == null)
            {
                MessageBox.Show(this, L10n.T("移动记录格式无法识别，没法自动还原。\n可以手动到待清理文件夹里把照片拖回。"),
                    L10n.T("无法还原"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(rec.Target))
            {
                MessageBox.Show(this, L10n.T("待清理文件夹已经不存在了，无需还原。"), L10n.T("无法还原"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var exist = rec.Pairs.Count(kv => File.Exists(Path.Combine(rec.Target, kv.Key)));
            if (exist == 0)
            {
                MessageBox.Show(this, L10n.T("待清理文件夹里已经没有上次移动的照片了（可能已被你删除）。"),
                    L10n.T("无需还原"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this, L10n.F("把 {0} 张照片移回原目录？\n\n{1}", exist, rec.Source),
                    L10n.T("确认还原"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            int failed;
            int ok = Mover.Restore(rec, out failed);

            MessageBox.Show(this,
                L10n.F("已还原 {0} 张。", ok) + (failed > 0 ? "\n" + L10n.F("失败 {0} 张（可能被占用）。", failed) : ""),
                L10n.T("完成"), MessageBoxButtons.OK, MessageBoxIcon.Information);

            StartScan(false);
        }

        // ==================== 清理自身记录 ====================
        private void CleanRecords()
        {
            try
            {
                if (RecordCleaner.ShowDialog(this, _screenShot))
                {
                    // 台账或移动记录可能被清掉，重新扫描以刷新统计
                    if (!string.IsNullOrEmpty(_screenShot)) StartScan(false);
                    else UpdateButtons();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L10n.T("清理时出错：") + ex.Message, L10n.T("出错了"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== 报告 ====================
        private void GenerateReport()
        {
            if (_result == null) return;
            try
            {
                var path = Report.Write(_result, _screenShot, MinAgeDays);
                Process.Start(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L10n.T("生成报告失败：") + ex.Message, L10n.T("出错了"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ============================================================
    //  HTML 报告
    // ============================================================
    internal static class Report
    {
        public static string Write(ScanResult res, string screenShot, int minAge)
        {
            var sb = new StringBuilder();
            int del = res.Items.Count(p => p.Status == StatusText.Deleted);
            int sus = res.Items.Count(p => p.Status == StatusText.Suspect);
            int kept = res.Items.Count(p => p.Status == StatusText.InAlbum);
            int nw = res.Items.Count(p => p.Status == StatusText.TooNew);

            sb.AppendLine("<!DOCTYPE html><html lang=\"" + L10n.T("zh-CN") + "\"><head><meta charset=\"utf-8\">");
            sb.AppendLine("<title>" + Esc(L10n.T("无限暖暖 · 相册残留对账报告")) + "</title>");
            sb.AppendLine(@"<style>
*{box-sizing:border-box}
body{margin:0;padding:28px 24px 60px;background:#f5f6f8;color:#1f2328;font-family:""Microsoft YaHei"",""Segoe UI"",system-ui,sans-serif}
h1{font-size:21px;margin:0 0 6px;font-weight:600}
.sub{color:#6b7280;font-size:13px;margin-bottom:22px;line-height:1.7}
.stats{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:22px}
.stat{background:#fff;border:1px solid #e5e7eb;border-radius:10px;padding:12px 18px;min-width:112px}
.stat .n{font-size:22px;font-weight:600;line-height:1.2}
.stat .l{font-size:12px;color:#6b7280;margin-top:3px}
.n-del{color:#dc2626}.n-sus{color:#d97706}.n-keep{color:#059669}.n-new{color:#6b7280}
h2{font-size:15px;margin:26px 0 12px;padding-left:9px;border-left:3px solid #2563eb;font-weight:600}
.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(232px,1fr));gap:14px}
.card{background:#fff;border:1px solid #e5e7eb;border-radius:10px;overflow:hidden;box-shadow:0 1px 2px rgba(16,24,40,.04)}
.card.del{border-color:#fca5a5}.card.sus{border-color:#fcd34d}
.card img{width:100%;height:150px;object-fit:cover;display:block;background:#eee}
.meta{padding:9px 11px 11px}
.name{font-size:11px;color:#374151;word-break:break-all;line-height:1.45;font-family:Consolas,monospace}
.row{display:flex;justify-content:space-between;align-items:center;margin-top:6px;font-size:11px;color:#9ca3af}
.badge{font-size:11px;padding:1px 7px;border-radius:20px;font-weight:600}
.b-del{background:#fee2e2;color:#b91c1c}.b-sus{background:#fef3c7;color:#92400e}
.b-new{background:#e5e7eb;color:#4b5563}
.tip{background:#eff6ff;border:1px solid #bfdbfe;border-radius:9px;padding:12px 15px;font-size:12.5px;color:#1e40af;line-height:1.75;margin-bottom:20px}
.empty{color:#9ca3af;font-size:13px;padding:16px 0}
footer{margin-top:36px;font-size:11.5px;color:#9ca3af;border-top:1px solid #e5e7eb;padding-top:14px}
</style></head><body>");

            sb.AppendLine("<h1>" + Esc(L10n.T("相册残留对账报告")) + "</h1>");
            sb.AppendLine("<div class='sub'>" +
                          L10n.F("生成时间：{0}<br>截图目录：{1}",
                                 DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Esc(screenShot)) + "</div>");
            sb.AppendLine("<div class='stats'>");
            sb.AppendLine("  <div class='stat'><div class='n n-del'>" + del + "</div><div class='l'>" + Esc(L10n.T("相册已删除")) + "</div></div>");
            sb.AppendLine("  <div class='stat'><div class='n n-sus'>" + sus + "</div><div class='l'>" + Esc(L10n.T("疑似残留")) + "</div></div>");
            sb.AppendLine("  <div class='stat'><div class='n n-keep'>" + kept + "</div><div class='l'>" + Esc(L10n.T("仍在册")) + "</div></div>");
            sb.AppendLine("  <div class='stat'><div class='n n-new'>" + nw + "</div><div class='l'>" + Esc(L10n.T("太新跳过")) + "</div></div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"tip\">");
            sb.AppendLine(L10n.T("<b>怎么用这份报告：</b>「相册已删除」= 你在游戏里删掉了，本地还留着，可以清；<br>\n「疑似残留」= 相册缓存里没见过它，可能删过也可能只是没同步，<b>请先看图确认</b>；<br>\n「仍在册」不会出现在下面，也不会被处理。"));
            sb.AppendLine("</div>");

            var groups = new[]
            {
                new { Key = StatusText.Deleted, Title = "一、相册已删除，本地仍在（建议清理）", Cls = "del", Badge = "b-del" },
                new { Key = StatusText.Suspect, Title = "二、疑似残留（请人工确认后再决定）",   Cls = "sus", Badge = "b-sus" },
                new { Key = StatusText.TooNew,  Title = "三、太新已跳过（保护新照片）",         Cls = "",    Badge = "b-new" }
            };

            foreach (var g in groups)
            {
                var list = res.Items.Where(p => p.Status == g.Key).ToList();
                sb.AppendLine("<h2>" + Esc(L10n.F("{0} · {1} 张", L10n.T(g.Title), list.Count)) + "</h2>");
                if (list.Count == 0) { sb.AppendLine("<div class='empty'>" + Esc(L10n.T("没有这一类。")) + "</div>"); continue; }
                sb.AppendLine("<div class=\"grid\">");
                foreach (var p in list)
                {
                    var thumb = MakeThumb(p.FullPath);
                    var img = thumb != null
                        ? "<img src=\"file:///" + Esc(thumb.Replace('\\', '/')) + "\" loading=\"lazy\" alt=\"\">"
                        : "<div style='height:150px;display:flex;align-items:center;justify-content:center;background:#f3f4f6;color:#9ca3af;font-size:12px'>" + Esc(L10n.T("预览不可用")) + "</div>";
                    sb.AppendLine("<div class=\"card " + g.Cls + "\">" + img + "<div class=\"meta\">");
                    sb.AppendLine("<div class=\"name\">" + Esc(p.Name) + "</div>");
                    sb.AppendLine("<div class=\"row\"><span>" + p.Time.ToString("yyyy-MM-dd HH:mm") + " · " + FmtSize(p.Size) +
                                  "</span><span class=\"badge " + g.Badge + "\">" + Esc(StatusText.Name(p.Status)) + "</span></div>");
                    sb.AppendLine("</div></div>");
                }
                sb.AppendLine("</div>");
            }

            sb.AppendLine("<footer>" +
                          L10n.F("本报告由「无限暖暖 · 本地照片清理助手」生成，仅用于识别与核对，不会删除任何文件。<br>判定阈值：拍摄满 {0} 天；相册缓存目录 {1} 个。",
                                 minAge, res.AlbumDirs) +
                          "</footer>");
            sb.AppendLine("</body></html>");

            var path = Path.Combine(AppPaths.ReportDir, L10n.T("对账报告_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            try
            {
                foreach (var old in new DirectoryInfo(AppPaths.ReportDir).GetFiles("*.html")
                    .OrderByDescending(f => f.LastWriteTime).Skip(20))
                    old.Delete();
            }
            catch { }

            return path;
        }

        private static string MakeThumb(string src)
        {
            try
            {
                var name = System.Text.RegularExpressions.Regex.Replace(Path.GetFileName(src), @"[^\w\.\-]", "_") + ".jpg";
                var dst = Path.Combine(AppPaths.ThumbDir, name);
                if (File.Exists(dst)) return dst;

                using (var fs = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs))
                {
                    const int size = 300;
                    var ratio = Math.Min((double)size / img.Width, (double)size / img.Height);
                    var w = (int)(img.Width * ratio);
                    var h = (int)(img.Height * ratio);
                    using (var bmp = new Bitmap(size, size))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.FromArgb(238, 238, 238));
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(img, (size - w) / 2, (size - h) / 2, w, h);
                        bmp.Save(dst, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                }
                return dst;
            }
            catch { return null; }
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string FmtSize(long b)
        {
            if (b >= 1024 * 1024) return string.Format("{0:N1} MB", b / 1024.0 / 1024.0);
            if (b >= 1024) return string.Format("{0:N0} KB", b / 1024.0);
            return b + " B";
        }
    }
}
