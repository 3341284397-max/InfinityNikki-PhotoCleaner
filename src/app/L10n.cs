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

        /// <summary>
        /// 只按命令行 / 系统语言定界面，不去读设置文件。
        ///
        /// 首次启动「数据放在哪」的弹窗要用：那时候设置文件在哪个目录还没定下来，
        /// 一读它就会把数据目录按 exe 同目录锁定，用户选完也改不回来了。
        /// </summary>
        public static void InitNoConfig(string forced)
        {
            var code = forced;
            if (string.IsNullOrEmpty(code)) code = SystemLang();
            _lang = IsEnCode(code) ? UiLang.En : UiLang.Zh;
        }

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
            { "只列出需要你判断的：已删除 / 疑似 / 太新。仍在相册与已忽略的会隐藏",
              "Show only what needs your call: Deleted / Unsure / Too new. In-album and ignored photos are hidden" },
            { "勾选的照片会被移入「{0}」，移动不等于删除",
              "Ticked photos are moved into “{0}” — moving is not deleting" },
            { "状态", "Status" },
            { "文件名", "File name" },
            { "拍摄时间", "Taken" },
            { "大小", "Size" },
            { "全选", "Select all" },
            { "取消全选", "Clear selection" },
            { "已勾选 {0} / {1} 张", "{0} of {1} selected" },
            { "暂无照片", "No photos" },

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

            // ---------------- 用户标记与外部导入 ----------------
            { "已忽略", "Ignored" },
            { "标记", "Tag" },
            { "外部导入", "Imported" },
            { "标记忽略", "Ignore" },
            { "取消忽略", "Un-ignore" },
            { "按状态", "By status" },
            { "全部勾选", "Tick all" },
            { "你标记了「忽略」，不参与勾选与统计。可在工具栏取消",
              "You marked this as ignored — excluded from ticking and counts. Undo it from the toolbar" },
            { "已标记忽略，不参与勾选与统计", "Marked as ignored — excluded from ticking and counts" },
            { "看起来是外部拷入的照片，默认不勾选",
              "Looks like a file copied in from outside — not ticked by default" },
            { "发现 {0} 张「游戏里已删」，一张都还没勾。要清理就点右边。",
              "{0} photos were deleted in game and none are ticked yet — use the button on the right" },
            { "有 {0} 张判定为「游戏里已删」。勾选后点「移入待清理文件夹」。",
              "{0} photos were deleted in game. Tick them, then move." },
            { "有 {0} 张「游戏里已删」，勾选后可移入待清理文件夹。",
              "{0} photos were deleted in game — tick them to move them out." },
            { "｜排除 {0}", " | {0} excluded" },
            { "点一下勾选全部「{0}」，再点一下取消",
              "Click to tick every “{0}”, click again to clear" },
            { "勾选全部「游戏里已删」", "Tick all “Deleted in game”" },
            { "取消勾选全部「游戏里已删」", "Clear all “Deleted in game”" },
            { "勾选全部「太新」", "Tick all “Too new”" },
            { "取消勾选全部「太新」", "Clear all “Too new”" },
            { "勾选全部「无法确认」", "Tick all “Unsure”" },
            { "取消勾选全部「无法确认」", "Clear all “Unsure”" },
            { "勾选全部「已忽略」", "Tick all “Ignored”" },
            { "取消勾选全部「已忽略」", "Clear all “Ignored”" },
            { "游戏相册里还有这些照片，正常情况下不需要清理",
              "These are still in the in-game album and normally need no cleanup" },
            { "勾选的 {0} 张都被标记为「已忽略」或「外部导入」，不会移动。\n\n要移动它们，先取消标记。",
              "All {0} ticked photos are ignored or imported, so nothing will be moved.\n\nUn-mark them first if you really want to move them." },
            { "标记说明", "Tags" },
            { "同时标记为「外部导入」：像是从外部拷进来的，默认不勾选",
              "Also tagged “Imported” — it looks like a file copied in from outside, so it is never ticked by default" },
            { "要把这 {0} 张（共 {1} 张）都标记为「已忽略」吗？\n\n标记后它们不会被勾选、不会被移动，也不再计入待删统计。\n之后随时可以用「取消忽略」还原。",
              "Mark all {0} of {1} photos as ignored?\n\nThey will no longer be ticked, moved, or counted as pending.\nYou can undo this any time with “Un-ignore”." },
            { "确认批量标记", "Confirm bulk tag" },

            // ---------------- 顶部统计与提示 ----------------
            { "本地 {0} 张｜在册 {1}｜已删除 {2}｜疑似 {3}｜太新 {4}",
              "{0} local | {1} in album | {2} deleted | {3} unsure | {4} too new" },
            { "⚠ {0}（本次仅可查看，禁止移动）", "⚠ {0} (view only — moving is blocked)" },
            { "已自动勾选 {0} 张判定为「已删除」的照片，核对后可移入待清理文件夹。",
              "{0} “Deleted” photos pre-ticked. Check, then move." },
            { "有 {0} 张判定为「游戏里已删」，核对后可移入待清理文件夹。",
              "{0} photos were deleted in game. Check them, then move." },
            { "勾选要清理的照片后点「移入待清理文件夹」；双击可看大图。",
              "Tick photos, then click “Move to review folder”." },
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
            { "下面这 {0} 张照片将被移入待清理文件夹",
              "These {0} photos will be moved into the review folder" },
            { "这只是移动，不是删除。移动后文件夹会自动打开，核对完 Ctrl+A 全选、按 Delete 才真正删掉；移错了点「移回原目录」可以全部移回。",
              "This is a move, not a delete. The folder opens automatically — press Ctrl+A then Delete there to really remove them. Moved the wrong ones? Click “Restore” to put everything back." },
            { "移入", "Into" },
            { "共 {0} 张，合计 {1}", "{0} photos, {1} in total" },
            { "移动 {0} 张", "Move {0} photos" },
            { "其中 {0} 张拍摄不足 {1} 天，可能只是还没同步到游戏相册。移动前请再看一眼；不需要移动的话点「取消」，回列表取消勾选。",
              "{0} of them were taken less than {1} days ago and may simply not be synced to the in-game album yet. Have another look before moving; if you do not need them, click Cancel and untick them in the list." },
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

            // ---------------- 使用数据放在哪 ----------------
            { "数据位置", "Data location" },
            { "看看使用数据存在哪、挪走它、删掉它，或者彻底卸载。",
              "See where the app keeps its data, move it, delete it, or uninstall completely." },
            { "使用数据放在哪里", "Where to keep the app data" },
            { "先把使用数据放到哪里", "First, where should the app keep its data?" },
            { "程序需要一个小文件夹来存相册台账、你标过的「忽略」、设置和缩略图缓存。如果你习惯把 exe 放在桌面上直接双击，它会顺手在桌面建一个文件夹 —— 所以先问你一句。选错了不要紧，之后在右上角「数据位置」里随时能改。",
              "The app needs one small folder for the album ledger, your “ignore” marks, settings and thumbnail cache. If you keep the exe on your desktop, that folder would land on the desktop too — so it asks first. Nothing is permanent: use “Data location” in the top-right corner to change it later." },
            { "跟随游戏放在一起（推荐）", "Next to the game folder (recommended)" },
            { "没找到游戏目录，可用下面两个选项", "Game folder not found — use one of the options below" },
            { "和 exe 放在同一个文件夹", "In the same folder as the exe" },
            { "我自己选一个位置…", "Let me pick a folder…" },
            { "点这张卡会弹出文件夹选择框", "Clicking this opens a folder picker" },
            { "只管程序自己的数据，不动游戏文件，也不动照片。",
              "Only affects the app's own data — never game files, never photos." },
            { "用默认位置", "Use default" },
            { "就用这里", "Use this" },
            { "选一个文件夹，程序会在它里面建「使用数据」文件夹",
              "Pick a folder — the app will create its data folder inside it" },
            { "还没选位置。点第三张卡片挑一个文件夹吧。",
              "No location chosen yet. Click the third card to pick a folder." },
            { "还差一步", "One more step" },
            { "这个位置写不进去：\n{0}\n\n可能没有权限（比如程序放在 C:\\Program Files 下），换一个位置，或者指到你的用户目录里。",
              "Cannot write to this location:\n{0}\n\nIt may require administrator rights (for example under C:\\Program Files). Pick another folder, such as one inside your user profile." },
            { "换个位置", "Pick another folder" },
            { "使用数据", "App data" },
            { "台账、你标过的「忽略」、设置和缩略图缓存都在这个文件夹里。照片本身不在里面 —— 只有你主动移进来的「待清理照片」是例外。没有安装过程就没有卸载程序：删掉 exe 之后，这里这个文件夹和 %APPDATA% 里那条位置记录，都要在这里清掉。",
              "The album ledger, your “ignore” marks, settings and thumbnail cache all live in this folder. Your photos are not stored here — except the review folder you move them into on purpose. No installer means no uninstaller: once the exe is gone, this folder and the location record in %APPDATA% are cleaned up from here." },
            { "数据文件夹", "Data folder" },
            { "打开", "Open" },
            { "更改位置…", "Move…" },
            { "删除使用数据…", "Delete app data…" },
            { "关闭", "Close" },
            { "选一个文件夹，数据文件夹会被搬进去",
              "Pick a folder — the data folder will be moved into it" },
            { "这个位置写不进去：\n{0}", "Cannot write to this location://n{0}" },
            { "那个位置上已经有一个数据文件夹了，里面还有 {0} 项内容。\n\n两份台账混在一起会把「已删除」判断搞乱，所以这里不覆盖。\n要么换个空位置，要么先把旧的删掉再来。",
              "That location already holds a data folder with {0} item(s).\n\nMerging two ledgers would scramble the “deleted in game” verdicts, so nothing is overwritten here.\nPick an empty location, or delete the old folder first." },
            { "这里已经有数据了", "Data already there" },
            { "新位置和现在的位置是同一个地方。", "That is where the data already lives." },
            { "没动", "Nothing changed" },
            { "复制后核对不上：源文件夹 {0} 个文件 / {1}，新位置 {2} 个 / {3}。\n旧数据一个都没删，请检查新位置是否可写。",
              "The copy does not add up: source has {0} file(s) / {1}, the new location has {2} / {3}.\nNothing was deleted from the old folder — check that the new location is writable." },
            { "没有搬成", "Move failed" },
            { "已经把 {0} 个文件搬到：\n{1}", "Moved {0} file(s) to://n{1}" },
            { "旧文件夹还有东西被别的程序占着，暂时没删掉：\n{0}\n已经记下来了，下次启动会先把它删干净。",
              "Some files in the old folder are held by another program and could not be deleted yet://n{0}//nThis is recorded — the next launch removes it before anything else." },
            { "旧文件夹已经删掉了，现在只剩这一份，两边的台账不会再打架。",
              "The old folder is gone. Only one copy is left, so the two ledgers can no longer disagree." },
            { "搬完了", "Moved" },
            { "新位置不可用。", "That location is not usable." },
            { "新位置不能是旧位置的上层或下层文件夹。",
              "The new location cannot be a parent or a child of the current one." },
            { "这个目录看起来不像是本程序的数据文件夹，为安全起见不删：\n{0}",
              "This folder does not look like the app's data folder, so nothing was deleted://n{0}" },
            { "这个目录看起来不像是本程序的数据文件夹。", "This folder does not look like the app's data folder." },
            { "没删", "Not deleted" },
            { "要删掉这个文件夹吗？\n\n{0}\n\n里面有 {1} 个文件，共 {2}。整个文件夹会被删掉 —— 台账、你标过的「忽略」、设置、缩略图缓存都会消失。\n\n照片本身不受影响。删完程序会退出；下次启动会重新问你要把数据放在哪。",
              "Delete this folder?\n\n{0}\n\nIt holds {1} file(s), {2} in total. The whole folder goes — the album ledger, your “ignore” marks, settings and thumbnail cache included.\n\nYour photos themselves are not affected. The app exits afterwards, and the next launch asks where to keep its data again." },
            { "\n\n待清理文件夹里还有 {0} 张照片，那是你的照片，这里不会动它。",
              "\n\nThe review folder still holds {0} photo(s). Those are your photos — they will not be touched here." },
            { "确认删除使用数据", "Confirm deleting app data" },
            { "还有文件正在被占用，这次没删完：\n{0}\n\n已经记下来了。程序这就退出，下次启动会先把剩下的删干净。",
              "Some files are still in use, so this pass did not finish://n{0}//n//nThis is recorded. The app exits now, and the next launch removes what is left before anything else." },
            { "没删干净", "Not fully removed" },
            { "已经删干净了，文件夹本身也没留下。\n\n程序现在退出。下次启动会重新问你要把数据放在哪 —— 这就是删成功的证明。",
              "Deleted, folder and all.\n\nThe app is closing now. The next launch asks where to keep its data again — that is your proof it worked." },
            { "删除完成", "Deleted" },

            { "游戏截图", "Screenshots" },
            { "待清理", "Review" },
            { "游戏截图 {0}", "Screenshots {0}" },
            { "待清理 {0}", "Review {0}" },
            { "占用", "Size" },
            { "待清理文件夹 {0} 张｜共 {1}", "Review folder: {0} photo(s) | {1} total" },
            { "待清理文件夹是空的。", "The review folder is empty." },
            { "勾选要留下的照片后点「移回原目录」；这张列表里的照片不在游戏截图目录里。",
              "Tick the photos you want to keep, then click “Restore”. These files are not in the screenshot folder." },
            { "移回原目录（{0}）", "Restore ({0})" },
            { "移回时的文件名：{0}", "Will be restored as: {0}" },
            { "移回时会用原名 {0}", "Restored under its original name {0}" },
            { "全部移回", "Restore all" },
            { "一张都没勾。要把这 {0} 张全部移回原目录吗？",
              "Nothing is ticked. Restore all {0} photos to the screenshot folder?" },
            { "已移回 {0} 张。", "Restored {0} photo(s)." },
            { "跳过 {0} 张：", "Skipped {0}:" },
            { "失败 {0} 张：", "Failed {0}:" },
            { "\n…还有 {0} 张", "\n…and {0} more" },
            { "移回完成", "Restore finished" },
            { " — 原目录里已有同一张，跳过", " — the same photo is already there, skipped" },
            { "你标记了「忽略」：不参与勾选与统计；取消标记后会回到原来的判定",
              "Marked “ignored”: excluded from selection and statistics. Clearing the mark brings the original verdict back" },
            { "看游戏截的图，也就是 ScreenShot 文件夹", "Photos the game took — the ScreenShot folder" },
            { "看待清理文件夹里的照片。勾选后可以移回原目录",
              "Photos you moved to the review folder. Tick some to restore them" },
            { "已经移入待清理文件夹。确认不要了就在资源管理器里删掉，想留着就勾选后移回原目录。",
              "Moved to the review folder. If you are sure, delete it in Explorer; if not, tick it and restore it." },

            { "zh-CN", "en" },
        };
    }
}
