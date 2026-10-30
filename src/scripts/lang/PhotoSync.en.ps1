# ============================================================
#  PhotoSync.ps1 / Restore.ps1 的英文文案表
#  English string table for PhotoSync.ps1 and Restore.ps1
#
#  key   = 脚本里传给 T / TF 的中文原文（必须一字不差，含前导空格）
#  value = 英文文案
#
#  中文界面下不会加载本文件，所以这里只需要维护英文。
#  新增文案时：脚本里写 (T '中文原文')，然后在本表加一条同名 key；
#  漏加的后果只是该句显示中文，不会报错（run-tests.sh 会给出提示）。
# ============================================================
@{
    # ---------------- 判定状态与理由 ----------------
    '在册' = 'In album'
    '已删除' = 'Deleted'
    '疑似' = 'Unsure'
    '太新' = 'Too new'
    '游戏相册中仍存在' = 'Still present in the in-game album'
    '曾出现在相册中，现已从相册缓存消失' = 'Was in the album before and is now gone from the album cache'
    '拍摄不足 {0} 天，可能还没同步到相册；要清理请手动勾选' = 'Taken less than {0} days ago and may not be synced yet; tick it manually if you still want to clean it'
    '相册缓存中从未见过（可能删过，也可能尚未同步）' = 'Never seen in the album cache (may have been deleted, or not synced yet)'

    # ---------------- 通用 ----------------
    '按回车键关闭窗口' = 'Press Enter to close this window'
    '已取消，未做任何改动。' = 'Cancelled. Nothing was changed.'
    '无限暖暖本地照片清理助手 - 配置' = 'Infinity Nikki Photo Cleanup Assistant - settings'
    '若游戏换盘或重装，删掉本文件重新运行即可。' = 'If the game moved to another drive or was reinstalled, delete this file and run again.'

    # ---------------- 查找游戏目录 ----------------
    '正在自动查找游戏目录…' = 'Looking for the game folder automatically...'
    '没能自动找到游戏目录，请在下面对话框里选中游戏里的 ScreenShot 文件夹。' = 'The game folder could not be found automatically. Please pick the game''s ScreenShot folder in the dialog below.'
    '请选择：无限暖暖\X6Game\ScreenShot 文件夹' = 'Select the Infinity Nikki \X6Game\ScreenShot folder'
    '所选目录看起来不是游戏的 ScreenShot 目录（其上一级应为 X6Game）。' = 'That does not look like the game''s ScreenShot folder (its parent folder must be named X6Game).'

    # ---------------- 清理记录 ----------------
    '清理工具自己产生的记录' = 'Clean up the records this tool created'
    '这些文件只用于保存判定依据和还原信息，清理它们不影响游戏，也不会动到任何照片。' = 'These files only store detection history and restore information. Cleaning them does not affect the game and never touches your photos.'
    '     序号  项目                 数量        占用' = '     No.   Item                 Count        Size'
    '网页报告' = 'HTML reports'
    '{0} 份' = '{0}'
    '缩略图缓存' = 'Thumbnail cache'
    '{0} 个' = '{0}'
    '待清理空文件夹' = 'Empty review folders'
    '移动记录' = 'Move record'
    '无' = 'none'
    '判定台账' = 'Detection ledger'
    '     绿色 = 安全项，删了会自动重建' = '     Green  = safe, rebuilt automatically'
    '     黄色 = 危险项，删了会失去还原能力或重置判定' = '     Yellow = risky, you lose restore ability or reset the results'
    '回车 = 只清安全项（1 2 3）；输入 a = 连同台账和移动记录一起清；输入 n = 取消' = 'Enter = clean safe items only (1 2 3); a = also clean the ledger and the move record; n = cancel'
    '     请选择' = '     Your choice'
    '清理台账后，所有照片的判定会重置为「疑似」；清理移动记录后「还原上次移动」会失效。' = 'Cleaning the ledger resets every photo to "Unsure"; cleaning the move record disables "restore last move".'
    '     确认连危险项一起清理？输入 yes 继续' = '     Clean the risky items too? Type yes to continue'
    '报告 {0} 份' = '{0} reports'
    '缩略图 {0} 个' = '{0} thumbnails'
    '空文件夹 {0} 个' = '{0} empty folders'
    '没有需要清理的内容。' = 'Nothing to clean up.'
    '已清理：' = 'Cleaned: '
    '、' = ', '
    '释放空间：' = 'Space freed: '
    '有 {0} 个文件被占用或权限不足，未能删除。' = '{0} files were in use or access was denied, so they were not deleted.'

    # ---------------- 主流程标题 ----------------
    '无限暖暖 · 本地照片清理助手' = 'Infinity Nikki · Photo Cleanup Assistant'
    '（只做识别与归拢，绝不自动删除照片）' = '(identifies and groups only - never deletes your photos)'
    '第 1 步：定位游戏目录' = 'Step 1: locate the game folder'
    '未能定位到游戏的 ScreenShot 目录，已退出。' = 'Could not locate the game''s ScreenShot folder. Exiting.'
    '截图目录：' = 'Screenshot folder: '
    '第 2 步：读取游戏内相册缓存' = 'Step 2: read the in-game album cache'
    '缓存目录数：' = 'Cache folders: '
    '相册在册照片：{0} 张' = 'Photos in album: {0}'
    '未找到 NikkiPhotos_* 缓存目录，可能从未在游戏里使用过相册。' = 'No NikkiPhotos_* cache folder found - the in-game album may never have been used.'
    '第 3 步：比对本地照片' = 'Step 3: compare local photos'
    'ScreenShot 目录里没有照片，无需处理。' = 'There are no photos in ScreenShot. Nothing to do.'
    '本地照片共 {0} 张' = '{0} local photo(s)'
    '相册仍在册：{0} 张' = 'Still in album: {0}'
    '判定已删除：{0} 张' = 'Deleted: {0}'
    '疑似残留：{0} 张' = 'Unsure leftovers: {0}'
    '太新跳过：{0} 张' = 'Too new, skipped: {0}'
    '第 4 步：安全检查' = 'Step 4: safety checks'
    '游戏内相册缓存为空。可能是你清空过缓存，此时比对结果不可信。' = 'The in-game album cache is empty. If you cleared it, this comparison cannot be trusted.'
    '超过 70% 的本地照片被判定为待删，比例异常，建议先人工抽查。' = 'Over 70% of local photos are flagged for cleanup - that ratio is abnormal, please spot-check first.'
    '游戏正在运行：刚在游戏内删除的照片，本地缓存可能还没刷新，建议稍等几秒再重新扫描。' = 'The game is running: photos deleted just now may not be written to disk yet. Wait a few seconds and rescan.'
    '安全检查通过' = 'Safety checks passed'
    '本次将强制只生成报告，不会移动任何文件。' = 'Only a report will be generated; no files will be moved.'
    '第 5 步：生成预览报告' = 'Step 5: generate the preview report'
    '已生成 {0} 张缩略图' = '{0} thumbnails generated'

    # ---------------- 网页报告 ----------------
    '无限暖暖 · 相册残留对账报告' = 'Infinity Nikki · Album Leftover Report'
    '相册残留对账报告' = 'Album Leftover Report'
    '生成时间：{0}<br>截图目录：{1}' = 'Generated: {0}<br>Screenshot folder: {1}'
    '相册已删除' = 'Deleted in album'
    '疑似残留' = 'Unsure leftovers'
    '仍在册' = 'Still in album'
    '太新跳过' = 'Skipped, too new'
    '{0} · {1} 张' = '{0} · {1} photo(s)'
    '没有这一类。' = 'Nothing in this category.'
    '预览不可用' = 'Preview unavailable'
    '本报告由「无限暖暖 · 本地照片清理助手」生成，仅用于识别与核对，不会删除任何文件。<br>判定阈值：拍摄满 {0} 天；相册缓存目录 {1} 个。' = 'Generated by "Infinity Nikki · Photo Cleanup Assistant". For identification and review only - it never deletes a file.<br>Threshold: photos at least {0} days old; {1} album cache folders found.'
    '对账报告_' = 'report_'
    '报告已生成：' = 'Report saved: '

    # ---------------- 首次运行 ----------------
    '首次运行提示' = 'First run'
    '这是第一次运行，已建立下面这份「相册台账」：' = 'This is the first run. The photo ledger below has been created:'
    '以后游戏里删掉照片，再运行本工具就能准确识别出来（判定为「已删除」）。' = 'From now on, photos you delete in game will be detected accurately on the next run (marked "Deleted").'
    '本次只生成报告，不移动任何文件。请先核对报告判断是否准确。' = 'This run only generates a report and moves nothing. Please check whether the report looks right.'

    # ---------------- 移动 ----------------
    '第 6 步：处理' = 'Step 6: process'
    '因安全检查未通过，本次不移动文件。' = 'Safety checks failed, so no files will be moved.'
    '当前为「只出报告」模式，不移动文件。' = 'Report-only mode: no files will be moved.'
    '没有需要清理的照片。' = 'No photos need cleaning.'
    '即将把 {0} 张照片移动到 ScreenShot 下的「待清理照片」文件夹。' = '{0} photo(s) will be moved into the review folder under ScreenShot.'
    '这只是移动，不是删除；确认无误后你可以打开该文件夹，Ctrl+A 全选再按 Delete。' = 'This is a move, not a delete. Once you have checked them, open that folder, press Ctrl+A and then Delete.'
    '    待移动清单：' = '    To be moved:'
    '      · {0} [{1}]' = '      · {0} [{1}]'
    '确认移动？输入 y 回车继续，其他任意键取消' = 'Move them? Type y and press Enter to continue, any other key to cancel'
    '移动失败：{0} — {1}' = 'Move failed: {0} — {1}'
    '本次移动记录 {0}（共 {1} 条，累积）' = 'Move record {0} ({1} entries, cumulative)'
    '源目录' = 'Source'
    '目标目录' = 'Target'
    '已移入 {0} 张：' = 'Moved {0} photo(s) to:'
    '接下来：窗口会自动打开这个文件夹，确认无误后 Ctrl+A 全选、按 Delete 删除。' = 'Next: the folder opens automatically. Once you are sure, press Ctrl+A and then Delete.'
    '如果发现移错了，双击工具目录里的「还原上次移动」脚本可以全部移回。' = 'Moved the wrong ones? Run the "restore last move" script in the tool folder to put everything back.'

    # ---------------- 还原（Restore.ps1） ----------------
    '无限暖暖 · 照片移回原目录' = 'Infinity Nikki · Move photos back'
    '没有可还原的记录' = 'No record to restore'
    '尚未执行过移动，或者记录文件已被清理。' = 'Nothing has been moved yet, or the record file was cleaned up.'
    '记录文件位置：' = 'Record file: '
    '来源：' = 'From: '
    '暂存：' = 'Held in: '
    '数量：{0} 张' = 'Count: {0}'
    '记录缺少源目录或目标目录，无法自动还原。' = 'The record has no source or target folder, so automatic restore is not possible.'
    '可以手动把照片从待清理文件夹拖回去。' = 'You can drag the photos back from the review folder manually.'
    '待清理文件夹已经不存在了（可能你已经把它删掉了），无法还原。' = 'The review folder no longer exists (you probably deleted it), so nothing can be restored.'
    '待清理文件夹里已经没有这些照片了，无需还原。' = 'These photos are no longer in the review folder - nothing to restore.'
    '将执行还原' = 'About to restore'
    '确认把这 {0} 张移回原目录？输入 y 回车继续，其他任意键取消' = 'Move these {0} photos back to the original folder? Type y and press Enter to continue, any other key to cancel'
    '还原失败：{0} — {1}' = 'Restore failed: {0} — {1}'
    '已还原 {0} 张' = 'Restored {0} photo(s)'
    '失败 {0} 张（可能被占用）。' = ' {0} failed (the file may be in use).'
    '记录已全部还原，记录文件已删除。' = 'Everything was restored; the record file has been deleted.'
    '还原记录保留：' = 'Record kept: '
    '待清理文件夹已空，已自动删除。' = 'The review folder is now empty and was removed.'
}
