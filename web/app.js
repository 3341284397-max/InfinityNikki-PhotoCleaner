/* ==========================================================================
   无限暖暖 · 照片清理助手（网页版）
   纯本地运行：文件只在浏览器内存里读取，不上传、不联网、不写回磁盘。
   ========================================================================== */
(function () {
  'use strict';

  /* ---------------- 常量 ---------------- */
  var STATUS = { InAlbum: 'InAlbum', Deleted: 'Deleted', Suspect: 'Suspect', TooNew: 'TooNew' };
  var MIN_AGE_DAYS = 3;
  var LEDGER_KEY = 'nikki.ledger.v1';
  var LANG_KEY = 'nikki.lang.v1';
  var THUMB_W = 460;

  /* 桌面版直链。GitHub 用 releases/latest/download/<资产名>，能 302 到当前版本的真实资产、
     发新版不用改地址。Gitee 的 latest 别名会把 latest 当成分支名、跳到源码归档路径而 404，
     所以 Gitee 必须写死具体 tag（releases/download/<tag>/<资产名>）——
     发新版时这里的 tag 要跟着一起改。
     国内 CDN 走 Gitee，GitHub 当备用。
     文案里的 <a class="exe-link"> 统一由 relinkExe() 补地址；index.html 里写死的
     href 则是没有 JS 时的兜底，改地址时两边都要动。 */
  var EXE_URL = 'https://gitee.com/lyrins/InfinityNikki-PhotoCleaner/releases/download/v1.7.0/NikkiPhotoCleaner.exe';
  var EXE_URL_ALT = 'https://github.com/3341284397-max/InfinityNikki-PhotoCleaner/releases/latest/download/NikkiPhotoCleaner.exe';
  /* 第三个入口：网盘整包（exe + GBK 中文说明 + 截图 + 零依赖版）。
     两个代码托管都连不上、或想要一份「给人看」的说明时用它。
     网盘改名/换链接只改这一处和 index.html 里那行兜底 href；提取码在文案里，
     跟着一起改。 */
  var PAN_URL = 'https://pan.baidu.com/s/15jaYZvAnNP8VuFZwOQsp2w';

  /* ---------------- 文案 ---------------- */
  var I18N = {
    zh: {
      'app.title': '无限暖暖 · 照片清理助手',
      'app.sub': '只帮你找出该删的截图，删除这一步永远由你亲手完成',
      'app.github': 'GitHub',

      'intro.eyebrow': '给搭配师的本地小工具',
      'intro.h2': '游戏里删掉的截图，电脑上还在占地方',
      'intro.lede': '在《无限暖暖》里删掉相册照片后，<code>X6Game\\ScreenShot</code> 里的原图不会跟着消失。这个工具帮你把它们挑出来——照片不上传、不联网，全部在你的浏览器里完成。',
      'intro.s1t': '选择游戏文件夹',
      'intro.s1d': '选中 <code>X6Game</code> 这一层，浏览器会向你请求读取权限',
      'intro.s2t': '自动比对',
      'intro.s2d': '把本地截图和相册缓存对一遍，标出游戏里已删除的那些',
      'intro.s3t': '一键移进待清理文件夹',
      'intro.s3d': '勾好之后点「移动照片」，把它们挪到一边，删不删还是你说了算',
      'intro.pick': '选择 X6Game 文件夹',
      'intro.demo': '还没有游戏目录？先看演示',
      'demo.badge': '演示数据',
      'demo.sample': '示意',
      'intro.pickNote': '首次使用会从这里建立一份「相册台账」，之后才能判断哪些是你在游戏里删掉的',
      'intro.privacy': '没有服务器、没有上传、没有统计。断网也能用。',
      'intro.exeT': '想要一键移动、一键还原？用桌面版',
      'intro.exeD': 'Windows 免安装单文件，点一下就把截图挪进待清理文件夹',
      'intro.exeGo': '下载 exe',
      'intro.exeGh': 'GitHub 备用',
      'intro.exePan': '百度网盘（1111）',
      'intro.panTip': '提取码 1111。整包含 exe、中文使用说明、三张截图，以及 exe 被拦时能用的零依赖版',

      'fallback.t': '这个浏览器暂时用不了',
      'fallback.d': '读取本地文件夹需要 <b>桌面版 Chrome 或 Edge（86 以上）</b>。Firefox、Safari 和手机浏览器都不支持这个能力。',
      'fallback.alt': '也可以改用桌面版程序：功能一样，还多一步「把截图移进待清理文件夹」，方便你一次性删除。<a class="exe-link" href="#">下载 Windows 版</a>',

      'ws.home': '返回',
      'ws.homeTip': '回到首页，重新选文件夹',
      'ws.folder': '游戏文件夹',
      'ws.repick': '重新选择',
      'ws.rescan': '重新扫描',
      'ws.onlyPending': '只看待处理',
      'ws.search': '搜索文件名',
      'ws.scanning': '扫描中…',
      'ws.selDel': '勾选全部已删',
      'notice.pick.t': '这些照片可以先挑出来',
      'notice.pick.b': '点上面的「勾选全部已删」，或在照片上逐张点选。工具不会替你勾任何一张。',
      'ws.clear': '取消勾选',
      'ws.selected': '张已勾选',
      'ws.copy': '复制文件名',
      'ws.export': '导出待删清单',
      'ws.script': '移动照片',

      'dlg.title': '把照片挪到一边',
      'dlg.lede': '勾选的 <b>{0}</b> 张会移进「无限暖暖清理助手_待清理照片」。下面三种办法任选一种，都只移动、不删照片。',
      'dlg.pathLabel': '截图文件夹的完整路径（可选）',
      'dlg.pathHint': '浏览器不会把文件夹路径告诉网页，所以这一栏要你自己粘 —— 在资源管理器地址栏点一下就能复制。填了脚本放哪儿都能跑；不填也行，双击时它会让你把文件夹拖进窗口。',
      'dlg.paste': '粘贴',
      'dlg.w1t': '下载脚本，双击运行',
      'dlg.w1tag': '最省事',
      'dlg.w1d': '一个纯文本 .bat，双击就建好文件夹、把照片移进去，跑完还问你要不要把它自己删掉。',
      'dlg.w2t': '复制命令，粘进命令行窗口',
      'dlg.w2tag': '脚本被拦时用这个',
      'dlg.w2d': '没有文件落到磁盘上，安全软件没东西可拦。开始菜单搜 cmd 打开窗口，右键粘贴，回车。',
      'dlg.w3t': '交给你的 AI 助手',
      'dlg.w3tag': '兜底',
      'dlg.w3d': '把这段提示词给能操作电脑的助手（Copilot、Claude、ChatGPT 之类），它照着自己就把文件搬过去了。',
      'dlg.cmd': '复制命令',
      'dlg.pt': '复制提示词',
      'dlg.save': '下载 txt',
      'dlg.peek': '先看看脚本长什么样',
      'dlg.note': '三条路都是纯本地操作：只移动、不删除。照片搬到「待清理照片」文件夹之后，删不删、什么时候删，全由你在资源管理器里决定。',
      'dlg.close': '关闭',
      'dlg.download': '下载脚本',
      'dlg.more': '… 后面还有 {0} 行，格式和上面一样',
      'act.scripted': '脚本已下载 —— 拖进 ScreenShot 文件夹再双击',
      'act.cmdCopied': '命令已复制 —— 打开 cmd 窗口，右键粘贴，回车',
      'act.ptCopied': '提示词已复制，发给你的助手就行',
      'act.saved': '已下载 {0}',
      'act.pasteFail': '浏览器不让读剪贴板，直接按 Ctrl+V 吧',
      'act.noScript': '脚本组件没加载出来，刷新页面再试',

      'st.keep': '仍在相册',
      'st.del': '游戏里已删',
      'st.sus': '无法确认',
      'st.new': '太新跳过',

      'foot.disclaimer': '非官方工具，与叠纸游戏 / 《无限暖暖》官方无关。所有判定只依据本地缓存的比对结果，不读取账号信息。',
      'foot.oss': '完全开源，MIT 协议。源码公开在 <a class="repo-link" href="https://github.com/3341284397-max/InfinityNikki-PhotoCleaner" target="_blank" rel="noopener">GitHub</a> 和 <a class="repo-link" href="https://gitee.com/lyrins/InfinityNikki-PhotoCleaner" target="_blank" rel="noopener">Gitee</a>，每一行怎么判定的都能自己去核对，也可以拿去改。作者 Lyrins。',
      'foot.tip': '不想下载脚本？<a class="exe-link" href="#">下载桌面版</a>，点一下就把截图挪进待清理文件夹，还能一键还原。',

      'notice.first.t': '第一次使用，先建立台账',
      'notice.first.b': '这次只能记录「哪些截图曾经在相册里」，还判断不出谁被删了。在游戏里删几张照片后回到这里重新扫描，就能看到「游戏里已删」。',
      'notice.empty.t': '没读到相册缓存',
      'notice.empty.b': 'Saved\\GamePlayPhotos 下没有找到 NikkiPhotos_* 缓存目录。如果你从没在游戏里用过相册，这是正常的；如果用过，可能是选了错误的文件夹。',
      'notice.ratio.t': '待删比例偏高',
      'notice.ratio.b': '超过 70% 的截图被判定为待删，这个比例异常，建议先逐张核对再动手。',
      'notice.gate.t': '这次不建议直接删',
      'notice.gate.b': '相册缓存为空时比对结果不可信，本次只用于查看。',

      'empty.none.t': '这个文件夹里没有截图',
      'empty.none.b': 'ScreenShot 目录下没有 .jpg / .jpeg / .png 文件。',
      'empty.filter.t': '没有符合筛选条件的截图',
      'empty.filter.b': '试试关掉「只看待处理」或清空搜索框。',

      'act.copied': '已复制 {0} 个文件名',
      'act.copyFail': '复制失败，请手动选择文本',
      'act.exported': '已导出 {0} 行清单',
      'act.nothing': '还没有勾选任何截图',
      'act.pickCancel': '已取消选择',

      'err.notX6': '这个文件夹里没有找到 ScreenShot 与 Saved，请选择游戏目录下的 X6Game 文件夹。',
      'err.noScreenShot': '在 X6Game 下没有找到 ScreenShot 文件夹。',
      'err.scan': '读取失败：{0}',

      'meta.count': '{0} 张截图 · {1} 个缓存目录',
      'meta.scanned': '扫描于 {0}',

      'csv.header': '文件名,状态,判定理由,拍摄时间,大小(KB)',
      'reason.InAlbum': '游戏相册中仍存在',
      'reason.Deleted': '曾出现在相册中，现已从相册缓存消失',
      'reason.Suspect': '相册缓存中从未见过（可能删过，也可能尚未同步）',
      'reason.TooNew': '拍摄不足 {0} 天，可能还没同步到相册；要清理请手动勾选'
    },

    en: {
      'app.title': 'Infinity Nikki · Photo Cleanup Assistant',
      'app.sub': 'It finds the screenshots worth deleting — you always do the deleting',
      'app.github': 'GitHub',

      'intro.eyebrow': 'A small local tool for stylists',
      'intro.h2': 'Screenshots you deleted in game are still taking up space',
      'intro.lede': 'When you delete an album photo in Infinity Nikki, the original file in <code>X6Game\\ScreenShot</code> stays behind. This tool picks those out for you — nothing is uploaded, nothing leaves your browser.',
      'intro.s1t': 'Pick the game folder',
      'intro.s1d': 'Select the <code>X6Game</code> folder; the browser will ask for read permission',
      'intro.s2t': 'Compare automatically',
      'intro.s2d': 'Local screenshots are matched against the album cache, highlighting the ones you deleted',
      'intro.s3t': 'Move them aside in one go',
      'intro.s3d': 'Tick what you want, press “Move photos” and they are moved aside - deleting stays your call',
      'intro.pick': 'Select the X6Game folder',
      'intro.demo': 'No game folder handy? See the demo',
      'demo.badge': 'Demo data',
      'demo.sample': 'Sample',
      'intro.pickNote': 'The first run builds a local "album ledger". Only after that can it tell which screenshots you deleted in game.',
      'intro.privacy': 'No server, no upload, no tracking. Works offline.',
      'intro.exeT': 'Want one-click move and one-click restore? Use the desktop app',
      'intro.exeD': 'A single portable Windows exe — it moves the screenshots into the review folder for you',
      'intro.exeGo': 'Download exe',
      'intro.exeGh': 'GitHub mirror',
      'intro.exePan': 'Baidu Disk (1111)',
      'intro.panTip': 'Code 1111. A bundle with the exe, screenshots and a no-dependency fallback for blocked exe',

      'fallback.t': 'This browser cannot do it yet',
      'fallback.d': 'Reading a local folder needs <b>desktop Chrome or Edge (86+)</b>. Firefox, Safari and mobile browsers do not support it.',
      'fallback.alt': 'You can also use the desktop app: same detection, plus it moves the files into a review folder so you can delete them in one go. <a class="exe-link" href="#">Download the Windows build</a>',

      'ws.home': 'Home',
      'ws.homeTip': 'Back to the start page and pick another folder',
      'ws.folder': 'Game folder',
      'ws.repick': 'Change',
      'ws.rescan': 'Rescan',
      'ws.onlyPending': 'Pending only',
      'ws.search': 'Search file name',
      'ws.scanning': 'Scanning...',
      'ws.selDel': 'Select all deleted',
      'notice.pick.t': 'Some photos are ready to be picked',
      'notice.pick.b': 'Use "Select all deleted" above, or tick photos one by one. Nothing is ever pre-selected.',
      'ws.clear': 'Clear selection',
      'ws.selected': 'selected',
      'ws.copy': 'Copy file names',
      'ws.export': 'Export list',
      'ws.script': 'Move photos',

      'dlg.title': 'Move the photos aside',
      'dlg.lede': 'The <b>{0}</b> selected photos go into a review folder. Pick any one of the three ways below — all of them move files, none delete anything.',
      'dlg.pathLabel': 'Full path of the screenshot folder (optional)',
      'dlg.pathHint': 'Browsers never reveal a folder path to the page, so paste it here yourself — click the address bar in Explorer and copy. With a path filled in the script runs from anywhere; without it, the script will ask you to drag the folder in.',
      'dlg.paste': 'Paste',
      'dlg.w1t': 'Download the script, double-click it',
      'dlg.w1tag': 'Simplest',
      'dlg.w1d': 'A plain-text .bat: creates the folder, moves the photos in, then asks whether to delete itself.',
      'dlg.w2t': 'Copy the commands into a console window',
      'dlg.w2tag': 'Use this if the script is blocked',
      'dlg.w2d': 'Nothing is written to disk, so there is no file for security software to block. Open Command Prompt from the Start menu, right-click to paste, press Enter.',
      'dlg.w3t': 'Hand it to your AI assistant',
      'dlg.w3tag': 'Last resort',
      'dlg.w3d': 'Give this prompt to an assistant that can touch files (Copilot, Claude, ChatGPT…) and it will move them for you.',
      'dlg.cmd': 'Copy commands',
      'dlg.pt': 'Copy prompt',
      'dlg.save': 'Save as .txt',
      'dlg.peek': 'Preview the script first',
      'dlg.note': 'All three routes are purely local: move only, never delete. Once the photos sit in the review folder, whether and when to delete them is entirely your call in Explorer.',
      'dlg.close': 'Close',
      'dlg.download': 'Download script',
      'dlg.more': '... {0} more lines, all the same shape',
      'act.scripted': 'Script downloaded — drop it into ScreenShot and double-click',
      'act.cmdCopied': 'Commands copied — open Command Prompt, right-click to paste, press Enter',
      'act.ptCopied': 'Prompt copied — hand it to your assistant',
      'act.saved': 'Downloaded {0}',
      'act.pasteFail': 'The browser would not let me read the clipboard — press Ctrl+V instead',
      'act.noScript': 'The script module did not load — reload the page',

      'st.keep': 'In album',
      'st.del': 'Deleted in game',
      'st.sus': 'Unsure',
      'st.new': 'Too new',

      'foot.disclaimer': 'Unofficial tool, not affiliated with Papergames or Infinity Nikki. Detection relies only on local cache comparison; no account data is read.',
      'foot.oss': 'Fully open source under the MIT licence. The code lives on <a class="repo-link" href="https://github.com/3341284397-max/InfinityNikki-PhotoCleaner" target="_blank" rel="noopener">GitHub</a> and <a class="repo-link" href="https://gitee.com/lyrins/InfinityNikki-PhotoCleaner" target="_blank" rel="noopener">Gitee</a> — you can audit exactly how a photo gets flagged, or fork it. Built by Lyrins.',
      'foot.tip': 'Prefer no script at all? <a class="exe-link" href="#">Get the desktop app</a> — it moves screenshots into the review folder in one click, and restores them just as fast.',

      'notice.first.t': 'First run — building the ledger',
      'notice.first.b': 'This pass only records which screenshots were in the album. Delete a few photos in game, come back and rescan, and they will show up as "Deleted in game".',
      'notice.empty.t': 'No album cache found',
      'notice.empty.b': 'No NikkiPhotos_* folder under Saved\\GamePlayPhotos. That is normal if you never used the in-game album; otherwise you may have picked the wrong folder.',
      'notice.ratio.t': 'Unusually many flagged',
      'notice.ratio.b': 'Over 70% of screenshots are flagged for deletion. That ratio is abnormal — check them one by one before acting.',
      'notice.gate.t': 'Better not to delete yet',
      'notice.gate.b': 'With an empty album cache the comparison cannot be trusted, so this run is view-only.',

      'empty.none.t': 'No screenshots in this folder',
      'empty.none.b': 'There are no .jpg / .jpeg / .png files under ScreenShot.',
      'empty.filter.t': 'Nothing matches the filter',
      'empty.filter.b': 'Try turning off "Pending only" or clearing the search box.',

      'act.copied': '{0} file names copied',
      'act.copyFail': 'Copy failed — please select the text manually',
      'act.exported': '{0} rows exported',
      'act.nothing': 'Nothing is selected yet',
      'act.pickCancel': 'Selection cancelled',

      'err.notX6': 'ScreenShot and Saved were not found inside this folder. Please pick the X6Game folder in the game directory.',
      'err.noScreenShot': 'No ScreenShot folder was found under X6Game.',
      'err.scan': 'Could not read the folder: {0}',

      'meta.count': '{0} screenshots · {1} cache folders',
      'meta.scanned': 'Scanned at {0}',

      'csv.header': 'File name,Status,Reason,Taken,Size (KB)',
      'reason.InAlbum': 'Still present in the in-game album',
      'reason.Deleted': 'Was in the album before and is now gone from the album cache',
      'reason.Suspect': 'Never seen in the album cache (may have been deleted, or not synced yet)',
      'reason.TooNew': 'Taken less than {0} days ago and may not be synced yet; tick it manually if you still want to clean it'
    }
  };

  /* ---------------- 状态 ---------------- */
  var state = {
    lang: 'zh',
    dir: null,
    items: [],
    byName: new Map(),
    ledger: {},
    selected: new Set(),
    onlyPending: false,
    query: '',
    albumDirs: 0,
    firstRun: false,
    isDemo: false
  };

  var el = {};
  var thumbObserver = null;
  var urlCache = new Map();

  /* ---------------- 工具 ---------------- */
  function t(key) {
    var dict = I18N[state.lang] || I18N.zh;
    return dict[key] != null ? dict[key] : (I18N.zh[key] != null ? I18N.zh[key] : key);
  }

  function tf(key) {
    var args = Array.prototype.slice.call(arguments, 1);
    var s = t(key);
    return s.replace(/\{(\d+)\}/g, function (m, i) {
      return args[i] != null ? args[i] : m;
    });
  }

  function fmtSize(bytes) {
    if (bytes >= 1048576) return (bytes / 1048576).toFixed(1) + ' MB';
    if (bytes >= 1024) return Math.round(bytes / 1024) + ' KB';
    return bytes + ' B';
  }

  function fmtTime(d) {
    var p = function (n) { return n < 10 ? '0' + n : '' + n; };
    return d.getFullYear() + '-' + p(d.getMonth() + 1) + '-' + p(d.getDate()) +
      ' ' + p(d.getHours()) + ':' + p(d.getMinutes());
  }

  function esc(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;')
      .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  function statusLabel(code) {
    if (code === STATUS.InAlbum) return t('st.keep');
    if (code === STATUS.Deleted) return t('st.del');
    if (code === STATUS.Suspect) return t('st.sus');
    return t('st.new');
  }

  function statusClass(code) {
    if (code === STATUS.InAlbum) return 'keep';
    if (code === STATUS.Deleted) return 'del';
    if (code === STATUS.Suspect) return 'sus';
    return 'new';
  }

  function reasonText(code) {
    if (code === STATUS.TooNew) return tf('reason.TooNew', MIN_AGE_DAYS);
    return t('reason.' + code);
  }

  function toast(msg, kind) {
    var box = document.getElementById('toast');
    if (!box) {
      box = document.createElement('div');
      box.id = 'toast';
      box.className = 'toast';
      document.body.appendChild(box);
    }
    box.textContent = msg;
    box.className = 'toast show' + (kind === 'bad' ? ' bad' : '');
    clearTimeout(box._timer);
    box._timer = setTimeout(function () { box.className = 'toast'; }, 2400);
  }

  /* ---------------- 语言 ---------------- */
  /* 文案里的 <a class="exe-link" href="#"> 只是占位，真正的下载地址统一在这里补。
     放在 applyLang 里调，因为 innerHTML 一换，原来的 <a> 节点就没了。 */
  /* i18n 一换文案，原来那些 <a> 节点就被重建了，href 得重新补 */
  function relinkExe() {
    document.querySelectorAll('a.exe-link, a.exe-btn').forEach(function (a) { a.href = EXE_URL; });
    document.querySelectorAll('a.exe-btn-sub:not(.exe-pan)').forEach(function (a) { a.href = EXE_URL_ALT; });
    document.querySelectorAll('a.exe-pan').forEach(function (a) { a.href = PAN_URL; });
  }

  function applyLang() {
    document.documentElement.lang = state.lang === 'en' ? 'en' : 'zh-CN';
    document.title = t('app.title');

    document.querySelectorAll('[data-i18n]').forEach(function (node) {
      var key = node.getAttribute('data-i18n');
      var v = t(key);
      if (/<[a-z]/i.test(v)) node.innerHTML = v;
      else node.textContent = v;
    });

    document.querySelectorAll('[data-i18n-ph]').forEach(function (node) {
      node.setAttribute('placeholder', t(node.getAttribute('data-i18n-ph')));
    });

    document.querySelectorAll('[data-i18n-title]').forEach(function (node) {
      node.setAttribute('title', t(node.getAttribute('data-i18n-title')));
    });

    relinkExe();

    el.btnLang.textContent = state.lang === 'en' ? '中文' : 'EN';

    if (state.items.length) { render(); updateNotice(); }
    // 对话框里的正文是脚本生成时写死的，切语言就收起来，免得中英混在一张卡片里
    if (el.dlgScript && !el.dlgScript.hidden) closeScriptDialog();
  }

  function toggleLang() {
    state.lang = state.lang === 'en' ? 'zh' : 'en';
    try { localStorage.setItem(LANG_KEY, state.lang); } catch (e) { }
    applyLang();
  }

  /* ---------------- 台账 ---------------- */
  function loadLedger() {
    try {
      var raw = localStorage.getItem(LEDGER_KEY);
      state.ledger = raw ? JSON.parse(raw) : {};
    } catch (e) { state.ledger = {}; }
  }

  function saveLedger() {
    try { localStorage.setItem(LEDGER_KEY, JSON.stringify(state.ledger)); } catch (e) { }
  }

  /* ---------------- 目录解析 ---------------- */
  async function hasSub(dir, name) {
    try { await dir.getDirectoryHandle(name); return true; }
    catch (e) { return false; }
  }

  /** 允许用户选 X6Game，也允许选游戏根目录（里面再找 X6Game） */
  async function resolveGameDir(handle) {
    if (await hasSub(handle, 'ScreenShot') && await hasSub(handle, 'Saved')) return handle;
    if (await hasSub(handle, 'X6Game')) {
      var inner = await handle.getDirectoryHandle('X6Game');
      if (await hasSub(inner, 'ScreenShot')) return inner;
    }
    return null;
  }

  /* ---------------- 扫描 ---------------- */
  async function scan() {
    var gameDir = await resolveGameDir(state.dir);
    if (!gameDir) { toast(t('err.notX6'), 'bad'); return; }

    var ssDir;
    try { ssDir = await gameDir.getDirectoryHandle('ScreenShot'); }
    catch (e) { toast(t('err.noScreenShot'), 'bad'); return; }

    // 台账在这次扫描之前是否为空——决定了「首次运行」提示，必须在写入新记录之前判断
    var ledgerWasEmpty = Object.keys(state.ledger).length === 0;

    var files = [];
    for await (var entry of ssDir.values()) {
      if (entry.kind === 'file' && /\.(jpe?g|png)$/i.test(entry.name)) files.push(entry);
    }
    files.sort(function (a, b) { return a.name < b.name ? -1 : 1; });

    // 相册缓存名单
    var albumNames = new Set();
    var albumDirs = 0;
    try {
      var saved = await gameDir.getDirectoryHandle('Saved');
      var gp = await saved.getDirectoryHandle('GamePlayPhotos');
      for await (var uid of gp.values()) {
        if (uid.kind !== 'directory') continue;
        for await (var sub of uid.values()) {
          if (sub.kind !== 'directory' || !/^NikkiPhotos_/i.test(sub.name)) continue;
          albumDirs++;
          for await (var f of sub.values()) {
            if (f.kind === 'file') albumNames.add(f.name.toLowerCase());
          }
        }
      }
    } catch (e) { /* 没有 Saved / GamePlayPhotos，按 0 处理 */ }

    state.albumDirs = albumDirs;

    // 读文件元信息
    var now = Date.now();
    var items = [];
    for (var i = 0; i < files.length; i++) {
      var f = files[i];
      var file;
      try { file = await f.getFile(); } catch (e) { continue; }

      var key = f.name.toLowerCase();
      var inAlbum = albumNames.has(key);
      var led = state.ledger[key];
      var ever = !!(led && led.ever);
      var status = inAlbum ? STATUS.InAlbum : (ever ? STATUS.Deleted : STATUS.Suspect);

      var ageDays = (now - file.lastModified) / 86400000;
      if (status !== STATUS.InAlbum && ageDays < MIN_AGE_DAYS) status = STATUS.TooNew;

      if (inAlbum) {
        state.ledger[key] = { first: (led && led.first) || now, last: now, ever: true };
      } else if (led) {
        led.last = now;
      }

      items.push({
        name: f.name,
        handle: f,
        size: file.size,
        time: new Date(file.lastModified),
        ageDays: Math.round(ageDays * 10) / 10,
        status: status,
        file: file
      });
    }

    state.firstRun = ledgerWasEmpty;
    saveLedger();

    items.sort(function (a, b) {
      var order = { Deleted: 0, Suspect: 1, TooNew: 2, InAlbum: 3 };
      var d = order[a.status] - order[b.status];
      return d !== 0 ? d : b.time - a.time;
    });

    state.items = items;
    state.byName = new Map();
    items.forEach(function (it) { state.byName.set(it.name, it); });
    // 1.7.0 起不再自动勾选：从 U 盘拷回来的备份文件名和台账一致，
    // 同样会被判成「已删除」，一扫描就整批预选、点一下就把备份搬走了。
    state.selected = new Set();

    enterWorkspace();
    el.dirName.textContent = state.dir.name;
    el.dirMeta.textContent = tf('meta.count', items.length, albumDirs) + ' · ' + tf('meta.scanned', fmtTime(new Date()));

    render();
    updateNotice();
  }

  /* ---------------- 提示条 ---------------- */
  function updateNotice() {
    var del = count(STATUS.Deleted), sus = count(STATUS.Suspect), total = state.items.length;
    var gate = null;

    if (state.albumDirs === 0) {
      gate = ['notice.empty.t', 'notice.empty.b'];
    } else if (state.firstRun) {
      gate = ['notice.first.t', 'notice.first.b'];
    } else if (total >= 5 && (del + sus) / total > 0.7) {
      gate = ['notice.ratio.t', 'notice.ratio.b'];
    } else if (del > 0 && state.selected.size === 0) {
      // 1.7.0 起不再预勾选，这里得说一句「下一步做什么」，否则用户面对一屏照片会停在原地
      gate = ['notice.pick.t', 'notice.pick.b'];
    }

    if (!gate) { el.notice.hidden = true; return; }
    el.notice.hidden = false;
    el.noticeTitle.textContent = t(gate[0]);
    el.noticeBody.textContent = t(gate[1]);
  }

  function count(code) {
    return state.items.filter(function (i) { return i.status === code; }).length;
  }

  /* ---------------- 渲染 ---------------- */
  function visibleItems() {
    var q = state.query.trim().toLowerCase();
    return state.items.filter(function (it) {
      if (state.onlyPending && it.status === STATUS.InAlbum) return false;
      if (q && it.name.toLowerCase().indexOf(q) === -1) return false;
      return true;
    });
  }

  function render() {
    el.nKeep.textContent = count(STATUS.InAlbum);
    el.nDel.textContent = count(STATUS.Deleted);
    el.nSus.textContent = count(STATUS.Suspect);
    el.nNew.textContent = count(STATUS.TooNew);

    var list = visibleItems();
    el.grid.innerHTML = '';
    el.empty.hidden = list.length > 0;
    if (!list.length) {
      var isFiltered = state.items.length > 0;
      el.empty.innerHTML = '<b>' + t(isFiltered ? 'empty.filter.t' : 'empty.none.t') + '</b>' +
        esc(t(isFiltered ? 'empty.filter.b' : 'empty.none.b'));
    }

    var frag = document.createDocumentFragment();
    list.forEach(function (it) {
      frag.appendChild(makeCard(it));
    });
    el.grid.appendChild(frag);

    observeThumbs();
    updateActionBar();
  }

  function makeCard(it) {
    var card = document.createElement('article');
    card.className = 'card' + (state.selected.has(it.name) ? ' is-selected' : '');
    card.dataset.name = it.name;
    card.innerHTML =
      '<div class="card-thumb skeleton" data-thumb>' +
      '<span class="badge ' + statusClass(it.status) + ' card-badge">' + esc(statusLabel(it.status)) + '</span>' +
      '<span class="card-tick"><svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="#fff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="m5 12.5 4.5 4.5L19 7"/></svg></span>' +
      '</div>' +
      '<div class="card-meta">' +
      '<div class="card-name" title="' + esc(it.name) + '">' + esc(it.name) + '</div>' +
      '<div class="card-sub"><span>' + fmtTime(it.time) + '</span><span>' + fmtSize(it.size) + '</span></div>' +
      '</div>';

    card.addEventListener('click', function (e) {
      e.preventDefault();
      toggleSelect(it.name);
    });
    card.addEventListener('dblclick', function (e) {
      e.preventDefault();
      openModal(it);
    });
    return card;
  }

  function toggleSelect(name) {
    if (state.selected.has(name)) state.selected.delete(name);
    else state.selected.add(name);

    var card = el.grid.querySelector('[data-name="' + CSS.escape(name) + '"]');
    if (card) card.classList.toggle('is-selected', state.selected.has(name));
    updateActionBar();
    updateNotice();   // 「这些照片可以先挑出来」在勾第一张之后就该消失
  }

  function updateActionBar() {
    var n = state.selected.size;
    el.actionbar.hidden = n === 0;
    el.selCount.textContent = n;
    // 操作条是 fixed 的，会盖住页脚最后一行：给它让出等高的底部空间
    document.body.classList.toggle('has-action', n > 0);
  }

  /* ---------------- 缩略图 ---------------- */
  function observeThumbs() {
    if (thumbObserver) thumbObserver.disconnect();
    thumbObserver = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        thumbObserver.unobserve(en.target);
        var card = en.target.closest('.card');
        var item = state.byName.get(card.dataset.name);
        if (item) loadThumb(card.querySelector('[data-thumb]'), item);
      });
    }, { rootMargin: '320px 0px' });

    el.grid.querySelectorAll('[data-thumb]').forEach(function (node) {
      thumbObserver.observe(node);
    });
  }

  function loadThumb(holder, item) {
    if (!holder || holder.dataset.done) return;
    holder.dataset.done = '1';

    // 已经生成过就复用，切换语言重建卡片时不用再来一遍
    var cached = urlCache.get(item.name);
    if (cached) { applyThumb(holder, item, cached); return; }

    var url = URL.createObjectURL(item.file);
    var img = new Image();
    img.decoding = 'async';
    img.onload = function () {
      try {
        var ratio = Math.min(THUMB_W / img.naturalWidth, 1);
        var w = Math.max(1, Math.round(img.naturalWidth * ratio));
        var h = Math.max(1, Math.round(img.naturalHeight * ratio));
        var cv = document.createElement('canvas');
        cv.width = w; cv.height = h;
        cv.getContext('2d').drawImage(img, 0, 0, w, h);
        cv.toBlob(function (blob) {
          URL.revokeObjectURL(url);
          var turl = URL.createObjectURL(blob);
          urlCache.set(item.name, turl);
          applyThumb(holder, item, turl);
        }, 'image/jpeg', 0.84);
      } catch (e) {
        URL.revokeObjectURL(url);
        holder.classList.remove('skeleton');
      }
    };
    img.onerror = function () {
      URL.revokeObjectURL(url);
      holder.classList.remove('skeleton');
    };
    img.src = url;
  }

  function applyThumb(holder, item, url) {
    var node = new Image();
    node.alt = item.name;
    node.onload = function () { node.classList.add('loaded'); };
    node.src = url;
    holder.classList.remove('skeleton');
    holder.appendChild(node);
  }

  /* ---------------- 大图 ---------------- */
  var modalTempUrl = null;

  function openModal(item) {
    var src = urlCache.get(item.name);
    if (!src) {
      // 没用缩略图缓存的，直接用原图，看得更清楚
      modalTempUrl = URL.createObjectURL(item.file);
      src = modalTempUrl;
    }
    el.modalImg.src = src;
    el.modalBadge.textContent = statusLabel(item.status);
    el.modalBadge.className = 'badge ' + statusClass(item.status);
    el.modalName.textContent = item.name;
    el.modalInfo.textContent = fmtTime(item.time) + ' · ' + fmtSize(item.size) + ' · ' + reasonText(item.status);
    el.modal.hidden = false;
    document.body.style.overflow = 'hidden';
  }

  function closeModal() {
    el.modal.hidden = true;
    el.modalImg.removeAttribute('src');
    if (modalTempUrl) { URL.revokeObjectURL(modalTempUrl); modalTempUrl = null; }
    document.body.style.overflow = '';
  }

  /* ---------------- 导出 ---------------- */
  function selectedItems() {
    return state.items.filter(function (i) { return state.selected.has(i.name); });
  }

  function copyNames() {
    var list = selectedItems();
    if (!list.length) { toast(t('act.nothing')); return; }
    var text = list.map(function (i) { return i.name; }).join('\n');
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function () {
        toast(tf('act.copied', list.length));
      }).catch(function () { toast(t('act.copyFail'), 'bad'); });
    } else {
      toast(t('act.copyFail'), 'bad');
    }
  }

  function exportCsv() {
    var list = selectedItems();
    if (!list.length) { toast(t('act.nothing')); return; }

    var rows = [t('csv.header')];
    list.forEach(function (i) {
      rows.push([
        '"' + i.name.replace(/"/g, '""') + '"',
        '"' + statusLabel(i.status) + '"',
        '"' + reasonText(i.status).replace(/"/g, '""') + '"',
        fmtTime(i.time),
        Math.round(i.size / 1024)
      ].join(','));
    });

    var blob = new Blob(['\ufeff' + rows.join('\r\n')], { type: 'text/csv;charset=utf-8' });
    var a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'nikki-photos-to-delete_' + new Date().toISOString().slice(0, 10) + '.csv';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(a.href); }, 4000);
    toast(tf('act.exported', list.length));
  }

  /* ---------------- 移动照片：三条路 ----------------
     浏览器碰不到「移动文件」，所以给出三条路：
       1. 下载 .bat，Windows 自己跑；
       2. 复制命令，粘进 cmd 窗口 —— 不落盘，被安全软件拦的时候走这条；
       3. 复制提示词，交给用户自己的 AI 助手。
     生成逻辑全在 script-gen.js，这里只管取名单、拼界面、下载。 */
  var scriptReady = null;
  var PREVIEW_LINES = 24;
  var PATH_KEY = 'nikki.path.v1';

  function buildPreview(text) {
    var lines = text.split('\r\n');
    if (lines.length <= PREVIEW_LINES) return lines.join('\n');
    return lines.slice(0, PREVIEW_LINES).join('\n') + '\n' +
      tf('dlg.more', lines.length - PREVIEW_LINES);
  }

  function currentPath() {
    return el.dlgPath ? el.dlgPath.value.trim() : '';
  }

  /** 三条路共用同一份名单和路径，任何一处变了就整体重来 */
  function rebuild() {
    if (!window.NikkiScript) return null;
    var names = selectedItems().map(function (i) { return i.name; });
    var opts = { srcPath: currentPath() };
    scriptReady = {
      bat: window.NikkiScript.build(state.lang, names, opts),
      cmd: window.NikkiScript.buildCommands(state.lang, names, opts.srcPath),
      prompt: window.NikkiScript.buildPrompt(state.lang, names, opts.srcPath)
    };
    el.dlgPre.textContent = buildPreview(scriptReady.bat.text);
    return scriptReady;
  }

  function openScriptDialog() {
    var list = selectedItems();
    if (!list.length) { toast(t('act.nothing')); return; }
    if (!window.NikkiScript) { toast(t('act.noScript'), 'bad'); return; }

    el.dlgLede.innerHTML = tf('dlg.lede', list.length);
    try { el.dlgPath.value = localStorage.getItem(PATH_KEY) || ''; } catch (e) { el.dlgPath.value = ''; }
    rebuild();
    el.dlgScript.hidden = false;
    document.body.style.overflow = 'hidden';
    el.dlgGo.focus();
  }

  function closeScriptDialog() {
    el.dlgScript.hidden = true;
    document.body.style.overflow = '';
    scriptReady = null;
  }

  /** 复制：优先 Clipboard API，被拒（或老浏览器）就退回 execCommand */
  function copyPlain(text, okKey) {
    function fallback() {
      var ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.top = '-1000px';
      document.body.appendChild(ta);
      ta.select();
      var ok = false;
      try { ok = document.execCommand('copy'); } catch (e) { }
      document.body.removeChild(ta);
      toast(ok ? t(okKey) : t('act.copyFail'), ok ? '' : 'bad');
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function () {
        toast(t(okKey));
      }).catch(fallback);
    } else {
      fallback();
    }
  }

  /**
   * 存成 .txt。提示词加 BOM（记事本打开中文才不会花）；
   * 命令块不加 —— BOM 会被用户一起复制走，粘进 cmd 时第一行前面多一个看不见的字符，
   * 报一句「不是内部或外部命令」。
   */
  function saveText(name, text, withBom) {
    var blob = new Blob([(withBom ? '\ufeff' : '') + text], { type: 'text/plain;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = name;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
    toast(tf('act.saved', name));
  }

  function downloadScript() {
    if (!scriptReady) return;
    var blob = new Blob([scriptReady.bat.bytes], { type: 'application/octet-stream' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = scriptReady.bat.filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 4000);
    toast(t('act.scripted'));
  }

  /* ---------------- 演示模式 ----------------
     审核者、或只是想先看看效果的人，不必准备游戏目录也能走完整流程。
     全部用 canvas 现画的示意图，不涉及任何游戏内素材。            */
  var DEMO_PALETTES = [
    ['#F7E7B8', '#E8C87A', '#D9B463'],
    ['#DFEDDF', '#AACDB7', '#8FB89E'],
    ['#F7DDD2', '#E0B3A0', '#CE9682'],
    ['#E7E2F1', '#BFB5D9', '#A79CC6'],
    ['#F8E9D6', '#E3C9A5', '#CDAF86'],
    ['#E3EEF0', '#B4CFD4', '#98B7BE']
  ];

  function makeDemoFile(idx, name, ageDays) {
    var cv = document.createElement('canvas');
    cv.width = 900; cv.height = 675;
    var g = cv.getContext('2d');
    var p = DEMO_PALETTES[idx % DEMO_PALETTES.length];

    var grad = g.createLinearGradient(0, 0, 900, 680);
    grad.addColorStop(0, p[0]);
    grad.addColorStop(1, p[1]);
    g.fillStyle = grad;
    g.fillRect(0, 0, 900, 675);

    // 远山
    g.beginPath();
    g.moveTo(0, 600);
    g.bezierCurveTo(220, 455, 430, 690, 900, 520);
    g.lineTo(900, 675); g.lineTo(0, 675);
    g.closePath();
    g.fillStyle = p[2];
    g.globalAlpha = .55;
    g.fill();
    g.globalAlpha = 1;

    // 光斑
    g.beginPath();
    g.arc(735, 150, 86, 0, Math.PI * 2);
    g.fillStyle = 'rgba(255,255,255,.42)';
    g.fill();

    // 小装饰
    g.beginPath();
    g.arc(150, 185, 7, 0, Math.PI * 2);
    g.arc(190, 215, 4, 0, Math.PI * 2);
    g.fillStyle = 'rgba(255,255,255,.6)';
    g.fill();

    g.fillStyle = 'rgba(58,64,58,.55)';
    g.font = '600 25px "Microsoft YaHei", "PingFang SC", sans-serif';
    g.fillText(t('demo.sample') + ' ' + (idx + 1), 56, 612);

    // 同步转 Blob：canvas.toBlob 是异步的，12 张串行会明显空等（无头环境
    // 甚至等不到）。toDataURL + base64 解码是同步的，一次性就绪。
    return new File([dataUrlToBlob(cv.toDataURL('image/jpeg', .9))], name, {
      type: 'image/jpeg',
      lastModified: Date.now() - ageDays * 86400000
    });
  }

  function dataUrlToBlob(dataUrl) {
    var parts = dataUrl.split(',');
    var mime = parts[0].match(/:(.*?);/)[1];
    var bin = atob(parts[1]);
    var buf = new Uint8Array(bin.length);
    for (var i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
    return new Blob([buf], { type: mime });
  }

  function startDemo() {
    setBusy(true);
    try {
      var specs = [
        ['2026_10_20_21_24_47_2143244.jpeg', STATUS.Deleted, 9],
        ['2026_10_19_18_03_12_2138771.jpeg', STATUS.Deleted, 10],
        ['2026_10_17_11_42_58_2129903.jpeg', STATUS.Deleted, 12],
        ['2026_10_16_09_15_33_2124418.jpeg', STATUS.Deleted, 13],
        ['2026_10_12_20_31_07_2116730.jpeg', STATUS.Suspect, 17],
        ['2026_10_11_22_08_44_2115092.jpeg', STATUS.Suspect, 18],
        ['2026_10_29_08_12_05_2160140.jpeg', STATUS.TooNew, 0.4],
        ['2026_10_28_19_55_31_2158802.jpeg', STATUS.TooNew, 1.1],
        ['2026_10_24_15_20_09_2151277.jpeg', STATUS.InAlbum, 5],
        ['2026_10_23_12_47_26_2149864.jpeg', STATUS.InAlbum, 6],
        ['2026_10_22_17_33_51_2148205.jpeg', STATUS.InAlbum, 7],
        ['2026_10_21_10_06_14_2146922.jpeg', STATUS.InAlbum, 8]
      ];

      var items = [];
      for (var i = 0; i < specs.length; i++) {
        var name = specs[i][0], status = specs[i][1], age = specs[i][2];
        var file = makeDemoFile(i, name, age);
        items.push({
          name: name,
          handle: null,
          file: file,
          size: file.size,
          time: new Date(file.lastModified),
          ageDays: age,
          status: status
        });
      }

      items.sort(function (a, b) {
        var order = { Deleted: 0, Suspect: 1, TooNew: 2, InAlbum: 3 };
        var d = order[a.status] - order[b.status];
        return d !== 0 ? d : b.time - a.time;
      });

      state.items = items;
      state.byName = new Map();
      items.forEach(function (it) { state.byName.set(it.name, it); });
      state.selected = new Set();
      state.albumDirs = 3;
      state.firstRun = false;
      state.isDemo = true;

      enterWorkspace();
      el.dirName.textContent = 'X6Game';
      el.dirMeta.textContent = t('demo.badge') + ' · ' + tf('meta.count', items.length, 3);

      render();
      updateNotice();
    } finally {
      setBusy(false);
    }
  }

  /* ---------------- 视图切换 ---------------- */
  // 进工作区时压一条历史记录，这样浏览器自带的「后退」也能退回首页，
  // 不用去刷页面。file:// 下个别浏览器不让改 history，出错就当没这回事。
  function enterWorkspace() {
    el.intro.hidden = true;
    el.workspace.hidden = false;
    try {
      if (!history.state || history.state.view !== 'workspace') {
        history.pushState({ view: 'workspace' }, '', '#scan');
      }
    } catch (e) { /* 忽略 */ }
  }

  /** 只负责把界面收回首页，不碰 history */
  function resetHome() {
    state.items = [];
    state.selected = new Set();
    state.byName = new Map();
    state.query = '';
    state.onlyPending = false;
    state.isDemo = false;
    state.dir = null;

    el.workspace.hidden = true;
    el.actionbar.hidden = true;
    el.intro.hidden = false;
    el.grid.innerHTML = '';
    el.empty.hidden = true;
    el.notice.hidden = true;
    el.search.value = '';
    el.onlyPending.checked = false;
    el.nKeep.textContent = el.nDel.textContent = el.nSus.textContent = el.nNew.textContent = '0';
    el.dirName.textContent = '';
    el.dirMeta.textContent = '';

    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  /** 「返回」按钮：优先走浏览器的后退，键盘上的返回键和它效果一致 */
  function goHome() {
    try {
      if (history.state && history.state.view === 'workspace') { history.back(); return; }
    } catch (e) { /* 忽略 */ }
    resetHome();
  }

  /* ---------------- 事件 ---------------- */
  function setBusy(busy) {
    document.body.classList.toggle('is-busy', busy);
    if (el.btnPick) el.btnPick.disabled = busy;
    if (el.btnRescan) el.btnRescan.disabled = busy;
    if (el.btnRepick) el.btnRepick.disabled = busy;
    var span = el.btnPick && el.btnPick.querySelector('span');
    if (span) span.textContent = busy ? t('ws.scanning') : t('intro.pick');
  }

  async function pick() {
    if (!window.showDirectoryPicker) { el.fallback.hidden = false; return; }
    try {
      var handle = await window.showDirectoryPicker({ id: 'nikki-game', mode: 'read' });
      if (!handle) return;
      state.dir = handle;
      setBusy(true);
      await scan();
    } catch (e) {
      if (e && e.name === 'AbortError') { toast(t('act.pickCancel')); return; }
      toast(tf('err.scan', (e && e.message) || e), 'bad');
    } finally {
      setBusy(false);
    }
  }

  function init() {
    el = {
      intro: document.getElementById('intro'),
      workspace: document.getElementById('workspace'),
      fallback: document.getElementById('fallback'),
      btnPick: document.getElementById('btnPick'),
      btnDemo: document.getElementById('btnDemo'),
      btnHome: document.getElementById('btnHome'),
      btnRepick: document.getElementById('btnRepick'),
      btnRescan: document.getElementById('btnRescan'),
      btnLang: document.getElementById('btnLang'),
      dirName: document.getElementById('dirName'),
      dirMeta: document.getElementById('dirMeta'),
      nKeep: document.getElementById('nKeep'),
      nDel: document.getElementById('nDel'),
      nSus: document.getElementById('nSus'),
      nNew: document.getElementById('nNew'),
      notice: document.getElementById('notice'),
      noticeTitle: document.getElementById('noticeTitle'),
      noticeBody: document.getElementById('noticeBody'),
      onlyPending: document.getElementById('onlyPending'),
      search: document.getElementById('search'),
      btnSelDel: document.getElementById('btnSelDel'),
      btnClear: document.getElementById('btnClear'),
      grid: document.getElementById('grid'),
      empty: document.getElementById('empty'),
      actionbar: document.getElementById('actionbar'),
      selCount: document.getElementById('selCount'),
      btnCopy: document.getElementById('btnCopy'),
      btnExport: document.getElementById('btnExport'),
      btnScript: document.getElementById('btnScript'),
      dlgScript: document.getElementById('dlgScript'),
      dlgMask: document.getElementById('dlgMask'),
      dlgLede: document.getElementById('dlgLede'),
      dlgPre: document.getElementById('dlgPre'),
      dlgPath: document.getElementById('dlgPath'),
      dlgPathPaste: document.getElementById('dlgPathPaste'),
      dlgCmd: document.getElementById('dlgCmd'),
      dlgCmdDl: document.getElementById('dlgCmdDl'),
      dlgPt: document.getElementById('dlgPt'),
      dlgPtDl: document.getElementById('dlgPtDl'),
      dlgCancel: document.getElementById('dlgCancel'),
      dlgGo: document.getElementById('dlgGo'),
      modal: document.getElementById('modal'),
      modalImg: document.getElementById('modalImg'),
      modalBadge: document.getElementById('modalBadge'),
      modalName: document.getElementById('modalName'),
      modalInfo: document.getElementById('modalInfo'),
      modalClose: document.getElementById('modalClose'),
      modalMask: document.getElementById('modalMask')
    };

    // ?lang=en / ?lang=zh 优先，其次是上次选择，最后跟随浏览器语言
    try {
      var qLang = (location.search.match(/[?&]lang=(zh|en)\b/) || [])[1];
      state.lang = qLang
        || localStorage.getItem(LANG_KEY)
        || (/^zh/i.test(navigator.language) ? 'zh' : 'en');
    } catch (e) {
      state.lang = /^zh/i.test(navigator.language) ? 'zh' : 'en';
    }

    loadLedger();
    applyLang();

    if (!window.showDirectoryPicker) {
      el.fallback.hidden = false;
      el.btnPick.disabled = true;
      el.btnPick.style.opacity = '.45';
      el.btnPick.style.cursor = 'not-allowed';
    }

    el.btnPick.addEventListener('click', pick);
    if (el.btnDemo) el.btnDemo.addEventListener('click', startDemo);
    el.btnRepick.addEventListener('click', pick);
    el.btnRescan.addEventListener('click', async function () {
      if (!state.dir) return;
      setBusy(true);
      try { await scan(); } finally { setBusy(false); }
    });
    el.btnLang.addEventListener('click', toggleLang);

    el.onlyPending.addEventListener('change', function () {
      state.onlyPending = el.onlyPending.checked;
      render();
    });

    el.search.addEventListener('input', function () {
      state.query = el.search.value;
      render();
    });

    el.btnSelDel.addEventListener('click', function () {
      visibleItems().forEach(function (i) {
        if (i.status !== STATUS.InAlbum) state.selected.add(i.name);
      });
      render();
      updateNotice();
    });

    el.btnClear.addEventListener('click', function () {
      state.selected.clear();
      render();
      updateNotice();
    });

    el.btnCopy.addEventListener('click', copyNames);
    el.btnExport.addEventListener('click', exportCsv);
    el.btnScript.addEventListener('click', openScriptDialog);
    el.dlgCancel.addEventListener('click', closeScriptDialog);
    el.dlgMask.addEventListener('click', closeScriptDialog);
    el.dlgGo.addEventListener('click', downloadScript);

    if (el.btnHome) el.btnHome.addEventListener('click', goHome);

    // 路径随时可以改，改一次就把 .bat / 命令 / 提示词一起重建
    el.dlgPath.addEventListener('input', function () {
      try { localStorage.setItem(PATH_KEY, currentPath()); } catch (e) { }
      rebuild();
    });

    el.dlgPathPaste.addEventListener('click', function () {
      if (!navigator.clipboard || !navigator.clipboard.readText) {
        toast(t('act.pasteFail'), 'bad');
        return;
      }
      navigator.clipboard.readText().then(function (txt) {
        el.dlgPath.value = (txt || '').trim();
        try { localStorage.setItem(PATH_KEY, el.dlgPath.value); } catch (e) { }
        rebuild();
      }).catch(function () { toast(t('act.pasteFail'), 'bad'); });
    });

    el.dlgCmd.addEventListener('click', function () {
      if (scriptReady) copyPlain(scriptReady.cmd.text, 'act.cmdCopied');
    });
    el.dlgCmdDl.addEventListener('click', function () {
      if (scriptReady) saveText(scriptReady.cmd.filename, scriptReady.cmd.text, false);
    });
    el.dlgPt.addEventListener('click', function () {
      if (scriptReady) copyPlain(scriptReady.prompt.text, 'act.ptCopied');
    });
    el.dlgPtDl.addEventListener('click', function () {
      if (scriptReady) saveText(scriptReady.prompt.filename, scriptReady.prompt.text, true);
    });

    // 浏览器后退键 / 我们自己的「返回」都走这里：把工作区收起来
    window.addEventListener('popstate', function () {
      if (!el.workspace.hidden) resetHome();
    });

    el.modalClose.addEventListener('click', closeModal);
    el.modalMask.addEventListener('click', closeModal);
    document.addEventListener('keydown', function (e) {
      if (e.key !== 'Escape') return;
      if (!el.modal.hidden) closeModal();
      else if (!el.dlgScript.hidden) closeScriptDialog();
    });

    // ?demo=1 直接进入演示：方便截图、也方便没装游戏的人一眼看到效果
    if (/[?&]demo=1/.test(location.search)) startDemo();

    window.addEventListener('beforeunload', function () {
      urlCache.forEach(function (u) { try { URL.revokeObjectURL(u); } catch (e) { } });
    });
  }

  document.addEventListener('DOMContentLoaded', init);
})();
