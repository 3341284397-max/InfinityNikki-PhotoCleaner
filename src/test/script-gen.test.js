/* eslint-disable */
/**
 * 移动脚本生成器的端到端测试（只在 Windows 上有意义）
 *
 *   node src/test/script-gen.test.js
 *
 * 会把生成出来的 .bat 真的丢进临时目录跑一遍，检查：
 *   1. 脚本放在 ScreenShot 里 → 自动认出目录，照片被移进待清理文件夹
 *   2. 原位置已没有的文件 → 计入「跳过」，不算失败
 *   3. 待清理文件夹里已经有同名文件 → 自动改名加 _1，不覆盖
 *   4. 脚本放在别处 → 接受拖进去的路径（带引号）
 *   5. 回答 Y → 脚本把自己删掉
 *   6. 英文版（纯 ASCII）同样能跑
 */
'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');
const gen = require('../../web/script-gen.js');

const NAMES = [
  '2026_10_01_10_00_01_1000001.jpeg',
  '2026_10_02_11_20_33_2000002.jpeg',
  '2026_10_03_12_00_00_3000003.jpeg'
];
const GONE = '2026_10_04_09_09_09_9999999.jpeg';   // 列在脚本里但磁盘上没有

let failed = 0;

function ok(cond, label, extra) {
  if (cond) {
    console.log('  \u2713 ' + label);
  } else {
    failed++;
    console.log('  \u00d7 ' + label + (extra ? '\n      ' + extra : ''));
  }
}

function sandbox(lang, opts) {
  opts = opts || {};
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nikki-script-'));
  const shot = path.join(root, 'X6Game', 'ScreenShot');
  fs.mkdirSync(shot, { recursive: true });
  NAMES.forEach(n => fs.writeFileSync(path.join(shot, n), Buffer.from('jpeg')));

  const built = gen.build(lang, NAMES.concat([GONE]));
  // 脚本放哪儿：ScreenShot 里，或模拟「下载目录」
  const at = opts.outside ? path.join(root, 'Downloads') : shot;
  fs.mkdirSync(at, { recursive: true });
  const script = path.join(at, built.filename);
  fs.writeFileSync(script, Buffer.from(built.bytes));

  return { root, shot, script, built };
}

/** 管道喂给 cmd 的字节要按控制台代码页（936）编，否则中文路径会被当成乱码 */
function gbk(s) {
  return Buffer.from(gen.encodeGbk(s || '\r\n\r\n'));
}

function run(script, stdin, cwd) {
  const p = spawnSync('cmd', ['/c', script], {
    input: gbk(stdin),
    cwd: cwd || path.dirname(script),
    timeout: 30000
  });
  return {
    out: (p.stdout || Buffer.alloc(0)),
    code: p.status
  };
}

function text(buf) {
  return new TextDecoder('gbk').decode(buf);
}

function listing(dir) {
  try { return fs.readdirSync(dir).sort(); } catch (e) { return []; }
}

/* ---------- 1. 脚本就在 ScreenShot 里 ---------- */
console.log('\n[1] 脚本放在 ScreenShot 里，自动认出目录');
{
  const s = sandbox('zh');
  const r = run(s.script, '\r\n\r\n');
  const moved = listing(path.join(s.shot, '无限暖暖清理助手_待清理照片'));
  const left = listing(s.shot).filter(n => /\.jpe?g$/i.test(n));

  ok(moved.length === 3, '3 张被移走', '实际 ' + moved.length + '：' + moved.join(','));
  ok(left.length === 0, '原目录不再有截图', '还剩 ' + left.join(','));
  ok(/跳过（原位置已经没有）：1 张/.test(text(r.out)), '缺失的那张计为跳过');
  ok(fs.existsSync(s.script), '回答「留着」后脚本还在');
}

/* ---------- 2. 同名文件不覆盖 ---------- */
console.log('\n[2] 待清理文件夹里已有同名文件');
{
  const s = sandbox('zh');
  const dst = path.join(s.shot, '无限暖暖清理助手_待清理照片');
  fs.mkdirSync(dst, { recursive: true });
  fs.writeFileSync(path.join(dst, NAMES[0]), Buffer.from('旧的'));

  run(s.script, '\r\n\r\n');
  const moved = listing(dst);
  ok(moved.length === 4, '变成 4 个文件（3 张 + 原有的 1 张）', moved.join(','));
  ok(moved.includes(NAMES[0]), '原文件没被改名');
  ok(moved.some(n => /_1\.jpeg$/.test(n)), '撞名的那张自动加了 _1', moved.join(','));
  ok(fs.readFileSync(path.join(dst, NAMES[0]), 'utf8') === '旧的', '原文件内容没被覆盖');
}

/* ---------- 3. 脚本在下载目录，拖路径进去 ---------- */
console.log('\n[3] 脚本不在 ScreenShot 里，运行时把路径拖进去');
{
  const s = sandbox('zh', { outside: true });
  // 模拟拖拽：路径带引号
  const r = run(s.script, '"' + s.shot + '"\r\n\r\n');
  const moved = listing(path.join(s.shot, '无限暖暖清理助手_待清理照片'));
  ok(moved.length === 3, '拖入路径后照样移走 3 张', '实际 ' + moved.length);
  ok(/截图文件夹/.test(text(r.out)), '回显了识别到的路径');
}

/* ---------- 4. 回答 Y，脚本自删 ---------- */
console.log('\n[4] 回答 Y 删掉脚本自己');
{
  const s = sandbox('zh');
  run(s.script, 'Y\r\n\r\n');
  ok(!fs.existsSync(s.script), '脚本已自删');
  const moved = listing(path.join(s.shot, '无限暖暖清理助手_待清理照片'));
  ok(moved.length === 3, '自删前照片已经移好', '实际 ' + moved.length);
}

/* ---------- 5. 英文版 ---------- */
console.log('\n[5] 英文版（纯 ASCII，任何代码页都能跑）');
{
  const s = sandbox('en');
  const raw = Buffer.from(s.built.bytes);
  ok(raw.every(b => b < 0x80), '全部字节都在 ASCII 范围内', '编码 ' + s.built.encoding);

  run(s.script, '\r\n\r\n');
  const moved = listing(path.join(s.shot, 'NikkiCleaner_ToDelete'));
  ok(moved.length === 3, '3 张被移到 NikkiCleaner_ToDelete', '实际 ' + moved.length);
}

/* ---------- 6. 网页填了路径：脚本放哪都能直接跑 ---------- */
console.log('\n[6] 网页上填好路径，脚本在下载目录里直接双击');
{
  const s = sandbox('zh', { outside: true, srcPath: null });
  const built = gen.build('zh', NAMES.concat([GONE]), { srcPath: s.shot });
  const script = path.join(path.dirname(s.script), built.filename);
  fs.writeFileSync(script, Buffer.from(built.bytes));

  const r = run(script, '\r\n\r\n');   // 不喂路径，全靠脚本里写死的那行
  const moved = listing(path.join(s.shot, '无限暖暖清理助手_待清理照片'));
  ok(moved.length === 3, '不用拖路径就把 3 张移走了', '实际 ' + moved.length);
  ok(!/没有在脚本旁边找到截图文件夹/.test(text(r.out)), '没有弹出「请拖入文件夹」的提示');
}

/* ---------- 7. 复制的 cmd 命令能直接粘进窗口 ---------- */
console.log('\n[7] 「复制命令」的文本粘进 cmd 交互窗口');
{
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nikki-cmd-'));
  const shot = path.join(root, 'X6Game', 'ScreenShot');
  fs.mkdirSync(shot, { recursive: true });
  NAMES.forEach(n => fs.writeFileSync(path.join(shot, n), Buffer.from('jpeg')));

  // 先放一张同名文件，验证命令里的 if exist 分支真的会改名
  const dst = path.join(shot, '无限暖暖清理助手_待清理照片');
  fs.mkdirSync(dst);
  fs.writeFileSync(path.join(dst, NAMES[0]), Buffer.from('旧的'));

  const built = gen.buildCommands('zh', NAMES.concat([GONE]), shot);
  // 交互式 cmd 读 stdin：不带 /c，逐行执行，等价于手动粘贴
  const p = spawnSync('cmd', [], { input: gbk(built.text), timeout: 30000 });
  const out = text(p.stdout || Buffer.alloc(0));

  const moved = listing(dst);
  ok(moved.length === 4, '3 张 + 原有 1 张 = 4 个文件', moved.join(','));
  ok(moved.includes(NAMES[0]), '原有的同名文件没被覆盖');
  ok(moved.some(n => /_1\.jpeg$/.test(n)), '撞名的那张改名成了 _1', moved.join(','));
  ok(fs.readFileSync(path.join(dst, NAMES[0]), 'utf8') === '旧的', '旧文件内容完好');
  ok(!/语法|不是内部或外部命令|The syntax/i.test(out), '命令没有语法错误', out.slice(0, 400));
}

/* ---------- 8. Agent 提示词 ---------- */
console.log('\n[8] 给 AI 助手的提示词');
{
  const p = gen.buildPrompt('zh', NAMES.concat([GONE]), 'D:\\Games\\X6Game\\ScreenShot');
  ok(p.text.includes('D:\\Games\\X6Game\\ScreenShot'), '带上了源目录');
  ok(p.text.includes('无限暖暖清理助手_待清理照片'), '带上了目标目录');
  ok(NAMES.concat([GONE]).every(n => p.text.includes(n)), '4 个文件名一个不落');
  ok(/只做移动，不要删除/.test(p.text), '写明了不许删除');
  ok(/_1\.扩展名/.test(p.text), '写明了撞名怎么处理');
  ok(/共 4 个/.test(p.text), '报了张数');

  const en = gen.buildPrompt('en', NAMES, 'C:\\Games\\X6Game\\ScreenShot');
  ok(!/[\u4e00-\u9fa5]/.test(en.text.replace('NikkiCleaner_ToDelete', '')), '英文版没有中文残留');
}

console.log(failed ? '\n有 ' + failed + ' 项没过\n' : '\n全部通过\n');
process.exit(failed ? 1 : 0);
