#!/usr/bin/env bash
# 无限暖暖照片清理助手 - 回归测试
# 用 net8 程序集直接运行 app/ 源码，绕开 SAC 对新 exe 的拦截。
# 用法：bash run-tests.sh

set -u
cd "$(dirname "$0")"

DLL="bin/Release/net8.0-windows/NikkiTest.dll"
ROOT="$(pwd)/_tmp"
SAMPLE="D:/G2_InfinityNikki/InfinityNikki_APP/InfinityNikki/X6Game/ScreenShot/2026_10_13_21_24_47_2143244.jpeg"

if [ ! -f "$DLL" ]; then echo "先编译：dotnet build -c Release"; exit 1; fi
if [ ! -f "$SAMPLE" ]; then echo "缺少样例照片：$SAMPLE"; exit 1; fi

rm -rf "$ROOT"
export NIKKI_TOOL_ROOT="$ROOT"
# 用例固定跑中文目录（数据目录名由「已存在哪个」决定，不跟界面语言走）
export NIKKI_LANG="${NIKKI_LANG:-zh}"
mkdir -p "$ROOT/无限暖暖清理助手_使用数据"
mkdir -p "$ROOT/X6Game/ScreenShot" "$ROOT/X6Game/Saved/GamePlayPhotos/999/NikkiPhotos_HighQuality"

SS="$ROOT/X6Game/ScreenShot"
AL="$ROOT/X6Game/Saved/GamePlayPhotos/999/NikkiPhotos_HighQuality"
PEND="$SS/无限暖暖清理助手_待清理照片"
DATA="$ROOT/无限暖暖清理助手_使用数据"

run() { dotnet --roll-forward Major "$DLL" "$@"; }

P1="2026_10_01_10_00_01_1000001.jpeg"
P2="2026_10_01_10_00_02_1000002.jpeg"
P3="2026_10_01_10_00_03_1000003.jpeg"
P4="2026_10_01_10_00_04_1000004.jpeg"

for n in $P1 $P2 $P3 $P4; do
  cp "$SAMPLE" "$SS/$n"
  cp "$SS/$n" "$AL/$n"
done

echo "========== 1. 初始扫描：4 张全在相册 =========="
run scan "$SS"

echo
echo "========== 2. 模拟游戏内删掉 P2、P3 =========="
rm -f "$AL/$P2" "$AL/$P3"
run scan "$SS"

echo
echo "========== 3. 只移动勾选的 P2、P3（在册的不动）=========="
run pick "$SS" "$P2" "$P3"
echo "ScreenShot 现有文件："
ls -1 "$SS" | grep -v 无限暖暖 || true

echo
echo "========== 4. 同名冲突：P2 复制回去再移一次 =========="
cp "$PEND/$P2" "$SS/"
run pick "$SS" "$P2"

echo
echo "========== 5. 移动记录（应累积 3 条，不能只留最后一批）=========="
cat "$DATA/最近一次移动记录.txt"

echo
echo "========== 6. 还原：应把 3 张全部移回 =========="
run restore

echo
echo "========== 7. 还原后核对 =========="
# 第 4 步人为复制过一份副本，所以这里是 5 而不是 4；多出的 _还原HHmmss 就是那份副本
echo "ScreenShot 文件数：$(ls -1 "$SS" | wc -l)（预期 5：原件 4 张 + 1 份测试副本）"
echo "待清理目录还在：$([ -d "$PEND" ] && echo 是 || echo 否)（应为 否）"
echo "记录文件还在：$([ -f "$DATA/最近一次移动记录.txt" ] && echo 是 || echo 否)（应为 否）"

echo
echo "========== 8. 太新判定：minAge=3 时新拍的应为「太新」=========="
rm -f "$AL/$P2" "$AL/$P3"
run scan3 "$SS"

echo
echo "========== 9. 手动勾选「太新」的照片也应能移动 =========="
run pick "$SS" "$P2"

echo
echo "========== 10. 相册缓存清空 -> 应触发安全闸门 =========="
rm -f "$AL"/*
run scan "$SS"

echo
echo "========== 11. 记录清理统计 =========="
run survey "$SS"

echo
echo "========== 12. 切换界面语言：目录不得分裂、台账必须连续 =========="
ZH_OUT=$(run lang "$SS" zh)
EN_OUT=$(run lang "$SS" en)
echo "--- 中文界面 ---"
echo "$ZH_OUT" | sed 's/^/  /'
echo "--- 英文界面 ---"
echo "$EN_OUT" | sed 's/^/  /'

ZH_DIR=$(echo "$ZH_OUT" | grep '^DataDir=' | cut -d= -f2-)
EN_DIR=$(echo "$EN_OUT" | grep '^DataDir=' | cut -d= -f2-)
ZH_PEND=$(echo "$ZH_OUT" | grep '^Pending=' | sed 's/^Pending=//; s/  \[.*//')
EN_PEND=$(echo "$EN_OUT" | grep '^Pending=' | sed 's/^Pending=//; s/  \[.*//')
ZH_CNT=$(echo "$ZH_OUT" | grep '^counts=')
EN_CNT=$(echo "$EN_OUT" | grep '^counts=')

FAIL=0
[ "$ZH_DIR" = "$EN_DIR" ] \
  && echo "  [OK] 数据目录一致：$(basename "$ZH_DIR")" \
  || { echo "  [FAIL] 数据目录分裂：$(basename "$ZH_DIR") vs $(basename "$EN_DIR")"; FAIL=1; }

[ "$ZH_PEND" = "$EN_PEND" ] \
  && echo "  [OK] 待清理目录一致：$(basename "$ZH_PEND")" \
  || { echo "  [FAIL] 待清理目录分裂：$(basename "$ZH_PEND") vs $(basename "$EN_PEND")"; FAIL=1; }

[ "$ZH_CNT" = "$EN_CNT" ] \
  && echo "  [OK] 判定结果连续：$ZH_CNT" \
  || { echo "  [FAIL] 判定结果不连续（台账断链）：$ZH_CNT vs $EN_CNT"; FAIL=1; }

echo "  英文界面状态文案：$(echo "$EN_OUT" | grep '^label.Deleted=' | sed 's/^label.Deleted=//')"
[ "$FAIL" = "0" ] && echo "  用例 12 通过" || echo "  用例 12 失败"

echo
echo "========== 13. 反向：英文数据目录已存在时切中文，不得另建中文目录 =========="
FRESH="$(pwd)/_tmp_en"
rm -rf "$FRESH"
mkdir -p "$FRESH/NikkiCleaner_Data"
mkdir -p "$FRESH/X6Game/ScreenShot" "$FRESH/X6Game/Saved/GamePlayPhotos/999/NikkiPhotos_HighQuality"
cp "$SAMPLE" "$FRESH/X6Game/ScreenShot/$P1"
cp "$SAMPLE" "$FRESH/X6Game/Saved/GamePlayPhotos/999/NikkiPhotos_HighQuality/$P1"

# --lang / 环境变量只影响界面语言，目录名只看「已经存在哪一个」
OUT2_ZH=$(NIKKI_TOOL_ROOT="$FRESH" NIKKI_LANG=zh dotnet --roll-forward Major "$DLL" lang "$FRESH/X6Game/ScreenShot" zh)
OUT2_EN=$(NIKKI_TOOL_ROOT="$FRESH" NIKKI_LANG=en dotnet --roll-forward Major "$DLL" lang "$FRESH/X6Game/ScreenShot" en)
echo "--- 中文界面 ---"
echo "$OUT2_ZH" | sed 's/^/  /'
echo "--- 英文界面 ---"
echo "$OUT2_EN" | sed 's/^/  /'

if [ -d "$FRESH/NikkiCleaner_Data" ] && [ ! -d "$FRESH/无限暖暖清理助手_使用数据" ]; then
  echo "  [OK] 只存在英文数据目录，没有另建中文目录"
else
  echo "  [FAIL] 出现了两个数据目录"
  FAIL=1
fi

D2_ZH=$(echo "$OUT2_ZH" | grep '^DataDir=' | cut -d= -f2-)
D2_EN=$(echo "$OUT2_EN" | grep '^DataDir=' | cut -d= -f2-)
if [ "$D2_ZH" = "$D2_EN" ]; then
  echo "  [OK] 两种界面共用同一目录：$(basename "$D2_ZH")"
else
  echo "  [FAIL] 目录不一致：$(basename "$D2_ZH") vs $(basename "$D2_EN")"
  FAIL=1
fi
[ "$FAIL" = "0" ] && echo "  用例 13 通过" || echo "  用例 13 失败"

echo
echo "========== 14-17. 标记体系（外部导入 / 忽略 / 同名不同大小）=========="
TAG="$(pwd)/_tmp_tag"
rm -rf "$TAG"
mkdir -p "$TAG/无限暖暖清理助手_使用数据"
mkdir -p "$TAG/X6Game/ScreenShot" "$TAG/X6Game/Saved/GamePlayPhotos/999/NikkiPhotos_HighQuality"
TS="$TAG/X6Game/ScreenShot"
TA="$TAG/X6Game/Saved/GamePlayPhotos/999/NikkiPhotos_HighQuality"

A="2026_10_01_10_00_01_1000001.jpeg"
B="2026_10_01_10_00_02_1000002.jpeg"
D="2026_10_01_10_00_04_1000004.jpeg"

# A、B 先在相册里；D 也在册，稍后用来测「同名不同大小」
for n in $A $B $D; do
  cp "$SAMPLE" "$TS/$n"
  cp "$TS/$n" "$TA/$n"
done

export NIKKI_TOOL_ROOT="$TAG"
run tag "$TS" > /dev/null         # 第 1 轮：建立台账，三张都记成「曾在相册」

# 模拟：游戏里删掉 B，同时用户从 U 盘把 B 的备份拷回来（创建时间晚于修改时间）
rm -f "$TA/$B"
run mkexternal "$TS" "$B" > /dev/null
# 模拟：游戏里删掉 D
rm -f "$TA/$D"

echo "--- 第 2 轮扫描（A 在册 / B 已删+外部导入 / D 已删）---"
OUT=$(run tag "$TS")
echo "$OUT" | sed 's/^/  /'

grep -q "counts=.*External:1" <<< "$OUT" \
  && echo "  [OK] 外部导入识别出 1 张" \
  || { echo "  [FAIL] 外部导入没识别出来"; FAIL=1; }
grep -q "counts=.*Excluded:1" <<< "$OUT" \
  && echo "  [OK] 被排除 1 张" \
  || { echo "  [FAIL] 排除数不对"; FAIL=1; }
grep -q "$B .*外部导入" <<< "$OUT" \
  && echo "  [OK] B 同时是「已删除」和「外部导入」" \
  || { echo "  [FAIL] B 的标记不对"; FAIL=1; }

echo "--- 15. 标记忽略 B ---"
M1=$(run ignore "$TS" "$B")
echo "$M1" | sed 's/^/  /'
grep -q "Ignored=1" <<< "$M1" \
  && echo "  [OK] B 变成已忽略" \
  || { echo "  [FAIL] 忽略没生效"; FAIL=1; }
grep -q "备份文件=无" <<< "$M1" \
  && echo "  [OK] 首次创建标记文件，没有旧版可备份（符合预期）" \
  || { echo "  [FAIL] 首次就不该有 .bak"; FAIL=1; }

echo "--- 16. 取消忽略 B ---"
M2=$(run unignore "$TS" "$B")
echo "$M2" | sed 's/^/  /'
grep -q "Ignored=0" <<< "$M2" \
  && echo "  [OK] 取消忽略后恢复原判定" \
  || { echo "  [FAIL] 取消失败"; FAIL=1; }
grep -q "备份文件=有" <<< "$M2" \
  && echo "  [OK] 改动前把上一版留成了 .bak" \
  || { echo "  [FAIL] 第二次改动没备份"; FAIL=1; }

echo "--- 17. 同名但大小不同：台账条目必须作废 ---"
SZ1=$(stat -c %s "$TS/$D")
head -c 300000 "$SAMPLE" > "$TS/$D"     # 同名、内容/大小都不同
SZ2=$(stat -c %s "$TS/$D")
echo "  D 原大小=$SZ1 新大小=$SZ2"
OUT3=$(run tag "$TS")
grep -q "$D" <<< "$OUT3" && echo "$OUT3" | grep "$D" | sed 's/^/  /'
grep -q "疑似.*$D" <<< "$OUT3" \
  && echo "  [OK] 同名新文件没继承「已删除」，退成疑似" \
  || { echo "  [FAIL] 同名新文件继承了旧台账（这是数据安全问题）"; FAIL=1; }

unset NIKKI_TOOL_ROOT

echo
echo "========== 18-20. 数据位置（指针文件 / 失效回落 / 同级目录推导）=========="
# 这三条用例故意不设 NIKKI_TOOL_ROOT —— 环境变量优先级最高，留着它就看不出指针文件的作用了。
# NIKKI_POINTER_DIR 把「位置记录」也关进隔离目录，别动真实用户 %APPDATA% 里的那条。
PT="$(pwd)/_tmp_ptr"
P2DIR="$(pwd)/_tmp_ptr_saved"
rm -rf "$PT" "$P2DIR"
mkdir -p "$PT" "$P2DIR/无限暖暖清理助手_使用数据"

# 前面几轮截图/演示跑过之后，exe 同级可能已经躺着数据文件夹了 —— 那会让程序
# 判成「老用户升级」直接沿用，测不到「全新环境」。这里先清掉。
EXEDIR="$(dirname "$DLL")"
rm -rf "$EXEDIR/无限暖暖清理助手_使用数据" "$EXEDIR/NikkiCleaner_Data"

# 记录文件里必须是 Windows 路径（D:\...），MSYS 的 /d/... 程序认不出来
P2WIN="$(cygpath -w "$P2DIR")"
P2MISS="$(cygpath -w "$PT/已经删掉的目录")"

echo "--- 18. 没有任何位置记录：回落到 exe 目录，并标记「需要询问」---"
W1=$(env -u NIKKI_TOOL_ROOT NIKKI_POINTER_DIR="$PT" NIKKI_LANG=zh \
     dotnet --roll-forward Major "$DLL" where)
echo "$W1" | sed 's/^/  /'
grep -q "PointerExists=0" <<< "$W1" \
  && echo "  [OK] 首次启动没有位置记录" \
  || { echo "  [FAIL] 隔离目录里不该有位置记录"; FAIL=1; }
grep -q "AskNeeded=1" <<< "$W1" \
  && echo "  [OK] 会弹窗问用户（这是本次改动的目的）" \
  || { echo "  [FAIL] 没标记需要询问，用户不会看到选择窗口"; FAIL=1; }
grep -q "Source=ExeDefault" <<< "$W1" \
  && echo "  [OK] 兜底用的是 exe 目录（与旧版本行为一致）" \
  || { echo "  [FAIL] 兜底来源不对"; FAIL=1; }

echo "--- 19. 写入位置记录后：下次启动直接用它，不再询问 ---"
printf '# test\n%s\n' "$P2WIN" > "$PT/location.txt"
W2=$(env -u NIKKI_TOOL_ROOT NIKKI_POINTER_DIR="$PT" NIKKI_LANG=zh \
     dotnet --roll-forward Major "$DLL" where)
echo "$W2" | sed 's/^/  /'
grep -q "Source=Saved" <<< "$W2" \
  && echo "  [OK] 位置来自记录文件" \
  || { echo "  [FAIL] 没读到位置记录"; FAIL=1; }
grep -q "AskNeeded=0" <<< "$W2" \
  && echo "  [OK] 不再弹窗（不会每次启动都烦人）" \
  || { echo "  [FAIL] 选过了还在问"; FAIL=1; }
grep -qF "DataDir=$P2WIN" <<< "$W2" \
  && echo "  [OK] 数据目录跟着记录走" \
  || { echo "  [FAIL] 数据目录没跟着记录走"; FAIL=1; }

echo "--- 20. 记录指向的目录没了：当作没选过，回落到 exe 目录 ---"
printf '%s\n' "$P2MISS" > "$PT/location.txt"
W3=$(env -u NIKKI_TOOL_ROOT NIKKI_POINTER_DIR="$PT" NIKKI_LANG=zh \
     dotnet --roll-forward Major "$DLL" where)
grep -q "Source=ExeDefault" <<< "$W3" \
  && echo "  [OK] 失效记录不会把程序卡在一个不存在的路径上" \
  || { echo "  [FAIL] 失效记录没被忽略"; FAIL=1; }
grep -q "AskNeeded=1" <<< "$W3" \
  && echo "  [OK] 顺便重新问一次，用户可以选个好位置" \
  || { echo "  [FAIL] 目录丢了却没重新询问"; FAIL=1; }

echo "--- 21. 与游戏文件夹并列的那一级 ---"
GOT=$(env -u NIKKI_TOOL_ROOT NIKKI_POINTER_DIR="$PT" NIKKI_LANG=zh \
      dotnet --roll-forward Major "$DLL" sibling "D:/G2_InfinityNikki/InfinityNikki_APP/InfinityNikki/X6Game/ScreenShot")
WANT="D:\\G2_InfinityNikki\\InfinityNikki_APP"
echo "  截图目录 → 数据父目录: $GOT"
[ "$GOT" = "$WANT" ] \
  && echo "  [OK] 推导到 InfinityNikki_APP（与 InfinityNikki 文件夹并列）" \
  || { echo "  [FAIL] 推导结果应为 $WANT"; FAIL=1; }

rm -rf "$PT" "$P2DIR"

echo "--- 22. 端到端：带着位置记录跑一次界面，数据必须建在记录目录 ---"
EQ="$(pwd)/_tmp_e2e"
rm -rf "$EQ"
mkdir -p "$EQ/chosen" "$EQ/ptr"
printf '%s\n' "$(cygpath -w "$EQ/chosen")" > "$EQ/ptr/location.txt"
rm -rf "$EXEDIR/无限暖暖清理助手_使用数据" "$EXEDIR/NikkiCleaner_Data"

env -u NIKKI_TOOL_ROOT NIKKI_POINTER_DIR="$(cygpath -w "$EQ/ptr")" NIKKI_LANG=zh \
    dotnet --roll-forward Major "$DLL" shot "$(cygpath -w "$EQ/out.png")" demo > /dev/null 2>&1

[ -d "$EQ/chosen/无限暖暖清理助手_使用数据" ] \
  && echo "  [OK] 数据文件夹建在记录目录下" \
  || { echo "  [FAIL] 记录目录下没建出数据文件夹"; FAIL=1; }
[ -d "$EXEDIR/无限暖暖清理助手_使用数据" ] \
  && { echo "  [FAIL] exe 旁边又建了一份（用户就是想避免这个）"; FAIL=1; } \
  || echo "  [OK] exe 旁边没有再建（桌面不会被弄乱）"
[ -f "$EQ/chosen/无限暖暖清理助手_使用数据/设置.txt" ] \
  && echo "  [OK] 设置文件也写在新位置" \
  || echo "  [--] 这次还没写设置文件（未切换语言，正常）"

rm -rf "$EQ"

echo
echo "全部用例执行完毕。测试目录：$ROOT 与 $TAG"
exit $FAIL
