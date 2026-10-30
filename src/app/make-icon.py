# -*- coding: utf-8 -*-
"""
app.ico / favicon.png / docs/icon.png 生成脚本

用法（需要 Pillow）：
    python make-icon.py "源图.png"

源图要求：正方形、边长 >= 256 px 的插画原图（仓库里不放原图，只放生成结果）。
输出：
    src/app/app.ico      16/24/32/48 用 32bpp DIB（小尺寸最锐利）
                         64/128/256 用调色板 PNG（同等画质，体积约为直存的 1/5）
    web/favicon.png      64 px 圆角图标
    docs/icon.png        256 px 圆角预览图

取景：16–48 px 只裁猫头，64 px 以上用整幅插画 ——
完整插画缩到任务栏尺寸会糊成一团，只留头部轮廓才认得出。
"""
import io
import os
import struct
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))

# 猫头取景框（相对原图宽高的比例），与整幅 0~1 对应
HEAD_BOX = (0.360, 0.132, 0.807, 0.578)
RADIUS_PCT = 0.19          # 圆角半径占边长的比例
SMALL_SIZES = (16, 24, 32, 48)   # 用猫头 + DIB
BIG_SIZES = (64, 128, 256)       # 用整幅 + PNG


def rounded(img, radius_pct=RADIUS_PCT, colors=250):
    """圆角 + 调色板化，返回 (P 图, 透明色索引)。

    PNG 调色板只能给「整档」透明度，圆角外那圈用全透明色索引，
    省下来的体积比直存 RGBA 少 70% 以上，256 px 下肉眼几乎看不出。
    """
    size = img.size[0]
    radius = int(size * radius_pct)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, size - 1, size - 1], radius=radius, fill=255)

    p = img.convert("RGB").quantize(
        colors=colors, method=Image.FASTOCTREE, dither=Image.FLOYDSTEINBERG)
    pal = p.getpalette()
    trans = len(pal) // 3
    pal += [0, 0, 0]
    px, mk = p.load(), mask.load()
    for y in range(size):
        for x in range(size):
            if mk[x, y] < 128:
                px[x, y] = trans
    p.putpalette(pal)
    return p, trans


def dib_entry(img):
    """32bpp DIB 条目：BITMAPINFOHEADER + XOR 位图 + AND 掩码（小尺寸用）。"""
    size = img.size[0]
    px = img.convert("RGBA").load()

    xor = bytearray()
    for y in range(size - 1, -1, -1):
        for x in range(size):
            r, g, b, a = px[x, y]
            xor += bytes((b, g, r, a))

    stride = ((size + 31) // 32) * 4
    andm = bytearray()
    for y in range(size - 1, -1, -1):
        row = bytearray(stride)
        for x in range(size):
            if px[x, y][3] < 128:
                row[x // 8] |= 0x80 >> (x % 8)
        andm += row

    bih = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0,
                      len(xor) + len(andm), 0, 0, 0, 0)
    return bytes(bih + xor + andm)


def png_entry(img):
    p, trans = rounded(img)
    buf = io.BytesIO()
    p.save(buf, "PNG", transparency=trans, compress_level=9)
    return buf.getvalue()


def build_ico(src):
    entries = []
    w = src.size[0]
    head = src.crop((int(w * HEAD_BOX[0]), int(w * HEAD_BOX[1]),
                     int(w * HEAD_BOX[2]), int(w * HEAD_BOX[3])))
    for size in SMALL_SIZES:
        entries.append((size, dib_entry(
            head.resize((size, size), Image.LANCZOS).convert("RGBA"))))
    for size in BIG_SIZES:
        entries.append((size, png_entry(
            src.resize((size, size), Image.LANCZOS).convert("RGBA"))))

    out = bytearray(struct.pack("<HHH", 0, 1, len(entries)))
    offset = 6 + 16 * len(entries)
    for size, data in entries:
        out += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0,
                           1, 32, len(data), offset)
        offset += len(data)
    for _, data in entries:
        out += data
    return bytes(out), entries


def main():
    src_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        HERE, "..", "..", "docs", "icon.png")
    src = Image.open(src_path).convert("RGB")
    if src.size[0] != src.size[1]:
        raise SystemExit("源图必须是正方形")
    if src.size[0] < 256:
        raise SystemExit("源图边长需 >= 256 px，否则 128/256 图标是放大的，会发虚")

    ico, entries = build_ico(src)
    ico_path = os.path.join(HERE, "app.ico")
    with open(ico_path, "wb") as f:
        f.write(ico)

    for name, size in (("web/favicon.png", 64), ("docs/icon.png", 256)):
        p, trans = rounded(src.resize((size, size), Image.LANCZOS).convert("RGBA"))
        path = os.path.join(ROOT, name)
        p.save(path, "PNG", transparency=trans, compress_level=9)

    print("app.ico %d bytes" % len(ico))
    for size, data in entries:
        print("  %3d px  %6d bytes" % (size, len(data)))


if __name__ == "__main__":
    main()
