using System;
using System.Collections.Generic;
using System.Globalization;

namespace NikkiPhotoCleaner
{
    internal enum UiLang { Zh, En }

    /// <summary>
    /// 界面语言。中文原文直接当 key 用：
    ///   · 中文界面下原样返回，行为与单语版完全一致；
    ///   · 英文界面下查表，查不到就退回中文（宁可露出中文，也不能让界面空白）。
    /// 这样任何一处漏翻都只是「显示中文」，不会出错，也容易在测试里扫出来。
    /// </summary>
    internal static class L10n
    {
        private static UiLang _lang = UiLang.Zh;

        public static UiLang Current { get { return _lang; } }
        public static bool IsEnglish { get { return _lang == UiLang.En; } }
        public static string Tag { get { return _lang == UiLang.En ? "en" : "zh"; } }

        /// <summary>
        /// 语言优先级：命令行 --lang &gt; 设置文件 Language= &gt; 系统界面语言。
        /// </summary>
        public static void Init(string forced)
        {
            var code = forced;
            if (string.IsNullOrEmpty(code)) code = Config.Language;
            if (string.IsNullOrEmpty(code)) code = SystemLang();
            _lang = IsEnCode(code) ? UiLang.En : UiLang.Zh;
        }

        public static void Set(UiLang lang) { _lang = lang; }

        public static void Toggle()
        {
            _lang = _lang == UiLang.En ? UiLang.Zh : UiLang.En;
            Config.SaveLanguage(Tag);
        }

        private static bool IsEnCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            code = code.Trim().ToLowerInvariant();
            return code.StartsWith("en");
        }

        private static string SystemLang()
        {
            try
            {
                var c = CultureInfo.CurrentUICulture;
                if (c != null && c.TwoLetterISOLanguageName == "zh") return "zh";
            }
            catch { }
            return "en";
        }

        /// <summary>取当前语言文案。zh 传入中文原文。</summary>
        public static string T(string zh)
        {
            if (_lang == UiLang.Zh) return zh;

            string en;
            return En.TryGetValue(zh, out en) ? en : zh;
        }

        /// <summary>带占位符的文案，例如 F("已移入 {0} 张。", n)</summary>
        public static string F(string zh, params object[] args)
        {
            try { return string.Format(T(zh), args); }
            catch { return T(zh); }
        }

        // ============================================================
        //  英文表：key = 中文原文，value = 英文文案
        // ============================================================
        private static readonly Dictionary<string, string> En =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ---------------- 窗口与按钮 ----------------
            { "无限暖暖 · 本地照片清理助手", "Infinity Nikki · Photo Cleanup Assistant" },
            { "本地照片清理助手", "Photo Cleanup Assistant" },
            { "只识别与归拢，绝不自动删除照片", "Sorts and groups only — never deletes a photo" },
            { "截图目录", "Screenshot folder" },
            { "重新选择", "Change folder" },
            { "重新扫描", "Rescan" },
            { "生成网页报告", "HTML report" },
            { "移入待清理文件夹", "Move to review folder" },
            { "移入待清理文件夹（{0}）", "Move to review folder ({0})" },
            { "打开待清理文件夹", "Open review folder" },
            { "移回原目录", "Restore" },
            { "清理记录", "Clean up records" },
            { "双击打开原图 · 右键定位文件", "Double-click to open · right-click for more" },
            { "选中一张照片，这里会显示大图与判定依据",
              "Select a photo to see the full image and why it was flagged" },
            { "示意", "Sample" },
            { "仍在相册", "In album" },
            { "游戏里已删", "Deleted in game" },
            { "无法确认", "Unsure" },

            // ---------------- 列表 ----------------
            { "只看待处理项", "Pending only" },
            { "只列出「已删除 / 疑似 / 太新」三类，隐藏仍在相册里的照片",
              "Show only Deleted / Unsure / Too new, hiding photos that are still in the album" },
            { "勾选的照片会被移入「{0}」，移动不等于删除",
              "Ticked photos are moved into “{0}” — moving is not deleting" },
            { "状态", "Status" },
            { "文件名", "File name" },
            { "拍摄时间", "Taken" },
            { "大小", "Size" },

            // ---------------- 状态与判定 ----------------
            { "在册", "In album" },
            { "已删除", "Deleted" },
            { "疑似", "Unsure" },
            { "太新", "Too new" },
            { "游戏相册中仍存在", "Still present in the in-game album" },
            { "曾出现在相册中，现已从相册缓存消失",
              "Was in the album before and is now gone from the album cache" },
            { "相册缓存中从未见过（可能删过，也可能尚未同步）",
              "Never seen in the album cache (may have been deleted, or not synced yet)" },
            { "拍摄不足 {0} 天，可能还没同步到相册；要清理请手动勾选",
              "Taken less than {0} days ago and may not be synced yet; tick it manually if you still want to clean it" },

            // ---------------- 顶部统计与提示 ----------------
            { "本地 {0} 张｜在册 {1}｜已删除 {2}｜疑似 {3}｜太新 {4}",
              "{0} local | {1} in album | {2} deleted | {3} unsure | {4} too new" },
            { "⚠ {0}（本次仅可查看，禁止移动）", "⚠ {0} (view only — moving is blocked)" },
            { "已自动勾选 {0} 张判定为「已删除」的照片，核对后可移入待清理文件夹。",
              "{0} photos marked “Deleted” were pre-ticked. Check them, then move them to the review folder." },
            { "勾选要清理的照片后点「移入待清理文件夹」；双击可看大图。",
              "Tick the photos to clean, then click “Move to review folder”. Double-click for a bigger preview." },
            { "尚未指定游戏目录", "Game folder not set yet" },

            // ---------------- 预览与右键 ----------------
            { "状态：{0}　{1}\r\n拍摄：{2}\r\n判定：{3}",
              "Status: {0}   {1}\r\nTaken: {2}\r\nReason: {3}" },
            { "打开原图", "Open photo" },
            { "在资源管理器中定位", "Show in Explorer" },
            { "复制文件名", "Copy file name" },

            // ---------------- 安全闸门 ----------------
            { "游戏内相册缓存为空。可能是你清空过缓存，此时比对结果不可信。",
              "The in-game album cache is empty. If you cleared it, this comparison cannot be trusted." },
            { "超过 70% 的本地照片被判定为待删，比例异常，建议先人工抽查。",
              "Over 70% of local photos are flagged for cleanup — that ratio is abnormal, please spot-check first." },
            { "游戏正在运行：刚在游戏内删除的照片，本地缓存可能还没刷新，建议稍等几秒再重新扫描。",
              "The game is running: photos deleted just now may not be written to disk yet. Wait a few seconds and rescan." },

            // ---------------- 扫描 ----------------
            { "扫描失败：", "Scan failed: " },
            { "出错了", "Error" },
            { "没能自动找到游戏的 ScreenShot 文件夹。\n要手动选择吗？",
              "The game's ScreenShot folder could not be found automatically.\nPick it manually?" },
            { "未找到游戏目录", "Game folder not found" },

            // ---------------- 移动 ----------------
            { "还没有扫描结果。\n请先点右上角「重新扫描」。",
              "No scan result yet.\nClick “Rescan” in the top right first." },
            { "暂无数据", "No data" },
            { "安全检查未通过，本次不能移动：\n\n· {0}\n\n这几道检查是为了在你误判时保护照片。按提示处理后点「重新扫描」再试。",
              "Safety checks failed, so nothing will be moved:\n\n· {0}\n\nThese checks protect your photos when the detection is wrong. Fix the issue, click “Rescan” and try again." },
            { "已阻止", "Blocked" },
            { "没有可移动的照片", "Nothing to move" },
            { "将把下面 {0} 张照片移入：\n{1}\n\n{2}{3}\n\n这只是【移动】，不是删除。移动后文件夹会自动打开，你核对完可以 Ctrl+A 全选、按 Delete 真正删除。\n移错了点「移回原目录」可以全部移回。",
              "{0} photos will be moved into:\n{1}\n\n{2}{3}\n\nThis is a MOVE, not a delete. The folder opens automatically — once you have checked the files, press Ctrl+A and then Delete to really remove them.\nMoved the wrong ones? Click “Restore” to put everything back." },
            { "· {0}　[{1}]", "· {0}   [{1}]" },
            { "… 以及另外 {0} 张", "… and {0} more" },
            { "\n\n注意：其中 {0} 张拍摄不足 {1} 天，可能只是还没同步到游戏相册。移动前请再看一眼，不需要的话点「取消」回列表取消勾选。",
              "\n\nNote: {0} of them were taken less than {1} days ago and may simply not be synced to the in-game album yet. Have another look before moving; if you do not need them, click Cancel and untick them." },
            { "\n\n继续吗？", "\n\nContinue?" },
            { "确认移动", "Confirm move" },
            { "移动失败", "Move failed" },
            { "已移入 {0} 张。\n\n位置：\n{1}\n\n核对无误后，在弹出的文件夹里 Ctrl+A 全选、按 Delete 即可真正删除。\n移错了点「移回原目录」。",
              "Moved {0} photos.\n\nLocation:\n{1}\n\nIn the folder that just opened, press Ctrl+A and then Delete to really remove them.\nClick “Restore” if you moved the wrong ones." },
            { "\n\n其中 {0} 张因重名加了批次后缀（不影响还原）。",
              "\n\n{0} of them got a batch suffix because the name already existed (restore still works)." },
            { "\n\n失败 {0} 张：\n{1}", "\n\n{0} failed:\n{1}" },
            { "完成", "Done" },
            { "当前没有勾选任何照片。", "No photos are ticked right now." },
            { "判定为「已删除」需要同时满足：",
              "A photo is marked “Deleted” only when both are true:" },
            { "  1. 这张照片曾经出现在游戏相册的本地缓存里", "  1. It once appeared in the in-game album cache" },
            { "  2. 现在本地缓存里已经没有它了", "  2. It is no longer in that cache" },
            { "本次扫描：在册 {0} 张｜已删除 {1} 张｜疑似 {2} 张｜太新 {3} 张",
              "This scan: {0} in album | {1} deleted | {2} unsure | {3} too new" },
            { "如果你刚在游戏里删过照片：", "If you just deleted photos in game:" },
            { "  · 回到游戏主界面或退出游戏，让缓存在本地落盘",
              "  · Go back to the game's main menu or quit, so the cache is written to disk" },
            { "  · 再点「重新扫描」，这时才会出现「已删除」项",
              "  · Click “Rescan” — only then will “Deleted” entries appear" },
            { "也可以在列表里手动勾选「疑似」的照片——",
              "You can also tick the “Unsure” photos yourself —" },
            { "那等于你确认它已经不需要了，工具不替你判断。",
              "that means you have decided they are no longer needed; the tool will not decide for you." },

            // ---------------- 还原 ----------------
            { "还没有指定游戏截图目录。", "The game screenshot folder has not been set yet." },
            { "暂无", "Not available" },
            { "还没有待清理文件夹。\n\n它的位置应该是：\n{0}\n\n勾选照片后点「移入待清理文件夹」才会生成。",
              "There is no review folder yet.\n\nIt will be created at:\n{0}\n\nIt appears after you tick photos and click “Move to review folder”." },
            { "没有还原记录（尚未执行过移动，或记录被清理过）。",
              "No restore record found (nothing has been moved yet, or the record was cleaned up)." },
            { "无法还原", "Cannot restore" },
            { "移动记录格式无法识别，没法自动还原。\n可以手动到待清理文件夹里把照片拖回。",
              "The move record cannot be parsed, so automatic restore is not possible.\nYou can drag the photos back from the review folder manually." },
            { "待清理文件夹已经不存在了，无需还原。",
              "The review folder no longer exists — nothing to restore." },
            { "待清理文件夹里已经没有上次移动的照片了（可能已被你删除）。",
              "The review folder no longer holds the photos from the last move (you probably deleted them)." },
            { "无需还原", "Nothing to restore" },
            { "把 {0} 张照片移回原目录？\n\n{1}", "Move {0} photos back to the original folder?\n\n{1}" },
            { "确认还原", "Confirm restore" },
            { "已还原 {0} 张。", "Restored {0} photos." },
            { "失败 {0} 张（可能被占用）。", " {0} failed (the file may be in use)." },

            // ---------------- 其它 ----------------
            { "切换界面语言。已经生成的数据文件夹和照片位置不会改变。",
              "Switch the interface language. Existing data folders and photo locations are not affected." },
            { "没有选中任何照片。", "No photos selected." },
            { "无法创建待清理文件夹：", "Cannot create the review folder: " },
            { " — 文件已不存在", " — the file no longer exists" },
            { "本次移动记录 {0}（共 {1} 条，累积）", "Move record {0} ({1} entries, cumulative)" },
            { "源目录", "Source" },
            { "目标目录", "Target" },
            { "_还原", "_restored" },
            { "清理时出错：", "Cleanup failed: " },
            { "生成报告失败：", "Report failed: " },
            { "请选择：无限暖暖\\X6Game\\ScreenShot 文件夹",
              "Select the Infinity Nikki \\X6Game\\ScreenShot folder" },
            { "所选目录看起来不对。\n它的上一级文件夹名必须是 X6Game。",
              "That folder does not look right.\nIts parent folder must be named X6Game." },
            { "目录无效", "Invalid folder" },

            // ---------------- 配置与目录说明 ----------------
            { "无限暖暖本地照片清理助手 - 配置", "Infinity Nikki Photo Cleanup Assistant - settings" },
            { "若游戏换盘或重装，删掉本文件重新运行即可。",
              "If the game moved to another drive or was reinstalled, delete this file and run again." },
            { "Language = zh / en，切换界面语言；删除此行则跟随系统语言。",
              "Language = zh / en switches the interface language; delete this line to follow the system language." },

            // ---------------- 记录清理窗口 ----------------
            { "清理工具记录", "Clean up tool records" },
            { "清理工具自己产生的记录", "Clean up the records this tool created" },
            { "这些文件只用于记录判定依据和还原信息，清理它们不会影响游戏，也不会动到任何照片。",
              "These files only store detection history and restore information. Cleaning them does not affect the game and never touches your photos." },
            { "网页报告", "HTML reports" },
            { "缩略图缓存", "Thumbnail cache" },
            { "待清理空文件夹", "Empty review folders" },
            { "移动记录", "Move record" },
            { "判定台账", "Detection ledger" },
            { "将保留最近 3 份", "Only the latest 3 are kept" },
            { "可随时重新生成", "Can be regenerated any time" },
            { "下次生成报告会自动重建", "Rebuilt automatically next time you generate a report" },
            { "里面已没有照片", "No photos left inside" },
            { "删后「移回原目录」失效", "Restore stops working after deletion" },
            { "删后判定全部重置为「疑似」", "All results reset to “Unsure” after deletion" },
            { "删除后，「移回原目录」将无法再把照片移回原位。\n如果照片已经删掉了，这一项可以安全清理；如果还想保留还原能力，请先不要清。\n\n建议先打开该文件夹确认：如果待确认文件夹已经空了，说明照片已被你删除，记录就没用了。",
              "After deletion, “Restore” can no longer move photos back to their original folder.\nIf you have already deleted the photos, this item is safe to clean; if you still want to keep the ability to restore, leave it alone for now.\n\nTip: open the review folder first. If it is empty, you deleted the photos already and the record is useless." },
            { "台账记录着哪些照片「曾经出现在相册里」，是判断照片是否被游戏内删除的唯一依据。\n\n删除后，所有照片都会变成「疑似」，工具需要重新积累记录才能再次准确判断。\n除非你想完全重置，否则请不要清理这一项。",
              "The ledger remembers which photos “were once in the album”, and it is the only basis for telling whether a photo was deleted in game.\n\nAfter deletion every photo becomes “Unsure”, and the tool must collect history again before it can decide accurately.\nUnless you want a full reset, do not clean this item." },
            { "清理所选", "Clean selected" },
            { "取消", "Cancel" },
            { "未选择任何项目。", "Nothing selected." },
            { "已选 {0} 项", "{0} selected" },
            { "，可释放约 {0}", ", about {0} can be freed" },
            { "（仅删除文件夹，不占空间）", " (folders only, no space freed)" },
            { "确认清理「{0}」", "Clean “{0}”?" },
            { "清理完成", "Cleanup finished" },
            { "没有需要清理的内容。", "Nothing to clean up." },
            { "已清理：", "Cleaned: " },
            { "释放空间：", "Space freed: " },
            { "有 {0} 个文件被占用或权限不足，未能删除。",
              "{0} files were in use or access was denied, so they were not deleted." },
            { "报告 {0} 份", "{0} reports" },
            { "缩略图 {0} 个", "{0} thumbnails" },
            { "空文件夹 {0} 个", "{0} empty folders" },
            { "{0} 个", "{0}" },
            { "、", ", " },

            // ---------------- 网页报告 ----------------
            { "无限暖暖 · 相册残留对账报告", "Infinity Nikki · Album Leftover Report" },
            { "相册残留对账报告", "Album Leftover Report" },
            { "生成时间：{0}<br>截图目录：{1}", "Generated: {0}<br>Screenshot folder: {1}" },
            { "相册已删除", "Deleted in album" },
            { "疑似残留", "Unsure leftovers" },
            { "仍在册", "Still in album" },
            { "太新跳过", "Skipped, too new" },
            { "<b>怎么用这份报告：</b>「相册已删除」= 你在游戏里删掉了，本地还留着，可以清；<br>\n「疑似残留」= 相册缓存里没见过它，可能删过也可能只是没同步，<b>请先看图确认</b>；<br>\n「仍在册」不会出现在下面，也不会被处理。",
              "<b>How to read this report:</b> “Deleted in album” means you removed it in game while the local copy is still here — safe to clean.<br>\n“Unsure leftovers” were never seen in the album cache: maybe deleted, maybe just not synced — <b>check the picture first</b>.<br>\n“Still in album” is not listed below and will not be touched." },
            { "一、相册已删除，本地仍在（建议清理）",
              "1. Deleted in the album but still on disk (recommended to clean)" },
            { "二、疑似残留（请人工确认后再决定）",
              "2. Unsure leftovers (please confirm by eye first)" },
            { "三、太新已跳过（保护新照片）",
              "3. Too new, skipped (protects new photos)" },
            { "{0} · {1} 张", "{0} · {1} photos" },
            { "没有这一类。", "Nothing in this category." },
            { "预览不可用", "Preview unavailable" },
            { "本报告由「无限暖暖 · 本地照片清理助手」生成，仅用于识别与核对，不会删除任何文件。<br>判定阈值：拍摄满 {0} 天；相册缓存目录 {1} 个。",
              "Generated by “Infinity Nikki · Photo Cleanup Assistant”. For identification and review only — it never deletes a file.<br>Threshold: photos at least {0} days old; {1} album cache folders found." },
            { "对账报告_", "report_" },
            { "zh-CN", "en" },
        };
    }
}
