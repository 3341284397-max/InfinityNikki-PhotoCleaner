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

            // 界面语言：先按命令行 / 系统语言定一版。设置文件要等数据位置确定后才读得到，
            // 所以这里用不读配置的那个入口，等选完位置再正式 Init 一次。
            L10n.InitNoConfig(ArgValue(argv, "--lang"));

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
                RunShot(argv[2], argv.Length >= 4 && argv[3] == "demo",
                        argv.Length >= 5 && argv[4] == "scroll",
                        (argv.Length >= 4 && argv[3] == "pending") ||
                        (argv.Length >= 5 && argv[4] == "pending"));
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
            if (argv.Length >= 4 && argv[1] == "--shotdata")
            {
                RunDataDialogShot(argv[2], argv[3]);
                return;
            }

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 首次启动先问数据放哪，再让主界面开工。
            // 顺序很重要：读设置文件会触发 AppPaths 解析，位置一旦按 exe 目录定下来就改不了了。
            AskWhereToKeepData();
            L10n.Init(ArgValue(argv, "--lang"));

            // 上次「更改位置 / 删除使用数据」没删干净的旧文件夹，趁现在还没有任何句柄补一刀。
            // 放在建主界面之前：那时程序还没打开过任何文件，一定删得掉。
            DataLocation.ProcessRemovalQueue();

            Application.Run(new MainForm());
        }

        /// <summary>
        /// 只有「全新环境」才问：没选过位置，exe 旁边也没有现成的数据文件夹。
        /// 老用户升级上来沿用原位置，不会看到这个窗口。
        /// </summary>
        private static void AskWhereToKeepData()
        {
            if (!DataLocation.AskNeeded) { AppPaths.Rebind(); return; }

            try
            {
                using (var dlg = new FirstRunDialog())
                {
                    var r = dlg.ShowDialog();
                    var chosen = dlg.ChosenBase;
                    // 用户直接关窗口＝不想现在决定，按默认位置（exe 旁边）记下来，别每次都问
                    if (r != DialogResult.OK || string.IsNullOrEmpty(chosen)) chosen = DataLocation.ExeDir;

                    DataLocation.TryRemember(chosen);
                }
            }
            catch
            {
                // 弹窗出问题不能挡住启动：退回 exe 同目录继续跑
            }
            finally
            {
                AppPaths.Rebind();
            }
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
        internal static void RunShot(string path, bool demo, bool scrollEnd = false, bool pendingView = false)
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

                // 需要时就切到「待清理」视图，截图里能看到两个胶囊和移回按钮
                if (pendingView) f.ShowPendingForTest();

                // 「已忽略」排在列表最底层，屏幕高度截不到，需要时先滚到底
                if (scrollEnd) f.ScrollListToEndForTest();

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
                    BaseStatus = (i % 6 == 5) ? StatusText.Suspect : StatusText.Deleted,
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

        /// <summary>隐藏入口：--shotdata 输出.png first|where —— 把「数据位置」相关窗口渲染成 PNG。</summary>
        internal static void RunDataDialogShot(string path, string which)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form f = null;
            try
            {
                f = string.Equals(which, "first", StringComparison.OrdinalIgnoreCase)
                    ? (Form)new FirstRunDialog()
                    : (Form)new DataLocationDialog();

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
            finally
            {
                if (f != null) f.Dispose();
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

        private static string _toolRoot;
        private static bool _rootReady;
        private static bool _english;
        private static bool _englishReady;

        /// <summary>
        /// 数据文件夹的父目录。位置由 DataLocation 决定（首次启动问一次，记在 %APPDATA%）。
        /// 1.8 起不再是 static readonly：用户可能在本次运行里改了位置，
        /// 缓存必须能被 Rebind() 打掉，否则改动要等下次启动才生效，甚至写回旧目录。
        /// </summary>
        public static string ToolRoot
        {
            get
            {
                if (!_rootReady)
                {
                    _toolRoot = DataLocation.BaseDir;
                    _rootReady = true;
                }
                return _toolRoot;
            }
        }

        /// <summary>
        /// 数据目录用英文名还是中文名。只看「已经存在哪一个」，不看当前界面语言：
        /// 目录一旦建好就锁定，之后切换语言不会换目录，台账和还原记录也就不会断。
        /// 两个都存在时中文优先（旧版本用户升级上来的情况）。
        /// </summary>
        public static bool DataIsEnglish
        {
            get
            {
                if (!_englishReady)
                {
                    _english = ResolveDataIsEnglish();
                    _englishReady = true;
                }
                return _english;
            }
        }

        /// <summary>位置变了之后让缓存失效，下一次读取按新位置重新判断。</summary>
        public static void Rebind()
        {
            _rootReady = false;
            _englishReady = false;
        }

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

        // 子项名字跟着数据目录走，不跟着界面语言走：保证切换语言后仍在读写同一批文件
        public static string DataDir
        {
            get { return Path.Combine(ToolRoot, DataIsEnglish ? DataFolderEn : DataFolderZh); }
        }

        public static string ThumbDir
        {
            get { return Path.Combine(DataDir, DataIsEnglish ? "thumbnails" : "缩略图缓存"); }
        }

        public static string ReportDir
        {
            get { return Path.Combine(DataDir, DataIsEnglish ? "reports" : "网页报告"); }
        }

        public static string LedgerPath
        {
            get { return Path.Combine(DataDir, DataIsEnglish ? "album-ledger.tsv" : "相册台账.tsv"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(DataDir, DataIsEnglish ? "settings.txt" : "设置.txt"); }
        }

        public static string LastMovePath
        {
            get { return Path.Combine(DataDir, DataIsEnglish ? "last-move.txt" : "最近一次移动记录.txt"); }
        }

        /// <summary>用户对单张照片的手工标记（忽略 / 确认是游戏截图）。</summary>
        public static string MarkPath
        {
            get { return Path.Combine(DataDir, DataIsEnglish ? "user-marks.tsv" : "用户标记.tsv"); }
        }

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
        /// <summary>
        /// 用户手工标的「忽略」。这是**标记**，不是判定：显示时它盖住状态列，
        /// 但 PhotoItem.BaseStatus 仍然是「它本来会被判成什么」。
        /// </summary>
        public const string Ignored = "Ignored";
        /// <summary>「待清理」视图里那些已经搬走的照片。只在那个视图出现。</summary>
        public const string Pending = "Pending";

        public static string Name(string s)
        {
            switch (s)
            {
                case InAlbum: return L10n.T("在册");
                case Deleted: return L10n.T("已删除");
                case Suspect: return L10n.T("疑似");
                case TooNew:  return L10n.T("太新");
                case Ignored: return L10n.T("已忽略");
                case Pending: return L10n.T("待清理");
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
                case Ignored: return L10n.T("你标记了「忽略」，不参与勾选与统计。可在工具栏取消");
                case Pending: return L10n.T("已经移入待清理文件夹。确认不要了就在资源管理器里删掉，想留着就勾选后移回原目录。");
                default:      return L10n.T("相册缓存中从未见过（可能删过，也可能尚未同步）");
            }
        }

        /// <summary>排序权重。传的是 BaseStatus —— 被标记的照片仍按它本来的判定站位。</summary>
        public static int Order(string s)
        {
            switch (s)
            {
                case Deleted: return 0;
                case Suspect: return 1;
                case TooNew:  return 2;
                case InAlbum: return 3;
                default:      return 4;
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
                case Ignored: return Theme.Text2;
                case Pending: return Theme.Text2;
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
        /// <summary>扫描时的「太新」阈值，用于按当前语言生成判定理由</summary>
        public int MinAgeDays;

        /// <summary>
        /// 程序自己的判定结果：在册 / 已删除 / 疑似 / 太新。
        /// 只由扫描和台账决定，用户打标记**不会**改写它 ——
        /// 1.8 之前把「已忽略」当成一种状态，标记一下原判定就丢了，
        /// 取消标记时只能靠猜（于是全部猜成「已删除」）。
        /// </summary>
        public string BaseStatus;

        /// <summary>
        /// 显示用状态：打了「忽略」标记就是「已忽略」，否则等于 BaseStatus。
        /// 标记与判定是两层东西，界面上分开显示（状态列 / 标记列）。
        /// </summary>
        public string Status { get { return Ignored ? StatusText.Ignored : BaseStatus; } }

        /// <summary>
        /// 外部导入：文件是被拷进 ScreenShot 的，不是游戏自己截的
        /// （判据：创建时间明显晚于修改时间）。和「已忽略」一样是标签，
        /// 可以叠加在任何状态上，不覆盖 BaseStatus。
        /// </summary>
        public bool External;

        /// <summary>用户是否对这张照片做过手工标记（忽略）。存在用户标记表里的才算。</summary>
        public bool Ignored;

        /// <summary>不参与勾选、统计与移动：已忽略或外部导入。</summary>
        public bool Excluded { get { return Ignored || External; } }

        /// <summary>
        /// 「待清理」视图用：移回原目录时该用的名字。
        /// 从移动记录里反查；查不到（例如用户自己拖进去的）就是文件现在的名字。
        /// </summary>
        public string OriginalName;
    }

    internal class LedgerEntry
    {
        public string FirstSeen;
        public string LastSeen;
        public bool EverInAlbum;

        /// <summary>
        /// 记录时的文件大小。同名但大小对不上，说明这张和当初记的不是同一份文件
        /// （例如原图被一个同名新截图顶替），必须当作新文件重新建档，
        /// 否则新照片会继承 EverInAlbum 被误判成「游戏里已删」。
        /// </summary>
        public long Size;
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
                    long size = 0;
                    // 第 5 列（大小）是 1.7.0 才加的：旧台账只有 4 列，读出来 Size=0，
                    // 表示「不知道」，此时不做同名覆盖校验，行为与旧版一致
                    if (f.Length >= 5) long.TryParse(f[4], out size);
                    l.Map[f[0]] = new LedgerEntry
                    {
                        FirstSeen = f[1],
                        LastSeen = f[2],
                        EverInAlbum = f[3] == "1",
                        Size = size
                    };
                }
            }
            catch { }
            return l;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 文件名\t首次进入相册\t最后见到\t是否在册\t大小");
            foreach (var k in Map.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var v = Map[k];
                sb.AppendLine(string.Join("\t", new[]
                {
                    k, v.FirstSeen, v.LastSeen, v.EverInAlbum ? "1" : "0", v.Size.ToString()
                }));
            }
            // 目录可能还没建（例如被外部清理掉），这里自保一次
            var dir = Path.GetDirectoryName(AppPaths.LedgerPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(AppPaths.LedgerPath, sb.ToString(), new UTF8Encoding(false));
        }
    }

    // ============================================================
    //  用户标记（忽略 / 将来可扩展「确认是游戏截图」）
    // ============================================================

    internal class UserMarkEntry
    {
        public string Name;
        public long Size;
        public bool Ignore;
        public string Time;

        /// <summary>标记文字。目前只有一种，留成方法便于以后加「确认是游戏截图」。</summary>
        public string KindText { get { return L10n.T("已忽略"); } }
    }

    /// <summary>
    /// 用户对单张照片的手工意志。和台账分开存：
    /// 台账是「程序观察到的事实」（自动维护，可以整份删掉重建），
    /// 这里是「用户说的话」（只能由用户自己改，程序绝不擅自丢）。
    ///
    /// key 用「文件名 + 大小」两个字段，不认单纯的文件名：
    /// 同名但大小变了，说明硬盘上已经是另一张照片，旧标记不该套到它头上。
    /// 这个方向是安全的 —— 宁可漏掉一条忽略，也不要误忽略一张本该清理的照片。
    /// </summary>
    internal class MarkStore
    {
        public readonly Dictionary<string, UserMarkEntry> Map =
            new Dictionary<string, UserMarkEntry>(StringComparer.OrdinalIgnoreCase);

        public static MarkStore Load()
        {
            var m = new MarkStore();
            if (!File.Exists(AppPaths.MarkPath)) return m;
            try
            {
                foreach (var line in File.ReadAllLines(AppPaths.MarkPath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    var f = line.Split('\t');
                    if (f.Length < 3) continue;
                    long size = 0;
                    long.TryParse(f[1], out size);
                    m.Map[f[0]] = new UserMarkEntry
                    {
                        Name = f[0],
                        Size = size,
                        Ignore = f[2].IndexOf('i') >= 0,
                        Time = f.Length >= 4 ? f[3] : ""
                    };
                }
            }
            catch { }
            return m;
        }

        /// <summary>命中的标记项；大小对不上则视为不是同一张照片，返回 null。</summary>
        public UserMarkEntry Hit(string name, long size)
        {
            UserMarkEntry e;
            if (!Map.TryGetValue(name, out e)) return null;
            if (e.Size != 0 && size != 0 && e.Size != size) return null;
            return e;
        }

        public bool IsIgnored(string name, long size)
        {
            var e = Hit(name, size);
            return e != null && e.Ignore;
        }

        public void SetIgnore(string name, long size, bool on)
        {
            if (on)
            {
                Map[name] = new UserMarkEntry
                {
                    Name = name,
                    Size = size,
                    Ignore = true,
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            else
            {
                Map.Remove(name);
            }
        }

        public int Count { get { return Map.Count; } }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 文件名\t大小\t标记(i=已忽略)\t标记时间");
            foreach (var k in Map.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var v = Map[k];
                sb.AppendLine(string.Join("\t", new[]
                {
                    k, v.Size.ToString(), v.Ignore ? "i" : "", v.Time ?? ""
                }));
            }
            var text = sb.ToString();

            var path = AppPaths.MarkPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // 改之前先把上一版留一份：用户手滑批量标记错了，还能从 .bak 捡回来
            try
            {
                if (File.Exists(path))
                {
                    var old = File.ReadAllText(path);
                    if (old != text) File.Copy(path, path + ".bak", true);
                }
            }
            catch { }

            File.WriteAllText(path, text, new UTF8Encoding(false));
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
        /// <summary>本次扫描读到的用户标记。界面直接复用，免得再读一遍文件。</summary>
        public MarkStore Marks;
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
            var marks = MarkStore.Load();
            res.Marks = marks;

            var extOk = new[] { ".jpg", ".jpeg", ".png" };
            var files = new DirectoryInfo(screenShotDir).GetFiles()
                .Where(f => extOk.Contains(f.Extension.ToLowerInvariant()))
                .ToList();

            foreach (var f in files)
            {
                // 同名但大小对不上 = 硬盘上已经是另一张照片（原图被同名新截图顶替）。
                // 必须把旧档案作废，否则新照片会白捡一条 EverInAlbum 被误判成「游戏里已删」。
                LedgerEntry prev;
                if (ledger.Map.TryGetValue(f.Name, out prev) && prev.Size != 0 && prev.Size != f.Length)
                {
                    ledger.Map.Remove(f.Name);
                    prev = null;
                }

                var inAlbum = albumNames.Contains(f.Name);
                var ever = prev != null && prev.EverInAlbum;

                string status;
                if (inAlbum) status = StatusText.InAlbum;
                else if (ever) status = StatusText.Deleted;
                else status = StatusText.Suspect;

                var age = (now - f.LastWriteTime).TotalDays;
                if (status != StatusText.InAlbum && age < minAgeDays) status = StatusText.TooNew;

                // 外部导入：游戏截图是「写完即定稿」，创建时间和修改时间几乎一致；
                // 从 U 盘/网盘/另一台机器拷进来的文件，修改时间保留原值、创建时间变成拷贝那一刻。
                // 这个判据是单边的（拷入只会让创建时间 > 修改时间），用图片软件改过图则相反，不会误中。
                var external = (f.CreationTime - f.LastWriteTime).TotalHours > 1.0;

                // 用户手工标记只是「贴一个标签」，不改写判定：
                // 取消标记时状态原样回来，不需要再猜一遍，也不用重扫。
                var ignored = marks.IsIgnored(f.Name, f.Length);

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
                    e.Size = f.Length;
                }
                else if (ledger.Map.ContainsKey(f.Name))
                {
                    ledger.Map[f.Name].LastSeen = now.ToString("yyyy-MM-dd HH:mm:ss");
                    ledger.Map[f.Name].Size = f.Length;
                }

                res.Items.Add(new PhotoItem
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    Size = f.Length,
                    Time = f.LastWriteTime,
                    AgeDays = Math.Round(age, 1),
                    BaseStatus = status,
                    MinAgeDays = minAgeDays,
                    External = external,
                    Ignored = ignored
                });
            }
            ledger.Save();

            // 按「本来会被判成什么」排，被标记的排在同一类的末尾：
            // 标记是附加信息，不该把照片从它本该在的位置挪走。
            res.Items = res.Items
                .OrderBy(p => StatusText.Order(p.BaseStatus))
                .ThenBy(p => p.Ignored ? 1 : 0)
                .ThenByDescending(p => p.Time)
                .ToList();

            // 3) 安全闸门
            if (res.AlbumCount == 0)
                res.Blockers.Add(L10n.T("游戏内相册缓存为空。可能是你清空过缓存，此时比对结果不可信。"));
            if (res.Items.Count >= 5)
            {
                // 用户已经表过态的照片（忽略）和明显不是游戏截图的（外部导入）不该参与比例判断，
                // 否则把 U 盘备份拷进来一次就会撞红线，真正该看的警告反而成了噪音
                var pool = res.Items.Where(p => !p.Excluded).ToList();
                var bad = pool.Count(p => p.BaseStatus == StatusText.Deleted || p.BaseStatus == StatusText.Suspect);
                if (pool.Count >= 5 && (double)bad / pool.Count > 0.7)
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

        /// <summary>
        /// 待清理文件夹里现在有哪些照片。
        /// 只列图片，别的文件（你自己丢进去的 txt 之类）当做没看见，不动它。
        /// </summary>
        public static List<PhotoItem> ScanPending(string screenShot)
        {
            var list = new List<PhotoItem>();
            if (string.IsNullOrEmpty(screenShot)) return list;

            var dir = PendingDir(screenShot);
            if (!Directory.Exists(dir)) return list;

            // 移动记录里存着「实际文件名 → 原来的文件名」，移回时按原名归位
            var origin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var kv in ReadRecord().Pairs) origin[kv.Key] = kv.Value;
            }
            catch { }

            var extOk = new[] { ".jpg", ".jpeg", ".png" };
            var now = DateTime.Now;
            try
            {
                foreach (var f in new DirectoryInfo(dir).GetFiles())
                {
                    if (!extOk.Contains(f.Extension.ToLowerInvariant())) continue;

                    string name;
                    if (!origin.TryGetValue(f.Name, out name) || string.IsNullOrEmpty(name)) name = f.Name;

                    list.Add(new PhotoItem
                    {
                        Name = f.Name,
                        OriginalName = name,
                        FullPath = f.FullName,
                        Size = f.Length,
                        // File.Move 保留修改时间，所以这里拿到的仍是拍摄时间，
                        // 和游戏截图那一栏的语义一致，列里不用换标题
                        Time = f.LastWriteTime,
                        AgeDays = Math.Round((now - f.LastWriteTime).TotalDays, 1),
                        BaseStatus = StatusText.Pending
                    });
                }
            }
            catch { }

            return list
                .OrderByDescending(p => p.Time)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>把勾选的待清理照片移回原目录。逐张来，一张失败不影响其他。</summary>
        public static RestoreResult RestoreSelected(List<PhotoItem> items, string screenShot)
        {
            var r = new RestoreResult();
            if (items == null || items.Count == 0 || string.IsNullOrEmpty(screenShot)) return r;

            var dir = PendingDir(screenShot);
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in items)
            {
                try
                {
                    if (!File.Exists(p.FullPath))
                    {
                        r.Skipped.Add(p.Name + L10n.T(" — 文件已不存在"));
                        continue;
                    }

                    var origin = string.IsNullOrEmpty(p.OriginalName) ? p.Name : p.OriginalName;
                    var to = Path.Combine(screenShot, origin);

                    if (File.Exists(to))
                    {
                        // 原目录里已经有一份同样大小的同名文件：多半是上次没移干净，
                        // 再搬一份过去只会多出个「_还原」副本，跳过更干净。
                        long len = 0;
                        try { len = new FileInfo(to).Length; } catch { }
                        if (len == p.Size)
                        {
                            r.Skipped.Add(p.Name + L10n.T(" — 原目录里已有同一张，跳过"));
                            done.Add(p.Name);
                            continue;
                        }

                        // 同名但不是同一张：两份都留着，重命名而不是覆盖
                        to = Path.Combine(screenShot,
                            Path.GetFileNameWithoutExtension(origin) + L10n.T("_还原") +
                            DateTime.Now.ToString("HHmmss") + Path.GetExtension(origin));
                    }

                    File.Move(p.FullPath, to);
                    r.Ok++;
                    done.Add(p.Name);
                }
                catch (Exception ex)
                {
                    r.Failed.Add(p.Name + " — " + ex.Message);
                }
            }

            if (done.Count > 0) DropFromRecord(done);
            TryRemoveEmptyDir(dir);
            return r;
        }

        /// <summary>把已经还回去的条目从移动记录里划掉，免得「整批还原」再搬一遍。</summary>
        private static void DropFromRecord(HashSet<string> actualNames)
        {
            try
            {
                var path = AppPaths.LastMovePath;
                if (!File.Exists(path)) return;

                var kept = new List<string>();
                var moved = 0;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("MOVE\t"))
                    {
                        var f = line.Split('\t');
                        if (f.Length >= 2 && actualNames.Contains(f[1])) { moved++; continue; }
                    }
                    kept.Add(line);
                }
                if (moved == 0) return;

                // 一条 MOVE 都不剩了：整份记录没用了，删掉
                if (!kept.Any(l => l.StartsWith("MOVE\t"))) { File.Delete(path); return; }
                File.WriteAllLines(path, kept, new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>待清理文件夹空了就删掉，别在 ScreenShot 里留个空壳。</summary>
        public static void TryRemoveEmptyDir(string dir)
        {
            try
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) &&
                    Directory.GetFiles(dir).Length == 0 &&
                    Directory.GetDirectories(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch { }
        }
    }

    internal class RestoreResult
    {
        public int Ok;
        public readonly List<string> Failed = new List<string>();
        public readonly List<string> Skipped = new List<string>();
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
        private SoftButton _btnData;
        private PillToggle _chkOnlyPending;

        /// <summary>
        /// 当前看的是哪个文件夹：false = 游戏截图目录，true = 待清理文件夹。
        /// 两个视图共用同一张列表和预览，只是数据来源不同 ——
        /// 这样「看大图确认一下再移回」就不必跳出程序去资源管理器。
        /// </summary>
        private bool _pendingMode;
        private List<PhotoItem> _pending = new List<PhotoItem>();
        private PillToggle _pillShot, _pillPending;
        private StatCard _statPendCount, _statPendSize;
        private Panel _toolbar, _statsRow;
        /// <summary>切换视图时把两个胶囊调成互斥，避免「两个都亮」或事件互相打回。</summary>
        private bool _viewSyncing;
        private ListView _list;
        private ListHeader _listHeader;
        private Panel _listTools;
        private Label _lblSel;
        private SoftButton _btnSelAll;
        private SoftButton _btnMark, _btnUnmark, _btnByStatus;
        private StatCard _statKeep, _statDel, _statSus, _statNew;
        private PictureBox _preview;
        private Label _lblInfo;
        private Label _lblStat;

        /// <summary>扫描后发现「已删除」但一张都没勾时出现的顶部黄条</summary>
        private CardPanel _noticeBar;
        private Label _lblNotice;
        private SoftButton _btnNoticeAll;
        private SoftButton _btnNoticeClose;
        /// <summary>用户点了黄条上的 ×，本次扫描内不再出现</summary>
        private bool _noticeDismissed;
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

            // 数据位置：放在语言按钮左边。它不是高频操作，但必须找得到 ——
            // 有人要挪走数据、有人要卸载，都只会来这里找一次。
            _btnData = new SoftButton(L10n.T("数据位置"), BtnKind.Ghost, 88) { Height = 28 };
            _btnData.Font = Theme.F(8.6f);
            _btnData.Click += (s, e) => ShowDataLocation();
            var tipData = new ToolTip();
            tipData.SetToolTip(_btnData, L10n.T("看看使用数据存在哪、挪走它、删掉它，或者彻底卸载。"));

            header.Controls.Add(_btnData);
            header.Controls.Add(_btnLang);
            header.Resize += (s, e) =>
            {
                _btnLang.Left = Math.Max(0, header.Width - _btnLang.Width);
                _btnLang.Top = 19;

                _btnData.Left = Math.Max(0, _btnLang.Left - S(6) - _btnData.Width);
                _btnData.Top = 19;
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
            _statsRow = statsRow;
            _statKeep = new StatCard { Accent = Theme.StatusAccent(StatusText.InAlbum), Label = L10n.T("仍在相册") };
            _statDel = new StatCard { Accent = Theme.StatusAccent(StatusText.Deleted), Label = L10n.T("游戏里已删") };
            _statSus = new StatCard { Accent = Theme.StatusAccent(StatusText.Suspect), Label = L10n.T("无法确认") };
            _statNew = new StatCard { Accent = Theme.StatusAccent(StatusText.TooNew), Label = L10n.T("太新") };
            statsRow.Controls.Add(_statKeep);
            statsRow.Controls.Add(_statDel);
            statsRow.Controls.Add(_statSus);
            statsRow.Controls.Add(_statNew);

            // 待清理视图下的两张卡。四个判定在这边没意义 ——
            // 那些照片已经搬出来了，再显示「在册 80 / 已删除 12」只会让人以为切换失灵。
            _statPendCount = new StatCard { Accent = Theme.Clay, Label = L10n.T("待清理"), Visible = false };
            _statPendSize = new StatCard { Accent = Theme.Amber, Label = L10n.T("占用"), Visible = false };
            statsRow.Controls.Add(_statPendCount);
            statsRow.Controls.Add(_statPendSize);

            statsRow.Resize += (s, e) => LayoutStats(statsRow);

            // 点卡片＝批量勾选这一类。用户本来就盯着这四张卡看，
            // 到这里找「把那 12 张都选上」比回头去工具栏下拉里翻要顺手得多。
            // 「仍在相册」故意不做成可点：那批照片游戏里还有，不该有任何一键全选的入口。
            var tipKeep = new ToolTip();
            tipKeep.SetToolTip(_statKeep, L10n.T("游戏相册里还有这些照片，正常情况下不需要清理"));
            BindStatClick(_statDel,  StatusText.Deleted, L10n.T("游戏里已删"));
            BindStatClick(_statSus,  StatusText.Suspect, L10n.T("无法确认"));
            BindStatClick(_statNew,  StatusText.TooNew,  L10n.T("太新"));

            // ================= 工具条 =================
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.Bg };
            _toolbar = toolbar;

            // 两个胶囊当视图切换用：点哪个亮哪个，互斥。
            // 原来的「移回原目录」是照着移动记录整批搬，看不见东西、也没法挑；
            // 现在能直接看到待清理文件夹里有哪些照片，挑好了再移回。
            _pillShot = new PillToggle(L10n.T("游戏截图"));
            _pillShot.Checked = true;
            _pillShot.CheckedChanged += (s, e) => { if (_pillShot.Checked) SetView(false); };

            _pillPending = new PillToggle(L10n.T("待清理"));
            _pillPending.CheckedChanged += (s, e) => { if (_pillPending.Checked) SetView(true); };

            _chkOnlyPending = new PillToggle(L10n.T("只看待处理项"));
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
            toolbar.Controls.Add(_pillPending);
            toolbar.Controls.Add(_pillShot);
            toolbar.Resize += (s, e) => LayoutToolbar();

            var tip = new ToolTip();
            tip.SetToolTip(_chkOnlyPending, L10n.T("只列出需要你判断的：已删除 / 疑似 / 太新。仍在相册与已忽略的会隐藏"));
            tip.SetToolTip(_pillShot, L10n.T("看游戏截的图，也就是 ScreenShot 文件夹"));
            tip.SetToolTip(_pillPending, L10n.T("看待清理文件夹里的照片。勾选后可以移回原目录"));

            // ================= 顶部黄条 =================
            // 1.7.0 起一张都不预勾选。安全感换来了「得自己去找」，
            // 这条黄条就是补偿：扫完直接告诉你「有 N 张」，想省事点右边一下，想稳妥就自己挑。
            _noticeBar = new CardPanel
            {
                Dock = DockStyle.Top,
                Height = S(46),
                Inset = 6,
                Radius = 12,
                Fill = Theme.YellowSoft,
                Border = Theme.YellowLine,
                PadX = 0,
                PadY = 0,
                Visible = false
            };

            _lblNotice = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Theme.F(8.6f),
                ForeColor = Theme.YellowDeep,
                BackColor = Color.Transparent,
                Text = ""
            };

            _btnNoticeAll = new SoftButton(L10n.T("全部勾选"), BtnKind.Ghost, S(78));
            _btnNoticeAll.Font = Theme.F(8.4f);
            _btnNoticeAll.Height = S(26);
            _btnNoticeAll.PadX = 11;
            _btnNoticeAll.FitWidth();
            _btnNoticeAll.Click += (s, e) => SelectAllDeleted();

            _btnNoticeClose = new SoftButton("✕", BtnKind.Ghost, S(24));
            _btnNoticeClose.Font = Theme.F(7.8f);
            _btnNoticeClose.Height = S(24);
            _btnNoticeClose.PadX = 5;
            _btnNoticeClose.FitWidth();
            _btnNoticeClose.Click += (s, e) => { _noticeDismissed = true; UpdateNotice(); };

            _noticeBar.Controls.Add(_lblNotice);
            _noticeBar.Controls.Add(_btnNoticeAll);
            _noticeBar.Controls.Add(_btnNoticeClose);
            _noticeBar.Resize += (s, e) => LayoutNotice();

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
                "", L10n.T("状态"), L10n.T("标记"), L10n.T("文件名"), L10n.T("拍摄时间"), L10n.T("大小")
            }, 4)
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
            _list.Columns.Add(L10n.T("标记"), 76);
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

            // 手工标记：给「程序猜不准」的照片一个出口。
            // 典型场景是 U 盘里的历史备份被拷回 ScreenShot —— 名字和台账里一样，
            // 会被判成「游戏里已删」，但它们在游戏里根本不存在，不该被清理。
            _btnMark = ListToolButton(L10n.T("标记忽略"), S(74));
            _btnMark.Click += (s, e) => SetIgnoreOnChecked(true);

            _btnUnmark = ListToolButton(L10n.T("取消忽略"), S(74));
            _btnUnmark.Click += (s, e) => SetIgnoreOnChecked(false);

            // 按状态批量勾选：八个动作塞进一个下拉，工具栏才放得下
            _btnByStatus = ListToolButton(L10n.T("按状态") + " ▾", S(78));
            _btnByStatus.Click += (s, e) => ShowByStatusMenu();

            _listTools.Controls.Add(_lblSel);
            _listTools.Controls.Add(_btnSelAll);
            _listTools.Controls.Add(_btnByStatus);
            _listTools.Controls.Add(_btnUnmark);
            _listTools.Controls.Add(_btnMark);
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
            // 同一个主按钮，两个视图两个方向：截图目录里是「搬出去」，待清理里是「搬回来」
            _btnMove.Click += (s, e) => { if (_pendingMode) MoveBackToScreenShot(); else MoveToPending(); };
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
            root.Controls.Add(_noticeBar);
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

        /// <summary>工具条从左往右摆：视图胶囊 → 筛选 → 右边统计文字。</summary>
        private void LayoutToolbar()
        {
            if (_toolbar == null || _lblStat == null) return;

            var x = 6;
            if (_pillShot != null)
            {
                _pillShot.FitWidth();
                _pillShot.SetBounds(x, 6, _pillShot.Width, 30);
                x += _pillShot.Width + 8;
            }
            if (_pillPending != null)
            {
                _pillPending.FitWidth();
                _pillPending.SetBounds(x, 6, _pillPending.Width, 30);
                x += _pillPending.Width + 14;
            }
            if (_chkOnlyPending != null)
            {
                _chkOnlyPending.FitWidth();
                _chkOnlyPending.SetBounds(x, 6, _chkOnlyPending.Width, 30);
            }

            var half = Math.Max(120, _toolbar.Width / 2);
            _lblStat.SetBounds(_toolbar.Width - 6 - half, 6, half, 30);
        }

        /// <summary>切换「游戏截图」/「待清理」两个视图。</summary>
        private void SetView(bool pending)
        {
            // 两个胶囊是互斥的：点哪个哪个亮。设 Checked 会再触发一次事件，
            // 所以这里先挡住，不要让两个处理函数互相打回。
            if (_viewSyncing) return;
            _viewSyncing = true;
            try
            {
                if (_pillShot != null) _pillShot.Checked = !pending;
                if (_pillPending != null) _pillPending.Checked = pending;
            }
            finally { _viewSyncing = false; }

            if (_pendingMode == pending)
            {
                // 重复点了当前视图，当成「刷新一下」——待清理文件夹可能刚被外部改动
                if (pending) ReloadPending();
                RebindList();
                UpdateStat();
                return;
            }

            _pendingMode = pending;
            _checked.Clear();
            if (pending) ReloadPending();

            _statKeep.Visible = _statDel.Visible = _statSus.Visible = _statNew.Visible = !pending;
            _statPendCount.Visible = _statPendSize.Visible = pending;

            RebindList();
            UpdateStat();
            UpdateNotice();
            UpdateButtons();
            LayoutStats(_statsRow);
            LayoutToolbar();
        }

        private void ReloadPending()
        {
            _pending = string.IsNullOrEmpty(_screenShot)
                ? new List<PhotoItem>()
                : Mover.ScanPending(_screenShot);
        }

        /// <summary>待清理视图里勾选的照片移回游戏截图目录。</summary>
        private void MoveBackToScreenShot()
        {
            if (!_pendingMode) return;

            var picked = _pending.Where(p => _checked.Contains(p.Name)).ToList();

            // 一张都没勾就问一句要不要全搬回去 —— 多数人切过来就是为了整体撤回
            if (picked.Count == 0)
            {
                if (_pending.Count == 0) return;
                var all = MessageBox.Show(this,
                    L10n.F("一张都没勾。要把这 {0} 张全部移回原目录吗？", _pending.Count),
                    L10n.T("全部移回"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (all != DialogResult.OK) return;
                picked = _pending.ToList();
            }

            var r = Mover.RestoreSelected(picked, _screenShot);

            var msg = L10n.F("已移回 {0} 张。", r.Ok);
            if (r.Skipped.Count > 0)
                msg += "\n\n" + L10n.F("跳过 {0} 张：", r.Skipped.Count) + "\n" +
                       string.Join("\n", r.Skipped.Take(6).ToArray()) +
                       (r.Skipped.Count > 6 ? L10n.F("\n…还有 {0} 张", r.Skipped.Count - 6) : "");
            if (r.Failed.Count > 0)
                msg += "\n\n" + L10n.F("失败 {0} 张：", r.Failed.Count) + "\n" +
                       string.Join("\n", r.Failed.Take(6).ToArray());

            MessageBox.Show(this, msg, L10n.T("移回完成"), MessageBoxButtons.OK,
                r.Failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            _checked.Clear();
            ReloadPending();
            if (_pending.Count == 0) { SetView(false); return; }   // 搬空了就回截图视图

            RebindList();
            UpdateStat();
            UpdateButtons();
        }

        private void LayoutStats(Panel row)
        {
            if (row == null) return;
            const int gap = 6;

            // 两个视图各用各的卡：截图目录看四个判定，待清理文件夹看张数和占用
            var cards = _pendingMode
                ? new[] { _statPendCount, _statPendSize }
                : new[] { _statKeep, _statDel, _statSus, _statNew };

            var w = (row.Width - gap * 2 * cards.Length) / cards.Length;
            if (w < 40) return;
            var x = gap;
            foreach (var c in cards)
            {
                if (c == null) continue;
                c.SetBounds(x, 6, w, row.Height - 12);
                x += w + gap * 2;
            }
        }

        /// <summary>统计卡点击 = 勾选 / 取消勾选某一类的全部行（被排除的跳过）。</summary>
        private void BindStatClick(StatCard card, string status, string label)
        {
            card.Hoverable = true;
            card.Cursor = Cursors.Hand;

            var tip = new ToolTip();
            tip.SetToolTip(card, L10n.F("点一下勾选全部「{0}」，再点一下取消", label));

            card.Click += (s, e) =>
            {
                if (_view == null || _view.Count == 0) return;
                var pool = _view.Where(p => p.BaseStatus == status && !p.Excluded).ToList();
                if (pool.Count == 0) return;

                var all = pool.Count(p => _checked.Contains(p.Name)) >= pool.Count;
                foreach (var p in pool)
                {
                    if (all) _checked.Remove(p.Name);
                    else _checked.Add(p.Name);
                }

                _list.Invalidate();
                RefreshSelUi();
                UpdateMoveButtonText();
            };
        }

        private void LayoutColumns()
        {
            if (_list == null || _list.Columns.Count < 6) return;
            var scale = DeviceDpi / 96f;

            // 时间列按实际文字量算，不同 DPI / 字体下都不会被截成「10-20 23…」
            var timeW = TextRenderer.MeasureText("10-20 23:26", Theme.F(8.6f)).Width + 22;
            var baseTimeW = (int)(96 * scale);
            if (timeW < baseTimeW) timeW = baseTimeW;

            var c0 = (int)(34 * scale);
            var c1 = TextRenderer.MeasureText(StatusText.Name(StatusText.Suspect), Theme.F(8.2f)).Width + 30;
            if (c1 < (int)(62 * scale)) c1 = (int)(62 * scale);

            // 标记列可能同时出现两枚徽章（已忽略 + 外部导入），按两枚实测宽度留位
            var c2 = TagWidth(L10n.T("已忽略")) + TagWidth(L10n.T("外部导入"));
            if (c2 < (int)(58 * scale)) c2 = (int)(58 * scale);

            var c5 = TextRenderer.MeasureText("000.0 MB", Theme.F(8.6f)).Width + 24;
            if (c5 < (int)(58 * scale)) c5 = (int)(58 * scale);

            // 一定要预留垂直滚动条宽度：ListView 出现滚动条时不保证触发 Resize，
            // 按「当前」ClientSize 算满会溢出，冒出横向滚动条并把「大小」列裁掉。
            var scroll = SystemInformation.VerticalScrollBarWidth;
            var fixedW = c0 + c1 + c2 + timeW + c5;

            // 逐列设宽度，每设一次 ListView 就重排一遍并重画；包在 BeginUpdate 里只重画一次
            _list.BeginUpdate();
            try
            {
                _list.Columns[0].Width = c0;
                _list.Columns[1].Width = c1;
                _list.Columns[2].Width = c2;
                _list.Columns[4].Width = timeW;
                _list.Columns[5].Width = c5;

                var nameW = _list.ClientSize.Width - fixedW - scroll - 2;
                if (nameW < 120) nameW = 120;
                _list.Columns[3].Width = nameW;
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

        /// <summary>「数据位置」：看一眼、挪走、删掉、彻底卸载。</summary>
        private void ShowDataLocation()
        {
            using (var dlg = new DataLocationDialog(_screenShot))
            {
                dlg.ShowDialog(this);
                if (!dlg.Changed) return;
            }

            // 数据可能刚被删掉或搬走了：重新扫一次，别让界面停在一份已经不存在的台账上
            if (Headless || DemoMode) return;
            if (string.IsNullOrEmpty(_screenShot)) { SetEmpty(L10n.T("尚未指定游戏目录")); return; }

            _checked.Clear();
            StartScan(false);
            RebindList();
            UpdateStat();
        }

        /// <summary>--shot demo 用：灌一批示意数据，不开游戏也能看真实排版。</summary>
        private void LoadDemoData()
        {
            // 一行 = 名称 / 判定 / 拍了几天 / 外部导入 / 标了忽略
            // 注意「已忽略」不是判定，而是标在「已删除」上面的标签 ——
            // 演示数据也照这个来，才能看出状态列与标记列是并排的两件事。
            var specs = new[]
            {
                new object[] { "2026_10_20_21_24_47_2143244.jpeg", StatusText.Deleted, 9.0,  false, false },
                new object[] { "2026_10_19_18_03_12_2138771.jpeg", StatusText.Deleted, 10.0, false, false },
                new object[] { "2026_10_17_11_42_58_2129903.jpeg", StatusText.Deleted, 12.0, false, false },
                new object[] { "2026_10_16_09_15_33_2124418.jpeg", StatusText.Deleted, 13.0, true,  false },
                new object[] { "2026_10_12_20_31_07_2116730.jpeg", StatusText.Suspect, 17.0, false, false },
                new object[] { "2026_10_11_22_08_44_2115092.jpeg", StatusText.Suspect, 18.0, false, false },
                new object[] { "2026_10_29_08_12_05_2160140.jpeg", StatusText.TooNew, 0.4,  false, false },
                new object[] { "2026_10_28_19_55_31_2158802.jpeg", StatusText.TooNew, 1.1,  false, false },
                new object[] { "2026_10_09_14_22_41_2113208.jpeg", StatusText.Deleted, 20.0, false, true  },
                new object[] { "2026_10_08_09_50_17_2111944.jpeg", StatusText.Deleted, 21.0, true,  true  },
                new object[] { "2026_10_24_15_20_09_2151277.jpeg", StatusText.InAlbum, 5.0,  false, false },
                new object[] { "2026_10_23_12_47_26_2149864.jpeg", StatusText.InAlbum, 6.0,  false, false },
                new object[] { "2026_10_22_17_33_51_2148205.jpeg", StatusText.InAlbum, 7.0,  false, false },
                new object[] { "2026_10_21_10_06_14_2146922.jpeg", StatusText.InAlbum, 8.0,  false, false }
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
                    BaseStatus = (string)s[1],
                    External = (bool)s[3],
                    Ignored = (bool)s[4],
                    MinAgeDays = 3
                });
            }

            items = items
                .OrderBy(p => StatusText.Order(p.BaseStatus))
                .ThenBy(p => p.Ignored ? 1 : 0)
                .ThenByDescending(p => p.Time)
                .ToList();

            _screenShot = dir;
            _txtPath.Text = dir;
            _result = new ScanResult { Items = items, AlbumCount = 44, AlbumDirs = 3, Marks = new MarkStore() };

            // 待清理文件夹也灌几张，好把「切过去、勾选、移回」这条路径也看一遍。
            // 走的是真实的 Mover.ScanPending，不是另一套假数据。
            try
            {
                var pend = Mover.PendingDir(dir);
                Directory.CreateDirectory(pend);
                for (var i = 0; i < 5; i++)
                {
                    var f = Path.Combine(pend, "2026_10_0" + (i + 1) + "_10_00_00_21099" + i + "0.jpeg");
                    if (!File.Exists(f)) DemoImage(f, 40 + i);
                }
            }
            catch { }

            _pending = Mover.ScanPending(dir);

            // 与真机一致：一张都不预勾选，黄条负责提示
            _checked.Clear();
            _noticeDismissed = false;

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
                // 状态列只报「程序判成了什么」。你有没有打标记，标记列单独说 ——
                // 两件事并排放在一起，才看得出「它本来是已删除，被我忽略了」。
                Theme.StatusPalette(p.BaseStatus, out bg, out fore, out border);
                var text = StatusText.Name(p.BaseStatus);
                var tw = TextRenderer.MeasureText(text, Theme.F(8.2f)).Width;
                var pill = new Rectangle(cell.Left + 2, mid - 11, Math.Min(tw + 20, cell.Width - 6), 22);
                Theme.DrawRound(g, pill, 11, bg, border);
                Theme.Str(g, text, Theme.F(8.2f), fore, pill, Theme.CenterMid);
                return;
            }

            if (e.ColumnIndex == 2)
            {
                // 标记列：这两件事互相独立，一张照片可以两个都占
                //（从 U 盘拷回来的备份，多半既「外部导入」又被你标了「已忽略」）。
                if (!p.Ignored && !p.External) return;

                var x = cell.Left + 4;
                if (p.Ignored)
                    x = DrawTag(g, x, mid, L10n.T("已忽略"), Theme.GreenSoft, Theme.GreenLine, Theme.GreenDeep);
                if (p.External)
                    DrawTag(g, x, mid, L10n.T("外部导入"), Theme.LineSoft, Theme.Line, Theme.Text2);
                return;
            }

            if (e.ColumnIndex == 3)
            {
                var r = new Rectangle(cell.Left + 4, cell.Top, cell.Width - 8, cell.Height);
                var fore = p.Ignored ? Theme.Text3 : (e.Item.Selected ? Theme.Text : Theme.Text2);
                Theme.Str(g, p.Name, Theme.F(8.9f), fore, r, Theme.LeftMid);
                return;
            }

            if (e.ColumnIndex == 4)
            {
                var r = new Rectangle(cell.Left, cell.Top, cell.Width - 8, cell.Height);
                Theme.Str(g, p.Time.ToString("MM-dd HH:mm"), Theme.F(8.6f), Theme.Text3, r, Theme.RightMid);
                return;
            }

            var r2 = new Rectangle(cell.Left, cell.Top, cell.Width - 12, cell.Height);
            Theme.Str(g, FmtSize(p.Size), Theme.F(8.6f), Theme.Text3, r2, Theme.RightMid);
        }

        /// <summary>标记列里的一枚小徽章。返回下一枚的起点 x（含右侧间距）。</summary>
        private static int DrawTag(Graphics g, int x, int mid, string label, Color fill, Color border, Color fore)
        {
            var f = Theme.F(7.6f);
            var w = TextRenderer.MeasureText(label, f).Width + 14;
            var pill = new Rectangle(x, mid - 10, w, 20);
            Theme.DrawRound(g, pill, 10, fill, border);
            Theme.Str(g, label, f, fore, pill, Theme.CenterMid);
            return x + w + 5;
        }

        /// <summary>一枚徽章占多宽（含间距），列宽按它算，两枚并排也不会被裁掉。</summary>
        private static int TagWidth(string label)
        {
            return TextRenderer.MeasureText(label, Theme.F(7.6f)).Width + 23;
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
                ReloadPending();   // 待清理文件夹的数量显示在这里定，切换视图不会再读盘

                // 1.7.0 起不再自动勾选任何照片。以前「已删除」默认全勾，
                // 用户从 U 盘把备份拷回来时，这批文件名字和台账里一致、同样判成「已删除」，
                // 一扫描就整批预勾上，手滑点一次「移入待清理」就把备份搬走了。
                // 现在一律不勾，改由顶部黄条提示「发现 N 张，点这里一键勾选」。
                _checked.Clear();
                _noticeDismissed = false;   // 新一轮扫描，黄条重新有机会出现

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
            if (_result == null && !_pendingMode) return;

            if (_pendingMode)
            {
                // 待清理视图不套「只看待处理项」：这里的照片全都已经搬出来了，
                // 再按判定筛一遍没意义，只会出现「明明有 23 张却只显示 3 张」。
                _view = _pending.ToList();
            }
            else
            {
                // 「待处理」= 需要你判断的：仍在相册的和已经标了忽略的都不算
                _view = _chkOnlyPending.Checked
                    ? _result.Items.Where(p => p.BaseStatus != StatusText.InAlbum && !p.Ignored).ToList()
                    : _result.Items.ToList();
            }

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var p in _view)
            {
                // 第一列留给自绘的勾选圈，文字从第二列开始；显示内容全部由 PhotoItem 现画
                var it = new ListViewItem("");
                it.SubItems.Add(StatusText.Name(p.BaseStatus));

                // 标记列：可以同时有两枚，所以用 · 连起来（实际绘制在 OnDrawSubItem）
                var marks = new List<string>();
                if (p.Ignored) marks.Add(L10n.T("已忽略"));
                if (p.External) marks.Add(L10n.T("外部导入"));
                it.SubItems.Add(string.Join(" · ", marks.ToArray()));

                it.SubItems.Add(p.Name);
                it.SubItems.Add(p.Time.ToString("yyyy-MM-dd HH:mm"));
                it.SubItems.Add(FmtSize(p.Size));
                it.Tag = p;

                // 中文界面下文件名列放不下 30+ 字符，悬停给出完整名字；
                // 被标记过的再补一句原因，用户不必点开预览也知道它为什么被排除
                var tip = new List<string> { p.Name };
                if (p.Ignored) tip.Add(L10n.T("已标记忽略，不参与勾选与统计"));
                if (p.External) tip.Add(L10n.T("看起来是外部拷入的照片，默认不勾选"));
                if (_pendingMode && p.OriginalName != null &&
                    !string.Equals(p.OriginalName, p.Name, StringComparison.OrdinalIgnoreCase))
                    tip.Add(L10n.F("移回时会用原名 {0}", p.OriginalName));
                it.ToolTipText = string.Join("\n", tip.ToArray());

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
            if (_pendingMode)
            {
                _btnMove.Text = n > 0
                    ? L10n.F("移回原目录（{0}）", n)
                    : L10n.T("移回原目录");
            }
            else
            {
                _btnMove.Text = n > 0
                    ? L10n.F("移入待清理文件夹（{0}）", n)
                    : L10n.T("移入待清理文件夹");
            }
            LayoutBottom();
        }

        private int S(int v) { return (int)Math.Round(v * (DeviceDpi / 96f)); }

        /// <summary>工具条：按钮贴右，报数靠左吃掉剩下的宽度。</summary>
        private void LayoutListTools()
        {
            if (_listTools == null || _btnSelAll == null || _lblSel == null) return;

            // 从右往左摆：全选 → 按状态 → 取消忽略 → 标记忽略，剩下的给报数文字
            var order = new[] { _btnSelAll, _btnByStatus, _btnUnmark, _btnMark };
            var gap = S(6);
            var x = _listTools.Width - S(2);
            foreach (var b in order)
            {
                if (b == null) continue;
                x -= b.Width;
                b.SetBounds(x, (_listTools.Height - b.Height) / 2, b.Width, b.Height);
                x -= gap;
            }
            _lblSel.SetBounds(S(4), 0, Math.Max(S(40), x - S(4)), _listTools.Height);
        }

        /// <summary>勾选工具条上的小按钮，样式统一收在这里。</summary>
        private SoftButton ListToolButton(string text, int width)
        {
            var b = new SoftButton(text, BtnKind.Ghost, width);
            b.Font = Theme.F(8.4f);
            b.Height = S(26);
            b.PadX = 11;
            b.FitWidth();
            return b;
        }

        /// <summary>标记 / 取消标记 只在「勾选的照片里确实有这一类的」时可点。</summary>
        private void UpdateMarkButtons()
        {
            if (_btnMark == null || _btnUnmark == null) return;

            // 待清理视图里没有「程序判定」这回事，标记无从谈起
            if (_pendingMode)
            {
                _btnMark.Enabled = false;
                _btnUnmark.Enabled = false;
                _btnByStatus.Enabled = false;
                return;
            }

            var picked = _view == null ? 0 : _view.Count(p => _checked.Contains(p.Name));
            var markable = _view == null ? 0 : _view.Count(p => _checked.Contains(p.Name) && !p.Ignored);
            var unmarkable = _view == null ? 0 : _view.Count(p => _checked.Contains(p.Name) && p.Ignored);

            _btnMark.Enabled = picked > 0 && markable > 0;
            _btnUnmark.Enabled = picked > 0 && unmarkable > 0;
            _btnByStatus.Enabled = _view != null && _view.Count > 0;
        }

        /// <summary>把当前勾选的照片标为已忽略 / 取消忽略，然后重新扫描式地刷新状态。</summary>
        private void SetIgnoreOnChecked(bool on)
        {
            if (_result == null || _result.Marks == null) return;
            if (DemoMode) return;   // 演示模式只画界面，不往真实的用户标记文件里写东西

            var targets = _view.Where(p => _checked.Contains(p.Name) && (on ? !p.Ignored : p.Ignored)).ToList();
            if (targets.Count == 0) return;

            // 防手滑：一次标一大片，基本是「按状态全选」之后顺手点的。
            // 取消标记是往安全方向走，不拦；只有「标为忽略」需要确认一次。
            if (on)
            {
                var total = Math.Max(1, _result.Items.Count);
                if (targets.Count >= 20 || targets.Count * 3 > total)
                {
                    var r = MessageBox.Show(this,
                        L10n.F("要把这 {0} 张（共 {1} 张）都标记为「已忽略」吗？\n\n" +
                               "标记后它们不会被勾选、不会被移动，也不再计入待删统计。\n" +
                               "之后随时可以用「取消忽略」还原。",
                               targets.Count, total),
                        L10n.T("确认批量标记"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);   // 默认落在「取消」上
                    if (r != DialogResult.OK) return;
                }
            }

            foreach (var p in targets)
            {
                _result.Marks.SetIgnore(p.Name, p.Size, on);
                p.Ignored = on;      // BaseStatus 一个字都不动，取消标记即原样还原
            }
            _result.Marks.Save();

            // 标记只是改标签，不重新读盘；排序会变，所以整表重绑一次
            _result.Items = _result.Items
                .OrderBy(p => StatusText.Order(p.BaseStatus))
                .ThenBy(p => p.Ignored ? 1 : 0)
                .ThenByDescending(p => p.Time)
                .ToList();

            RebindList();
            UpdateStat();
            UpdateNotice();
        }

        /// <summary>「按状态 ▾」：八个批量勾选动作。</summary>
        private void ShowByStatusMenu()
        {
            if (_view == null || _view.Count == 0) return;

            var menu = new ContextMenuStrip { Font = Theme.F(8.8f), ShowImageMargin = false };

            Action<string, bool, string> add = (status, on, caption) =>
            {
                var item = new ToolStripMenuItem(caption);
                item.Click += (s, e) => SelectByStatus(status, on);
                menu.Items.Add(item);
            };

            add(StatusText.Deleted, true,  L10n.T("勾选全部「游戏里已删」"));
            add(StatusText.Deleted, false, L10n.T("取消勾选全部「游戏里已删」"));
            menu.Items.Add(new ToolStripSeparator());
            add(StatusText.TooNew, true,   L10n.T("勾选全部「太新」"));
            add(StatusText.TooNew, false,  L10n.T("取消勾选全部「太新」"));
            menu.Items.Add(new ToolStripSeparator());
            add(StatusText.Suspect, true,  L10n.T("勾选全部「无法确认」"));
            add(StatusText.Suspect, false, L10n.T("取消勾选全部「无法确认」"));
            menu.Items.Add(new ToolStripSeparator());
            add(StatusText.Ignored, true,  L10n.T("勾选全部「已忽略」"));
            add(StatusText.Ignored, false, L10n.T("取消勾选全部「已忽略」"));

            var below = _btnByStatus.PointToScreen(new Point(0, _btnByStatus.Height + 2));
            menu.Show(below);
        }

        /// <summary>
        /// 按状态批量勾选。作用范围是「当前筛选出来的行」，
        /// 被「只看待处理项」藏起来的那批不会被顺手勾上。
        /// </summary>
        private void SelectByStatus(string status, bool on)
        {
            if (_view == null) return;

            var hit = _view.Where(p => (status == StatusText.Ignored ? p.Ignored : p.BaseStatus == status)
                                       && (on || _checked.Contains(p.Name)));
            foreach (var p in hit)
            {
                if (on)
                {
                    // 被标记排除的不批量勾上，否则「按状态全选 + 移动」等于绕过标记；
                    // 唯独「已忽略」这一类要能选中 —— 选上它才好点「取消忽略」。
                    if (p.Excluded && status != StatusText.Ignored) continue;
                    _checked.Add(p.Name);
                }
                else _checked.Remove(p.Name);
            }

            _list.Invalidate();
            RefreshSelUi();
            UpdateMoveButtonText();
        }

        /// <summary>刷新「已勾选 12 / 128 张」与全选按钮的措辞。</summary>
        private void RefreshSelUi()
        {
            if (_btnSelAll == null || _lblSel == null) return;

            // 分母只算「能移动的」：已忽略和外部导入本来就勾不上，
            // 把它们算进总数会让「全选」按钮永远显示成「全选」，永远变不成「取消全选」
            var pool = _view == null ? new List<PhotoItem>() : _view.Where(p => !p.Excluded).ToList();
            var total = pool.Count;
            var n = total == 0 ? 0 : pool.Count(p => _checked.Contains(p.Name));

            // 当前视野里全勾上了，按钮就变成「取消全选」
            _btnSelAll.Text = (total > 0 && n >= total) ? L10n.T("取消全选") : L10n.T("全选");
            _btnSelAll.Enabled = total > 0;
            _btnSelAll.FitWidth();

            _lblSel.Text = total == 0
                ? L10n.T("暂无照片")
                : L10n.F("已勾选 {0} / {1} 张", n, total);

            LayoutListTools();
            UpdateNotice();
            UpdateMarkButtons();
        }

        /// <summary>黄条内部布局：文字吃左边，右边依次是「全部勾选」和关闭。</summary>
        private void LayoutNotice()
        {
            if (_noticeBar == null || _lblNotice == null) return;
            var pad = _noticeBar.Inset + 10;
            var right = _noticeBar.Width - _noticeBar.Inset - 8;
            if (right < pad + 40) return;

            var c = _btnNoticeClose;
            c.SetBounds(right - c.Width, (_noticeBar.Height - c.Height) / 2, c.Width, c.Height);
            right -= c.Width + 2;

            var a = _btnNoticeAll;
            a.SetBounds(right - a.Width, (_noticeBar.Height - a.Height) / 2, a.Width, a.Height);
            right -= a.Width + 8;

            _lblNotice.SetBounds(pad, _noticeBar.Inset,
                Math.Max(40, right - pad), _noticeBar.Height - _noticeBar.Inset * 2);
        }

        /// <summary>按当前勾选情况决定黄条要不要出现。</summary>
        private void UpdateNotice()
        {
            if (_noticeBar == null) return;

            var del = _result == null ? 0
                : _result.Items.Count(p => p.BaseStatus == StatusText.Deleted && !p.Ignored);
            var picked = _result == null ? 0
                : _result.Items.Count(p => p.BaseStatus == StatusText.Deleted && !p.Ignored &&
                                           _checked.Contains(p.Name));

            // 一张都没勾才算「还没开始」；勾过任何一张就不再打扰。
            // 没有「已删除」的照片、或用户点了 ×、或正在看待清理视图，都不显示 ——
            // 那边已经全是搬出去的照片，再喊「发现 N 张」只会指错文件夹。
            var show = !_pendingMode && del > 0 && picked == 0 && !_noticeDismissed;

            if (show)
                _lblNotice.Text = L10n.F("发现 {0} 张「游戏里已删」，一张都还没勾。要清理就点右边。", del);

            if (_noticeBar.Visible != show) _noticeBar.Visible = show;
            if (show) LayoutNotice();
        }

        /// <summary>黄条上的「全部勾选」：只勾「已删除」，被排除的一律跳过。</summary>
        private void SelectAllDeleted()
        {
            if (_result == null) return;
            foreach (var p in _result.Items.Where(p => p.BaseStatus == StatusText.Deleted && !p.Excluded))
                _checked.Add(p.Name);

            _list.Invalidate();
            RefreshSelUi();
            UpdateMoveButtonText();
        }

        /// <summary>
        /// 全选 / 取消全选。只作用于当前筛选出来的行 ——
        /// 被「只看待清理」藏起来的那批照片，不应该被这个按钮顺手勾上。
        /// </summary>
        /// <summary>给截图/自检入口用：等价于点一下「全选」按钮。</summary>
        internal void SelectAllForTest() { ToggleSelectAll(); }

        /// <summary>给截图入口用：滚到列表最后一行，方便确认「已忽略」这种沉底的状态。</summary>
        /// <summary>给截图入口用：切到「待清理」视图。</summary>
        internal void ShowPendingForTest()
        {
            if (!_pendingMode) SetView(true);
            Application.DoEvents();
        }

        internal void ScrollListToEndForTest()
        {
            if (_list == null || _list.Items.Count == 0) return;
            _list.EnsureVisible(_list.Items.Count - 1);
            Application.DoEvents();
        }

        private void ToggleSelectAll()
        {
            if (_view == null || _view.Count == 0) return;

            // 只作用在「允许被移动」的行上：已忽略和外部导入的不参与，
            // 否则「全选 + 移动」就成了绕过标记的后门。
            var pool = _view.Where(p => !p.Excluded).ToList();
            if (pool.Count == 0) return;

            var all = pool.Count(p => _checked.Contains(p.Name)) >= pool.Count;
            foreach (var p in pool)
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
            if (_pendingMode)
            {
                long bytes = 0;
                foreach (var p in _pending) bytes += p.Size;

                _statPendCount.Count = _pending.Count.ToString();
                _statPendSize.Count = FmtSize(bytes);

                _pillShot.Text = L10n.T("游戏截图");
                _pillPending.Text = L10n.F("待清理 {0}", _pending.Count);

                _lblStat.Text = L10n.F("待清理文件夹 {0} 张｜共 {1}", _pending.Count, FmtSize(bytes));
                _lblHint.ForeColor = Theme.Text3;
                _lblHint.Text = _pending.Count == 0
                    ? L10n.T("待清理文件夹是空的。")
                    : L10n.T("勾选要留下的照片后点「移回原目录」；这张列表里的照片不在游戏截图目录里。");

                LayoutToolbar();
                _chkOnlyPending.Enabled = false;
                return;
            }

            _chkOnlyPending.Enabled = true;
            _pillShot.Text = _result == null
                ? L10n.T("游戏截图")
                : L10n.F("游戏截图 {0}", _result.Items.Count);
            _pillPending.Text = L10n.F("待清理 {0}", _pending.Count);
            LayoutToolbar();

            if (_result == null) return;

            // 四个数字按「程序本来判成什么」来数：这样 在册 + 已删除 + 疑似 + 太新 = 本地总数，
            // 被标记排除的张数单独报一句。原来把「已忽略」算成第五类，四项加起来对不上总数。
            int kept = _result.Items.Count(p => p.BaseStatus == StatusText.InAlbum);
            int del = _result.Items.Count(p => p.BaseStatus == StatusText.Deleted);
            int sus = _result.Items.Count(p => p.BaseStatus == StatusText.Suspect);
            int nw = _result.Items.Count(p => p.BaseStatus == StatusText.TooNew);
            int cut = _result.Items.Count(p => p.Excluded);

            var head = L10n.F(
                "本地 {0} 张｜在册 {1}｜已删除 {2}｜疑似 {3}｜太新 {4}",
                _result.Items.Count, kept, del, sus, nw);
            // 被排除的张数单独报一句：不然卡上写「已删除 12」、实际只能勾 9 张，对不上账
            _lblStat.Text = cut > 0 ? head + L10n.F("｜排除 {0}", cut) : head;

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
            else
            {
                // 不再单独报「有 N 张已删除」——顶部黄条已经说了同一件事，
                // 底部再说一遍只会跟右侧一排按钮抢地方，把句子挤成两行。
                _lblHint.ForeColor = Theme.Text3;
                _lblHint.Text = L10n.T("勾选要清理的照片后点「移入待清理文件夹」；双击可看大图。");
            }
        }

        private void UpdateButtons()
        {
            var hasResult = _result != null;
            if (_pendingMode)
            {
                _btnMove.Enabled = _pending.Count > 0;    // 一张都没有就没什么可移回的
                _btnReport.Enabled = false;               // 报告讲的是截图目录的对账结果
                _btnRestore.Enabled = false;              // 主按钮已经能按勾选移回，这条整批还原是重复入口
            }
            else
            {
                _btnMove.Enabled = hasResult;             // 始终可点，点了会给出具体原因
                _btnReport.Enabled = hasResult;
                _btnRestore.Enabled = true;
            }
            _btnOpenPending.Enabled = true;
            UpdateMoveButtonText();
            RefreshSelUi();
            UpdateMarkButtons();
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
            var reason = StatusText.Reason(p.BaseStatus, p.MinAgeDays);
            if (_pendingMode && p.OriginalName != null &&
                !string.Equals(p.OriginalName, p.Name, StringComparison.OrdinalIgnoreCase))
                reason = L10n.F("移回时的文件名：{0}", p.OriginalName) + "\r\n" + reason;
            if (p.Ignored)
                reason += "\r\n" + L10n.T("你标记了「忽略」：不参与勾选与统计；取消标记后会回到原来的判定");
            if (p.External)
                reason += "\r\n" + L10n.T("同时标记为「外部导入」：像是从外部拷进来的，默认不勾选");

            _lblInfo.Text = L10n.F(
                "状态：{0}　{1}\r\n拍摄：{2}\r\n判定：{3}",
                StatusText.Name(p.BaseStatus), FmtSize(p.Size),
                p.Time.ToString("yyyy-MM-dd HH:mm:ss"), reason);
            _lblInfo.ForeColor = StatusText.ForeColor(p.BaseStatus);
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

            // 被标记忽略 / 判为外部导入的一律不搬 —— 即使用户把它们勾上了。
            // 勾选圈仍然允许点，是为了让人能勾上去取消标记，不是让人绕过标记搬走。
            var target = _result.Items
                .Where(p => _checked.Contains(p.Name) && !p.Excluded)
                .ToList();

            if (target.Count == 0)
            {
                var excluded = _result.Items.Count(p => _checked.Contains(p.Name) && p.Excluded);
                var msg = excluded > 0
                    ? L10n.F("勾选的 {0} 张都被标记为「已忽略」或「外部导入」，不会移动。\n\n要移动它们，先取消标记。", excluded)
                    : BuildNoTargetMessage();
                MessageBox.Show(this, msg, L10n.T("没有可移动的照片"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var pending = Mover.PendingDir(_screenShot);
            var tooNew = target.Count(p => p.BaseStatus == StatusText.TooNew);

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

            // 搬走之后待清理视图的内容也变了，顺手刷一下，免得切过去还是旧列表
            ReloadPending();
            StartScan(false);
        }

        /// <summary>一张都没勾时，把「为什么」讲清楚，而不是一声不响</summary>
        private string BuildNoTargetMessage()
        {
            if (_result == null) return L10n.T("当前没有勾选任何照片。");

            int kept = _result.Items.Count(p => p.BaseStatus == StatusText.InAlbum);
            int del  = _result.Items.Count(p => p.BaseStatus == StatusText.Deleted);
            int sus  = _result.Items.Count(p => p.BaseStatus == StatusText.Suspect);
            int nw   = _result.Items.Count(p => p.BaseStatus == StatusText.TooNew);

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
            // 报告里的数字按判定数，和下面三个分组口径一致（被忽略的不计入）
            int del = res.Items.Count(p => p.BaseStatus == StatusText.Deleted && !p.Ignored);
            int sus = res.Items.Count(p => p.BaseStatus == StatusText.Suspect && !p.Ignored);
            int kept = res.Items.Count(p => p.BaseStatus == StatusText.InAlbum);
            int nw = res.Items.Count(p => p.BaseStatus == StatusText.TooNew && !p.Ignored);

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
                // 你标了「忽略」的不列进来：这份报告是给「要不要删」做参考的，
                // 把用户已经表过态的照片再推一遍，等于没听见他的话。
                var list = res.Items.Where(p => p.BaseStatus == g.Key && !p.Ignored).ToList();
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
                                  "</span><span class=\"badge " + g.Badge + "\">" + Esc(StatusText.Name(p.BaseStatus)) + "</span></div>");
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
