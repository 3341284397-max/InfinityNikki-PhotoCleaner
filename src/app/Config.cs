using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NikkiPhotoCleaner
{
    /// <summary>
    /// 设置文件（设置.txt）的读写。格式是简单的 key=value，以 # 开头为注释。
    /// 旧版本只写「游戏目录」一个键，这里同时识别中文键和英文键，保证升级不丢配置。
    /// </summary>
    internal static class Config
    {
        private const string KeyGameDirZh = "游戏目录";
        private const string KeyGameDirEn = "GameDir";
        private const string KeyLanguage  = "Language";

        /// <summary>已保存的界面语言代码（zh / en / null）</summary>
        public static string Language
        {
            get { return Read(KeyLanguage); }
        }

        /// <summary>已保存的游戏截图目录（未配置或已失效时返回 null）</summary>
        public static string GameDir
        {
            get
            {
                var v = Read(KeyGameDirZh);
                if (string.IsNullOrEmpty(v)) v = Read(KeyGameDirEn);
                if (string.IsNullOrEmpty(v)) return null;

                v = v.Trim().Trim('"');
                return GameLocator.IsScreenShotDir(v) ? v : null;
            }
        }

        public static void SaveGameDir(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Write(new[] { KeyGameDirZh, KeyGameDirEn },
                  L10n.IsEnglish ? KeyGameDirEn : KeyGameDirZh,
                  path);
        }

        public static void SaveLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            Write(new[] { KeyLanguage }, KeyLanguage, code);
        }

        // ---------------- 底层 ----------------

        private static string Read(string key)
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigPath)) return null;
                foreach (var line in File.ReadAllLines(AppPaths.ConfigPath, Encoding.UTF8))
                {
                    var s = line.Trim();
                    if (s.Length == 0 || s.StartsWith("#")) continue;

                    var i = s.IndexOf('=');
                    if (i <= 0) continue;

                    if (string.Equals(s.Substring(0, i).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return s.Substring(i + 1).Trim();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 改写一个键：删掉 candidates 里所有等价键（避免中英文键并存），再写入新的那一行。
        /// 其它行原样保留。
        /// </summary>
        private static void Write(string[] candidates, string key, string value)
        {
            var lines = new List<string>();
            try
            {
                if (File.Exists(AppPaths.ConfigPath))
                    lines.AddRange(File.ReadAllLines(AppPaths.ConfigPath, Encoding.UTF8));
            }
            catch { }

            var kept = new List<string>();
            foreach (var line in lines)
            {
                var s = line.Trim();
                var drop = false;

                if (s.Length > 0 && !s.StartsWith("#"))
                {
                    var i = s.IndexOf('=');
                    if (i > 0)
                    {
                        var k = s.Substring(0, i).Trim();
                        foreach (var c in candidates)
                            if (string.Equals(k, c, StringComparison.OrdinalIgnoreCase)) { drop = true; break; }
                    }
                }

                if (!drop) kept.Add(line);
            }

            kept.Add(key + "=" + value);

            try
            {
                var dir = Path.GetDirectoryName(AppPaths.ConfigPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // 文件头部的注释只在首次创建时写，避免每次保存都重复堆叠
                var head = File.Exists(AppPaths.ConfigPath) && kept.Exists(x => x.StartsWith("#"))
                    ? new List<string>()
                    : new List<string>
                    {
                        "# " + L10n.T("无限暖暖本地照片清理助手 - 配置"),
                        "# " + L10n.T("若游戏换盘或重装，删掉本文件重新运行即可。"),
                        "# " + L10n.T("Language = zh / en，切换界面语言；删除此行则跟随系统语言。")
                    };

                var all = new List<string>(head);
                all.AddRange(kept);

                File.WriteAllLines(AppPaths.ConfigPath, all, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
