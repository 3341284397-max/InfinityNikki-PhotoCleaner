using System;
using System.IO;
using System.Linq;
using System.Text;

namespace NikkiPhotoCleaner
{
    /// <summary>测试驱动。dotnet NikkiTest.dll &lt;命令&gt; [参数]</summary>
    internal static class Runner
    {
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }

            // 测试可用 NIKKI_LANG=zh|en 固定界面语言
            L10n.Init(Environment.GetEnvironmentVariable("NIKKI_LANG"));

            if (args.Length == 0) { Console.WriteLine("用法: <scan|move|restore|full|survey|lang> ..."); return 2; }

            switch (args[0])
            {
                case "paths":
                    Console.WriteLine("ToolRoot=" + AppPaths.ToolRoot);
                    Console.WriteLine("DataDir=" + AppPaths.DataDir);
                    Console.WriteLine("Pending=" + AppPaths.PendingFolderName);
                    return 0;

                case "shot":
                    // SAC 拦未签名 exe，用 dotnet 跑同一份 UI 代码出图
                    UI(args[1], args.Length > 2 && args[2] == "demo");
                    return 0;

                case "dlg":
                    DialogUI(args[1], args.Length > 2 ? args[2] : ".");
                    return 0;

                case "lang":
                    // lang <ss> <zh|en>：验证切换语言后路径与判定是否连续
                    Console.Write(Lang(args[1], args[2]));
                    return 0;

                case "scan":
                    Console.Write(Scan(args[1], 0));
                    return 0;

                case "scan3":
                    Console.Write(Scan(args[1], 3));
                    return 0;

                case "move":
                    Console.Write(Program.RunMoveTest(args[1]));
                    return 0;

                case "pick":
                    // pick <ss> <name1> [name2] ...
                    Console.Write(Pick(args[1], args.Skip(2).ToArray()));
                    return 0;

                case "restore":
                    Console.Write(Restore());
                    return 0;

                case "survey":
                    Console.Write(Survey(args[1]));
                    return 0;
            }

            Console.WriteLine("未知命令: " + args[0]);
            return 2;
        }

        /// <summary>
        /// 把主窗口渲染成 PNG。WinForms 要 STA，而 .NET 控制台主线程是 MTA，
        /// 所以另开一条 STA 线程跑整个界面生命周期。
        /// </summary>
        private static void UI(string path, bool demo)
        {
            Exception err = null;
            var th = new System.Threading.Thread(() =>
            {
                try { Program.RunShot(path, demo); }
                catch (Exception ex) { err = ex; }
            });
            th.SetApartmentState(System.Threading.ApartmentState.STA);
            th.Start();
            th.Join();

            if (err != null) Console.WriteLine("截图失败：" + err);
            Console.WriteLine(File.Exists(path) ? "截图已写出 " + path : "截图未生成");
        }

        /// <summary>把「清理记录」对话框渲染成 PNG，同样需要 STA。</summary>
        private static void DialogUI(string path, string screenShot)
        {
            Exception err = null;
            var th = new System.Threading.Thread(() =>
            {
                try { Program.RunDialogShot(path, screenShot); }
                catch (Exception ex) { err = ex; }
            });
            th.SetApartmentState(System.Threading.ApartmentState.STA);
            th.Start();
            th.Join();

            if (err != null) Console.WriteLine("截图失败：" + err);
            Console.WriteLine(File.Exists(path) ? "截图已写出 " + path : "截图未生成");
        }

        private static string Lang(string ss, string code)
        {
            L10n.Init(code);

            var r = Scanner.Run(ss, 0);
            var sb = new StringBuilder();
            sb.AppendLine("lang=" + L10n.Tag);
            sb.AppendLine("DataDir=" + AppPaths.DataDir);
            sb.AppendLine("Ledger=" + AppPaths.LedgerPath);
            sb.AppendLine("Pending=" + Mover.PendingDir(ss) + "  [预计生成名: " + AppPaths.PendingFolderName + "]");
            sb.AppendLine("counts=InAlbum:" + r.Items.Count(p => p.Status == StatusText.InAlbum) +
                          " Deleted:" + r.Items.Count(p => p.Status == StatusText.Deleted) +
                          " Suspect:" + r.Items.Count(p => p.Status == StatusText.Suspect));
            // 顺便验证界面文案确实换成了英文
            sb.AppendLine("label.Deleted=" + StatusText.Name(StatusText.Deleted));
            return sb.ToString();
        }

        private static string Scan(string ss, int minAge)
        {
            var r = Scanner.Run(ss, minAge);
            var sb = new StringBuilder();
            foreach (var p in r.Items) sb.AppendLine("  " + StatusText.Name(p.Status).PadRight(6) + p.Name);
            sb.AppendLine("  闸门=" + (r.Blockers.Count == 0 ? "无" : string.Join(" / ", r.Blockers)));
            sb.AppendLine("  提示=" + (r.Hints.Count == 0 ? "无" : string.Join(" / ", r.Hints)));
            return sb.ToString();
        }

        private static string Pick(string ss, string[] names)
        {
            var r = Scanner.Run(ss, 0);
            var target = r.Items.Where(p => names.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            var mv = Mover.Move(target, ss);

            var sb = new StringBuilder();
            sb.AppendLine("选中=" + target.Count + " 移入=" + mv.Moved.Count +
                          " 改名=" + mv.RenamedCount + " 失败=" + mv.Failed.Count);
            foreach (var f in mv.Failed) sb.AppendLine("  ! " + f);
            foreach (var kv in mv.Pairs)
                if (!string.Equals(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine("  改名: " + kv.Value + " -> " + kv.Key);
            sb.AppendLine("目录内=" + (Directory.Exists(mv.PendingDir) ? Directory.GetFiles(mv.PendingDir).Length : -1));
            sb.AppendLine("ScreenShot剩余=" + Directory.GetFiles(ss, "*.jpeg").Length);
            return sb.ToString();
        }

        private static string Restore()
        {
            var rec = Mover.ReadRecord();
            var sb = new StringBuilder();
            sb.AppendLine("记录条数=" + rec.Count);

            int failed;
            int ok = Mover.Restore(rec, out failed);
            sb.AppendLine("还原=" + ok + " 失败=" + failed);
            sb.AppendLine("记录文件仍在=" + File.Exists(AppPaths.LastMovePath));
            if (rec.Target != null) sb.AppendLine("待清理目录仍在=" + Directory.Exists(rec.Target));
            return sb.ToString();
        }

        private static string Survey(string ss)
        {
            var items = RecordCleaner.Survey(ss);
            var sb = new StringBuilder();
            foreach (var i in items)
                sb.AppendLine(string.Format("{0,-8} 数量={1,-4} 大小={2,-10} 危险={3,-6} {4}",
                    i.Key, i.Count, RecordCleaner.Size(i.Bytes), i.Risky, i.Note));
            return sb.ToString();
        }
    }
}
