# -*- coding: utf-8 -*-
"""
打「网盘分享包」。

产物（都在 dist/ 下，不进仓库）：
  dist/网盘分享版/无限暖暖照片清理助手_v<版本>/     解压即用的文件夹
  dist/无限暖暖照片清理助手_v<版本>.zip             整包，适合传网盘后发一个链接

内容：
  无限暖暖照片清理助手.exe      单文件桌面版
  使用说明.txt                  GBK + CRLF（中文记事本的默认口味，不会乱码）
  截图/                         三张，按顺序编号
  备用-打不开exe时用/            零依赖版启动器 + 它依赖的 _src/

用法：
  python src/pack/build.py            # 版本号读 csproj
  python src/pack/build.py 1.7.0      # 手动指定
"""
import os
import re
import shutil
import sys
import zipfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PKG_DIR = os.path.join(ROOT, 'dist', '网盘分享版')
EXE_SRC = os.path.join(ROOT, 'dist', '无限暖暖照片清理助手.exe')
NOTE_SRC = os.path.join(ROOT, 'src', 'pack', '使用说明.txt')

SHOTS = [('main-zh.png', '1-主界面.png'),
         ('move-zh.png', '2-移动前确认.png'),
         ('web-zh.png', '3-网页版.png')]

# 零依赖版：四个中文启动器 + 它们要的脚本
LAUNCHERS = ['零依赖版-检查相册残留.bat', '零依赖版-执行移动.bat',
             '还原上次移动.bat', '清理工具记录.bat']
SCRIPTS = ['PhotoSync.ps1', 'Restore.ps1']
LANG = ['PhotoSync.en.ps1']


def read_version():
    if len(sys.argv) > 1:
        return sys.argv[1].strip()
    csproj = os.path.join(ROOT, 'src', 'app', 'NikkiPhotoCleaner.csproj')
    with open(csproj, encoding='utf-8') as f:
        m = re.search(r'<Version>([^<]+)</Version>', f.read())
    if not m:
        sys.exit('读不到 <Version>，请把版本号作为参数传进来')
    return m.group(1).strip()


def build(ver):
    name = '无限暖暖照片清理助手_v' + ver
    pkg = os.path.join(PKG_DIR, name)
    if os.path.isdir(PKG_DIR):
        shutil.rmtree(PKG_DIR)
    os.makedirs(pkg)

    # 1. exe
    if not os.path.isfile(EXE_SRC):
        sys.exit('缺少 %s，先 dotnet build 再把这个文件拷进 dist/' % EXE_SRC)
    shutil.copy2(EXE_SRC, pkg)

    # 2. 使用说明：UTF-8 模板 → GBK + CRLF
    text = open(NOTE_SRC, encoding='utf-8').read()
    text = text.replace('\r\n', '\n').replace('\n', '\r\n')
    # 模板里的版本号会跟着每次发版前移，写死替换迟早失效；
    # 统一用正则把标题行那个 vX.Y.Z 换掉，发版脚本才不用跟着改
    text = re.sub(r'v\d+\.\d+\.\d+', 'v' + ver, text, count=1)
    try:
        raw = text.encode('gbk')
    except UnicodeEncodeError as e:
        sys.exit('说明里有一个字符 GBK 编码不了：%r（中文记事本会变问号）'
                 % text[e.start:e.end])
    with open(os.path.join(pkg, '使用说明.txt'), 'wb') as f:
        f.write(raw)

    # 3. 截图
    shot_dir = os.path.join(pkg, '截图')
    os.makedirs(shot_dir)
    for src, dst in SHOTS:
        shutil.copy2(os.path.join(ROOT, 'docs', 'screenshots', src),
                     os.path.join(shot_dir, dst))

    # 4. 零依赖版
    fb = os.path.join(pkg, '备用-打不开exe时用')
    os.makedirs(os.path.join(fb, '_src', 'lang'))
    for f in LAUNCHERS:
        shutil.copy2(os.path.join(ROOT, 'package', f), fb)
    for f in SCRIPTS:
        shutil.copy2(os.path.join(ROOT, 'src', 'scripts', f), os.path.join(fb, '_src'))
    for f in LANG:
        shutil.copy2(os.path.join(ROOT, 'src', 'scripts', 'lang', f),
                     os.path.join(fb, '_src', 'lang'))

    # 5. 打包
    zpath = os.path.join(ROOT, 'dist', name + '.zip')
    if os.path.exists(zpath):
        os.remove(zpath)
    with zipfile.ZipFile(zpath, 'w', zipfile.ZIP_DEFLATED) as z:
        for base, _, files in os.walk(pkg):
            for f in sorted(files):
                full = os.path.join(base, f)
                arc = os.path.join(name, os.path.relpath(full, pkg))
                z.write(full, arc)

    return pkg, zpath


def report(pkg, zpath):
    print('文件夹：%s' % pkg)
    for base, dirs, files in os.walk(pkg):
        lv = base[len(pkg):].count(os.sep)
        print('  ' * lv + os.path.basename(base) + '/')
        for f in sorted(files):
            size = os.path.getsize(os.path.join(base, f))
            print('  ' * lv + '  %-38s %8.1f KB' % (f, size / 1024.0))
    print('压缩包：%s  (%.1f KB)' % (zpath, os.path.getsize(zpath) / 1024.0))


if __name__ == '__main__':
    report(*build(read_version()))
