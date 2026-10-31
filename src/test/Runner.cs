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
                    UI(args[1], args.Length > 2 && args[2] == "demo",
                       args.Length > 3 && args[3] == "pending");
                    return 0;

                case "dlg":
                    DialogUI(args[1], args.Length > 2 ? args[2] : ".");
                    return 0;

                case "shotdata":
                    // shotdata <png> first|where：数据位置相关的两个窗口
                    DialogShot(args[1], args.Length > 2 ? args[2] : "where");
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

                case "tag":
                    // tag <ss>：列出每张的状态与标记，并给出汇总，
                    // 用来断言「外部导入」「已忽略」是否按预期生效
                    Console.Write(Tag(args[1]));
                    return 0;

                case "ignore":
                    // ignore <ss> [name...]：不给名字就是全部标记
                    Console.Write(SetIgnore(args[1], args.Skip(2).ToArray(), true));
                    return 0;

                case "unignore":
                    Console.Write(SetIgnore(args[1], args.Skip(2).ToArray(), false));
                    return 0;

                case "mkexternal":
                    // mkexternal <ss> <name>：把文件改成「外部拷入」的时间特征
                    // （修改时间留在过去、创建时间是现在），模拟从 U 盘拷回来的备份
                    Console.Write(MakeExternal(args[1], args[2]));
                    return 0;

                case "where":
                    // where：打印数据根目录是怎么定下来的，验证「指针文件」与回落逻辑
                    Console.Write(Where());
                    return 0;

                case "sibling":
                    // sibling <截图目录>：验证「与游戏文件夹并列」那一级推导是否正确
                    Console.Write(DataLocation.SiblingOf(args.Length > 1 ? args[1] : null) ?? "(null)");
                    return 0;

                case "pending":
                    // pending <ss>：列出待清理文件夹里的照片，以及移回时会用的原名
                    Console.Write(Pending(args[1]));
                    return 0;

                case "moveback":
                    // moveback <ss> [name...]：把勾选的（或不给名字则全部）移回截图目录
                    Console.Write(MoveBack(args[1], args.Skip(2).ToArray()));
                    return 0;

                case "movedata":
                    // movedata <新父目录>：搬数据文件夹，验证「复制 → 核对 → 删旧」
                    Console.Write(MoveData(args[1]));
                    return 0;

                case "deltada":
                    // deltada：删数据文件夹本身，验证删完文件夹真的不见了
                    Console.Write(DeleteData());
                    return 0;
            }

            Console.WriteLine("未知命令: " + args[0]);
            return 2;
        }

        /// <summary>
        /// 把主窗口渲染成 PNG。WinForms 要 STA，而 .NET 控制台主线程是 MTA，
        /// 所以另开一条 STA 线程跑整个界面生命周期。
        /// </summary>
        private static void UI(string path, bool demo, bool pending = false)
        {
            Exception err = null;
            var th = new System.Threading.Thread(() =>
            {
                try { Program.RunShot(path, demo, false, pending); }
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

        private static void DialogShot(string path, string which)
        {
            Exception err = null;
            var th = new System.Threading.Thread(() =>
            {
                try { Program.RunDataDialogShot(path, which); }
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

        /// <summary>每张的判定 + 标记，末尾给一行汇总，方便脚本里 grep 断言。</summary>
        private static string Tag(string ss)
        {
            var r = Scanner.Run(ss, 0);
            var sb = new StringBuilder();
            foreach (var p in r.Items)
            {
                var tags = p.External && p.Ignored ? "外部导入+已忽略"
                         : p.External ? "外部导入"
                         : p.Ignored ? "已忽略"
                         : "";
                // 判定列永远报 BaseStatus：标记是另一栏的事，不能把它盖掉
                sb.AppendLine("  " + StatusText.Name(p.BaseStatus).PadRight(6) +
                              p.Name + (tags.Length > 0 ? "  [" + tags + "]" : ""));
            }
            sb.AppendLine("counts=InAlbum:" + r.Items.Count(p => p.BaseStatus == StatusText.InAlbum) +
                          " Deleted:" + r.Items.Count(p => p.BaseStatus == StatusText.Deleted) +
                          " Suspect:" + r.Items.Count(p => p.BaseStatus == StatusText.Suspect) +
                          " TooNew:" + r.Items.Count(p => p.BaseStatus == StatusText.TooNew) +
                          " Ignored:" + r.Items.Count(p => p.Ignored) +
                          " External:" + r.Items.Count(p => p.External) +
                          " Excluded:" + r.Items.Count(p => p.Excluded) +
                          " Marks:" + (r.Marks == null ? 0 : r.Marks.Count));
            sb.AppendLine("闸门=" + (r.Blockers.Count == 0 ? "无" : string.Join(" / ", r.Blockers)));
            return sb.ToString();
        }

        private static string SetIgnore(string ss, string[] names, bool on)
        {
            var r = Scanner.Run(ss, 0);
            var marks = MarkStore.Load();

            var targets = r.Items
                .Where(p => names.Length == 0 || names.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();
            foreach (var p in targets) marks.SetIgnore(p.Name, p.Size, on);
            marks.Save();

            var after = Scanner.Run(ss, 0);
            var sb = new StringBuilder();
            sb.AppendLine("操作=" + (on ? "标记忽略" : "取消忽略") + " 命中=" + targets.Count);

            // 这一行是本次修复的核心：标记前后「判定」都不该变，只有「标记」变
            foreach (var t in targets)
            {
                var a = after.Items.FirstOrDefault(x => string.Equals(x.Name, t.Name, StringComparison.OrdinalIgnoreCase));
                sb.AppendLine("  " + t.Name +
                              " 判定=" + StatusText.Name(t.BaseStatus) +
                              (a == null ? " （扫描后不见了）"
                                         : " 标记=" + (a.Ignored ? StatusText.Name(StatusText.Ignored) : "无") +
                                           " 显示=" + StatusText.Name(a.Status)));
            }

            sb.AppendLine("Ignored=" + after.Items.Count(p => p.Ignored) +
                          " Marks=" + (after.Marks == null ? 0 : after.Marks.Count));
            sb.AppendLine("备份文件=" + (File.Exists(AppPaths.MarkPath + ".bak") ? "有" : "无"));
            return sb.ToString();
        }

        private static string MakeExternal(string ss, string name)
        {
            var full = Path.Combine(ss, name);
            if (!File.Exists(full)) return "文件不存在: " + full + "\n";

            File.SetLastWriteTime(full, DateTime.Now.AddDays(-10));
            File.SetCreationTime(full, DateTime.Now);   // 创建时间=现在，修改时间=10 天前
            return "已改为外部导入特征: " + name +
                   "  差值=" + (File.GetCreationTime(full) - File.GetLastWriteTime(full)).TotalHours.ToString("0.0") + " 小时\n";
        }

        /// <summary>
        /// 打印数据位置是怎么定下来的：指针文件在哪、内容是什么、最终用了哪个目录。
        /// 断言靠这几行，不需要去猜 %APPDATA% 里的内容。
        /// </summary>
        private static string Where()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pointer=" + DataLocation.PointerPath);
            sb.AppendLine("PointerExists=" + (File.Exists(DataLocation.PointerPath) ? "1" : "0"));
            sb.AppendLine("PointerValue=" + (DataLocation.SavedBase ?? "(null)"));
            sb.AppendLine("Source=" + DataLocation.Origin);
            sb.AppendLine("AskNeeded=" + (DataLocation.AskNeeded ? "1" : "0"));
            sb.AppendLine("ExeDir=" + DataLocation.ExeDir);
            sb.AppendLine("ToolRoot=" + AppPaths.ToolRoot);
            sb.AppendLine("DataDir=" + AppPaths.DataDir);
            sb.AppendLine("Ledger=" + AppPaths.LedgerPath);
            return sb.ToString();
        }

        /// <summary>待清理文件夹里有什么，以及移回时会用哪个名字。</summary>
        private static string Pending(string ss)
        {
            var list = Mover.ScanPending(ss);
            var sb = new StringBuilder();
            foreach (var p in list)
                sb.AppendLine("  " + p.Name + "  原名=" + p.OriginalName +
                              (string.Equals(p.Name, p.OriginalName, StringComparison.OrdinalIgnoreCase) ? "（未改名）" : "（改过名）"));
            sb.AppendLine("张数=" + list.Count);
            sb.AppendLine("目录=" + Mover.PendingDir(ss));
            sb.AppendLine("目录存在=" + (Directory.Exists(Mover.PendingDir(ss)) ? "1" : "0"));
            return sb.ToString();
        }

        private static string MoveBack(string ss, string[] names)
        {
            var all = Mover.ScanPending(ss);
            var picked = names.Length == 0
                ? all
                : all.Where(p => names.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();

            var r = Mover.RestoreSelected(picked, ss);

            var sb = new StringBuilder();
            sb.AppendLine("勾选=" + picked.Count + " 移回=" + r.Ok +
                          " 跳过=" + r.Skipped.Count + " 失败=" + r.Failed.Count);
            foreach (var s in r.Skipped) sb.AppendLine("  跳过: " + s);
            foreach (var s in r.Failed) sb.AppendLine("  失败: " + s);
            sb.AppendLine("剩余待清理=" + Mover.ScanPending(ss).Count);
            sb.AppendLine("ScreenShot张数=" + Directory.GetFiles(ss, "*.jpeg").Length);
            sb.AppendLine("记录仍在=" + (File.Exists(AppPaths.LastMovePath) ? "1" : "0"));
            return sb.ToString();
        }

        /// <summary>搬数据文件夹：复制 → 核对 → 删旧，并报告旧目录是否真的没了。</summary>
        private static string MoveData(string newBase)
        {
            var old = AppPaths.DataDir;
            int copied;
            string err, leftover;
            var ok = DataLocation.MoveDataTo(newBase, out copied, out err, out leftover);

            var sb = new StringBuilder();
            sb.AppendLine("结果=" + (ok ? "成功" : "失败"));
            sb.AppendLine("复制文件数=" + copied);
            sb.AppendLine("旧目录=" + old);
            sb.AppendLine("旧目录还在=" + (Directory.Exists(old) ? "1" : "0"));
            sb.AppendLine("新目录=" + DataLocation.DataPathFor(newBase, AppPaths.DataIsEnglish));
            sb.AppendLine("新目录存在=" + (Directory.Exists(DataLocation.DataPathFor(newBase, AppPaths.DataIsEnglish)) ? "1" : "0"));
            sb.AppendLine("待删清单=" + (File.Exists(DataLocation.RemovalQueuePath) ? "有" : "无"));
            if (leftover != null) sb.AppendLine("残留=" + leftover);
            if (err != null) sb.AppendLine("错误=" + err);
            return sb.ToString();
        }

        private static string DeleteData()
        {
            var dir = AppPaths.DataDir;
            string err;
            var gone = DataLocation.TryDeleteDataDir(dir, out err);

            var sb = new StringBuilder();
            sb.AppendLine("目标=" + dir);
            sb.AppendLine("删除成功=" + (gone ? "1" : "0"));
            sb.AppendLine("目录还在=" + (Directory.Exists(dir) ? "1" : "0"));
            if (err != null) sb.AppendLine("错误=" + err);
            return sb.ToString();
        }
    }
}
