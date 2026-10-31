using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NikkiPhotoCleaner
{
    /// <summary>
    /// 使用数据的存放位置。
    ///
    /// 1.7 及以前是「数据文件夹跟着 exe 走」。把 exe 放桌面时，桌面就会多出一个
    /// 「无限暖暖清理助手_使用数据」文件夹 —— 用户只是为了方便双击，却换来一个常驻的杂物堆。
    ///
    /// 现在首次启动问一次，选择结果记在 %APPDATA%\NikkiPhotoCleaner\location.txt。
    /// 为什么必须记在 %APPDATA%：记在数据文件夹里等于自指 —— 连文件夹在哪都不知道，
    /// 就没法从里面读出它在哪。
    /// </summary>
    internal static class DataLocation
    {
        private const string AppFolder = "NikkiPhotoCleaner";
        private const string FileName = "location.txt";

        /// <summary>位置是怎么定下来的，用于界面上的措辞和排错。</summary>
        internal enum Source
        {
            /// <summary>环境变量 NIKKI_TOOL_ROOT（回归测试用）</summary>
            Env,
            /// <summary>上次用户选的位置</summary>
            Saved,
            /// <summary>老用户：exe 旁边已经有数据文件夹，沿用</summary>
            Existing,
            /// <summary>兜底：exe 所在目录</summary>
            ExeDefault
        }

        private static string _base;
        private static bool _ask;
        private static bool _ready;
        private static Source _source = Source.ExeDefault;

        // ---------------- 指针文件 ----------------

        public static string PointerDir
        {
            get
            {
                // 回归测试用：把「位置记录」也关进隔离目录，
                // 否则跑一次测试就会改掉真实用户 %APPDATA% 里的那条记录
                try
                {
                    var ov = Environment.GetEnvironmentVariable("NIKKI_POINTER_DIR");
                    if (!string.IsNullOrEmpty(ov)) return ov;
                }
                catch { }

                var appData = "";
                try { appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
                catch { }
                if (string.IsNullOrEmpty(appData))
                {
                    try
                    {
                        appData = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "AppData", "Roaming");
                    }
                    catch { appData = Path.GetTempPath(); }
                }
                return Path.Combine(appData, AppFolder);
            }
        }

        public static string PointerPath { get { return Path.Combine(PointerDir, FileName); } }

        /// <summary>exe 所在目录。不要用 AppPaths.ToolRoot —— 那个可能已经被用户改到别处了。</summary>
        public static string ExeDir
        {
            get
            {
                try { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/'); }
                catch { return Directory.GetCurrentDirectory(); }
            }
        }

        // ---------------- 解析 ----------------

        /// <summary>数据文件夹的父目录。</summary>
        public static string BaseDir
        {
            get { Ensure(); return _base; }
        }

        /// <summary>本次启动是否需要问用户把数据放哪。</summary>
        public static bool AskNeeded
        {
            get { Ensure(); return _ask; }
        }

        public static Source Origin
        {
            get { Ensure(); return _source; }
        }

        /// <summary>上一次的选择（未选择或已失效时返回 null）。</summary>
        public static string SavedBase
        {
            get
            {
                var v = ReadPointer();
                if (v == null) return null;
                try { return Directory.Exists(v) ? v : null; }
                catch { return null; }
            }
        }

        public static void Ensure()
        {
            if (_ready) return;
            Prepare();
        }

        /// <summary>
        /// 决定数据文件夹的父目录。优先级：
        ///   环境变量（测试） &gt; 上次的选择 &gt; exe 旁已有的数据文件夹 &gt; exe 目录（并询问）
        /// 只在第一次访问时执行，之后由 Rebind 控制。
        /// </summary>
        public static void Prepare()
        {
            if (_ready) return;
            _ready = true;

            // 1) 环境变量：回归测试靠它把数据关进隔离目录，永远不让弹窗插进来
            try
            {
                var env = Environment.GetEnvironmentVariable("NIKKI_TOOL_ROOT");
                if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
                {
                    _base = env.TrimEnd('\\', '/');
                    _source = Source.Env;
                    _ask = false;
                    return;
                }
            }
            catch { }

            // 2) 上次选过的位置。目录被删掉/换盘了就当作没选过，重新判断
            var saved = SavedBase;
            if (saved != null)
            {
                _base = saved;
                _source = Source.Saved;
                _ask = false;
                return;
            }

            // 3) 老用户升级上来：exe 旁边已经有数据文件夹。沿用，并且静默记住，别打扰
            if (HasDataFolder(ExeDir))
            {
                _base = ExeDir;
                _source = Source.Existing;
                _ask = false;
                TryRemember(ExeDir);
                return;
            }

            // 4) 全新环境：先用 exe 目录兜底，同时标记「要问一下」
            _base = ExeDir;
            _source = Source.ExeDefault;
            _ask = true;
        }

        /// <summary>让下一次访问重新解析（用户刚改完位置时调用）。</summary>
        public static void Reset()
        {
            _ready = false;
            _base = null;
            _ask = false;
            _source = Source.ExeDefault;
        }

        /// <summary>
        /// 用户刚选完位置：写记录 → 让这次解析作废 → 让 AppPaths 重新取路径。
        ///
        /// 三件事必须一起做，少一件都不算数。1.8/1.9.0 只做了前两件里的第一件加
        /// <c>AppPaths.Rebind()</c>，唯独漏了 <see cref="Reset"/> ——
        /// 于是 <see cref="BaseDir"/> 里缓存的还是启动时那次的 exe 目录，
        /// 「跟随游戏放在一起」「自己选一个位置」全都不生效：数据照旧写回 exe 旁边，
        /// 界面「数据位置」里显示的也还是旧路径，重启一次才对得上。
        /// 所以别再手写这三步，统一走这里。
        /// </summary>
        public static bool SwitchTo(string baseDir)
        {
            var ok = TryRemember(baseDir);
            Reset();
            AppPaths.Rebind();
            return ok;
        }

        // ---------------- 记住 / 忘记 ----------------

        public static bool TryRemember(string baseDir)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir)) return false;
                Directory.CreateDirectory(PointerDir);

                var lines = new[]
                {
                    "# 无限暖暖本地照片清理助手 —— 使用数据放在哪里",
                    "# 这一行就是答案：数据文件夹（无限暖暖清理助手_使用数据）建在这下面。",
                    "# 换位置请用程序里的「数据位置」，不要只改这一行，否则旧数据不会跟着走。",
                    Path.GetFullPath(baseDir).TrimEnd('\\', '/')
                };
                File.WriteAllLines(PointerPath, lines, new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        public static bool TryNotRemember()
        {
            try
            {
                if (File.Exists(PointerPath)) File.Delete(PointerPath);
                try { if (Directory.Exists(PointerDir) && Directory.GetFileSystemEntries(PointerDir).Length == 0) Directory.Delete(PointerDir); }
                catch { }
                return true;
            }
            catch { return false; }
        }

        private static string ReadPointer()
        {
            try
            {
                if (!File.Exists(PointerPath)) return null;
                foreach (var raw in File.ReadAllLines(PointerPath, Encoding.UTF8))
                {
                    var s = raw.Trim().Trim('"');
                    if (s.Length == 0 || s.StartsWith("#")) continue;
                    return s.TrimEnd('\\', '/');
                }
            }
            catch { }
            return null;
        }

        // ---------------- 目录名 ----------------

        public static string DataFolderName(bool english)
        {
            return english ? AppPaths.DataFolderEn : AppPaths.DataFolderZh;
        }

        /// <summary>在某个父目录下，数据文件夹的完整路径。</summary>
        public static string DataPathIn(string baseDir, bool english)
        {
            try { return Path.Combine(baseDir, DataFolderName(english)); }
            catch { return null; }
        }

        /// <summary>
        /// 预览用：这个位置上数据文件夹会叫什么。
        /// 已经有哪一个就沿用哪一个（和主程序的判定一致），都没有才按界面语言起名。
        /// 不然英文界面会预告一个 NikkiCleaner_Data，实际建出来的却是旁边的中文旧目录。
        /// </summary>
        public static string DataPathFor(string baseDir, bool preferEnglish)
        {
            try
            {
                if (string.IsNullOrEmpty(baseDir)) return null;
                if (Directory.Exists(Path.Combine(baseDir, AppPaths.DataFolderZh)))
                    return Path.Combine(baseDir, AppPaths.DataFolderZh);
                if (Directory.Exists(Path.Combine(baseDir, AppPaths.DataFolderEn)))
                    return Path.Combine(baseDir, AppPaths.DataFolderEn);
                return DataPathIn(baseDir, preferEnglish);
            }
            catch { return null; }
        }

        public static bool HasDataFolder(string baseDir)
        {
            if (string.IsNullOrEmpty(baseDir)) return false;
            try
            {
                return Directory.Exists(Path.Combine(baseDir, AppPaths.DataFolderZh))
                    || Directory.Exists(Path.Combine(baseDir, AppPaths.DataFolderEn));
            }
            catch { return false; }
        }

        // ---------------- 推荐位置：与游戏文件夹并列 ----------------

        /// <summary>
        /// 从截图目录推出「和游戏文件夹并列」的那一级。
        ///
        ///   …\InfinityNikki_APP\InfinityNikki\X6Game\ScreenShot
        ///                          ↑ 从下往上找名叫 InfinityNikki 的那一段，取它的父级
        ///   ⇒ …\InfinityNikki_APP
        ///
        /// 为什么放这一级：既不会被游戏更新/校验碰到（不在游戏目录内），
        /// 又和游戏一起在同一个盘、同一个大目录里，用户找得到。
        /// </summary>
        public static string SiblingOf(string screenShotDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(screenShotDir)) return null;

                var full = Path.GetFullPath(screenShotDir.Trim().Trim('"')).TrimEnd('\\', '/');
                var parts = full.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

                for (var i = parts.Length - 1; i >= 0; i--)
                {
                    if (!string.Equals(parts[i], "InfinityNikki", StringComparison.OrdinalIgnoreCase)) continue;

                    // 这一段已经是盘根后的第一级，没有可用的父级
                    if (i == 0) return null;

                    var parent = string.Join("\\", parts, 0, i);
                    // "D:" 这类盘根不往里放东西
                    if (parent.Length <= 2 || parent.EndsWith(":")) return null;

                    return parent;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 猜一个推荐位置。找不到游戏目录就返回 null（调用方据此把选项置灰）。
        /// 这里会触发一次目录扫描，只应在首次启动的那一次调用。
        /// </summary>
        public static string SuggestNearGame()
        {
            try
            {
                var ss = GameLocator.Auto();
                return SiblingOf(ss);
            }
            catch { return null; }
        }

        // ---------------- 卸载相关 ----------------

        /// <summary>统计一个目录里的文件数与总字节数，用于删除前的警告。</summary>
        public static void Measure(string dir, out int files, out long bytes)
        {
            files = 0;
            bytes = 0;
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    files++;
                    try { bytes += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// 只允许删「看起来确实是我们的数据文件夹」的目录。
        /// 名字对不上、正好是 exe 目录、或者是盘根，一律拒绝 —— 这个函数宁可误拒。
        /// </summary>
        public static bool IsDeletableDataDir(string dir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) return false;

                var full = Path.GetFullPath(dir).TrimEnd('\\', '/');
                if (full.Length <= 3) return false;                       // C:\ 之类

                var name = Path.GetFileName(full);
                if (!string.Equals(name, AppPaths.DataFolderZh, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, AppPaths.DataFolderEn, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (string.Equals(full, ExeDir, StringComparison.OrdinalIgnoreCase)) return false;
                return Directory.Exists(full);
            }
            catch { return false; }
        }

        /// <summary>递归复制目录，已存在的同名文件保留原样（宁可少覆盖）。</summary>
        public static int CopyDir(string from, string to)
        {
            var n = 0;
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return 0;
            if (!Directory.Exists(from)) return 0;

            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from))
            {
                var t = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(t)) continue;
                try { File.Copy(f, t, false); n++; } catch { }
            }
            foreach (var d in Directory.GetDirectories(from))
                n += CopyDir(d, Path.Combine(to, Path.GetFileName(d)));
            return n;
        }

        // ---------------- 改位置：复制 → 校验 → 删旧 ----------------

        /// <summary>
        /// 把数据文件夹整个搬到 <paramref name="targetBase"/> 下面。
        ///
        /// 1.8 只复制不删旧，用户改完位置会发现原地还留着一份，两边还都可能被继续写 ——
        /// 那等于把「搬」做成了「复制」，改天两边台账不一样，判定就对不上了。
        /// 现在复制后会核对文件数与总字节，对得上才删旧；删不掉（被占用）就登记到待删清单，
        /// 下次启动第一件事处理，绝不假装成功。
        /// </summary>
        public static bool MoveDataTo(string targetBase, out int copied, out string error, out string leftover)
        {
            copied = 0;
            error = null;
            leftover = null;

            try
            {
                var from = AppPaths.DataDir;
                var to = DataPathFor(targetBase, AppPaths.DataIsEnglish);

                if (string.IsNullOrEmpty(to)) { error = L10n.T("新位置不可用。"); return false; }
                if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                {
                    error = L10n.T("新位置和现在的位置是同一个地方。");
                    return false;
                }
                // 目标落在旧的里面（或反过来）：先复制再删就会把自己删掉
                if (to.StartsWith(from + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    from.StartsWith(to + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    error = L10n.T("新位置不能是旧位置的上层或下层文件夹。");
                    return false;
                }

                if (!Directory.Exists(from))
                {
                    // 还没有数据（第一次启动就改位置）：建个空目录就算搬完了
                    Directory.CreateDirectory(to);
                    return true;
                }

                copied = CopyDir(from, to);

                int f1, f2; long b1, b2;
                Measure(from, out f1, out b1);
                Measure(to, out f2, out b2);
                if (f2 < f1 || b2 < b1)
                {
                    error = L10n.F("复制后核对不上：源文件夹 {0} 个文件 / {1}，新位置 {2} 个 / {3}。\n旧数据一个都没删，请检查新位置是否可写。",
                        f1, RecordCleaner.Size(b1), f2, RecordCleaner.Size(b2));
                    return false;
                }

                // 核对通过才动旧目录。删不干净（有文件被占用）不算失败 ——
                // 数据已经安全落到新位置了，只是垃圾留到下次启动再扫。
                if (IsDeletableDataDir(from))
                {
                    string err;
                    if (!TryDeleteDataDir(from, out err)) { QueueRemoval(from); leftover = from; }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 删掉数据文件夹本身，不只是里面的文件。
        ///
        /// 逐项删而不是 <c>Directory.Delete(recursive)</c>：一把梭遇到一个被占用的文件就整段停下，
        /// 结果就是「删了一半、文件夹还在」，用户以为删干净了。
        /// 只有目录真的消失才返回 true。
        /// </summary>
        public static bool TryDeleteDataDir(string dir, out string err)
        {
            err = null;
            if (!IsDeletableDataDir(dir)) { err = L10n.T("这个目录看起来不像是本程序的数据文件夹。"); return false; }

            try
            {
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    try { File.Delete(f); }
                    catch (Exception ex) { if (err == null) err = Path.GetFileName(f) + " — " + ex.Message; }
                }

                // 从最深的开始删空目录
                var subs = Directory.GetDirectories(dir, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length)
                    .ToList();
                foreach (var d in subs) { try { Directory.Delete(d, true); } catch { } }

                try { Directory.Delete(dir, true); }
                catch (Exception ex) { if (err == null) err = ex.Message; }
            }
            catch (Exception ex)
            {
                if (err == null) err = ex.Message;
            }

            return !Directory.Exists(dir);
        }

        // ---------------- 「下次启动再删」清单 ----------------
        //
        // 有些文件（缩略图、正在被别的程序扫描的台账）在程序运行时删不掉。
        // 与其骗用户「删完了」，不如记下来，下次启动第一件事处理 ——
        // 那时还没有任何句柄，一定删得掉。

        public static string RemovalQueuePath
        {
            get { return Path.Combine(PointerDir, "pending-remove.txt"); }
        }

        public static void QueueRemoval(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return;
                Directory.CreateDirectory(PointerDir);

                var lines = new List<string>();
                if (File.Exists(RemovalQueuePath)) lines.AddRange(File.ReadAllLines(RemovalQueuePath, Encoding.UTF8));
                if (lines.Any(l => string.Equals(l.Trim(), dir, StringComparison.OrdinalIgnoreCase))) return;
                lines.Add(dir);
                File.WriteAllLines(RemovalQueuePath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>启动时调用：把上次没删干净的旧数据文件夹补删一次。</summary>
        public static void ProcessRemovalQueue()
        {
            try
            {
                if (!File.Exists(RemovalQueuePath)) return;

                var left = new List<string>();
                foreach (var raw in File.ReadAllLines(RemovalQueuePath, Encoding.UTF8))
                {
                    var dir = raw.Trim();
                    if (dir.Length == 0 || dir.StartsWith("#")) continue;
                    if (!Directory.Exists(dir)) continue;

                    // 绝不能删正在用的那个
                    string cur = null;
                    try { cur = AppPaths.DataDir; } catch { }
                    if (!string.IsNullOrEmpty(cur) &&
                        string.Equals(dir.TrimEnd('\\', '/'), cur.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                        continue;

                    string err;
                    if (!TryDeleteDataDir(dir, out err)) left.Add(dir);
                }

                if (left.Count == 0) File.Delete(RemovalQueuePath);
                else File.WriteAllLines(RemovalQueuePath, left.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
