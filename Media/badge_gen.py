#!/usr/bin/env python3
"""LQ badge + Workshop preview. Same geometry system as the CE+SS suite badges
(300x100 bar/circle/ring-knockout; 512 preview) — visually consistent, distinct
identity: masterwork-teal accent (quality), emblem = CE's rifle glyph (this is a CE
mod; glyph remixed from CE's Badge_CE_compatible.svg, CC BY-NC-SA, CE team)
crowned with a quality star. Run from Media/: python3 badge_gen.py"""
import collections
import io
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = "/usr/share/fonts/dejavu-sans-fonts/DejaVuSansCondensed-Bold.ttf"
S = 4
BLACK = (0, 0, 0, 255)
WHITE = (255, 255, 255, 255)
GOLD = (0, 168, 156, 255)  # masterwork teal — amber/gold collided with the Loadouts Module badge


def extract_rifle():
    import cairosvg
    buf = io.BytesIO()
    cairosvg.svg2png(url=os.path.join(HERE, "Badge_CE_compatible.svg"), write_to=buf, scale=8)
    buf.seek(0)
    src = Image.open(buf).convert("RGBA")
    Z = 8
    px = src.load()
    pts = [(x, y) for x in range(105 * Z) for y in range(100 * Z)
           if px[x, y][0] > 200 and px[x, y][3] > 200]
    ptset = set(pts)
    seen = set()
    clusters = []
    for p in pts:
        if p in seen:
            continue
        q = collections.deque([p])
        comp = []
        seen.add(p)
        while q:
            c = q.popleft()
            comp.append(c)
            x, y = c
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    n = (x + dx, y + dy)
                    if n in ptset and n not in seen:
                        seen.add(n)
                        q.append(n)
        clusters.append(comp)

    def is_rifle(comp):
        xs = [p[0] for p in comp]
        ys = [p[1] for p in comp]
        return (min(xs) + max(xs)) / 2 < 50 * Z and (min(ys) + max(ys)) / 2 > 40 * Z

    comp = max((c for c in clusters if is_rifle(c)), key=len)
    xs = [p[0] for p in comp]
    ys = [p[1] for p in comp]
    x0, y0 = min(xs), min(ys)
    m = Image.new("L", (max(xs) - x0 + 1, max(ys) - y0 + 1), 0)
    mp = m.load()
    for x, y in comp:
        mp[x - x0, y - y0] = 255
    m = m.rotate(45, expand=True, resample=Image.BICUBIC)
    m = m.transpose(Image.FLIP_LEFT_RIGHT).point(lambda v: 255 if v > 110 else 0)
    return m.crop(m.getbbox())


def star(d, cx, cy, r, fill):
    import math
    pts = []
    for i in range(10):
        ang = -math.pi / 2 + i * math.pi / 5
        rad = r if i % 2 == 0 else r * 0.42
        pts.append((cx + rad * math.cos(ang), cy + rad * math.sin(ang)))
    d.polygon(pts, fill=fill)


def emblem(img, d, cx_units, rifle, scale):
    """Rifle with a quality star above, centered on circle center."""
    def paste_glyph(m, gx, gy, target_w):
        sc = target_w / m.width
        g = m.resize((int(m.width * sc), int(m.height * sc)), Image.LANCZOS)
        img.paste(WHITE, (int(gx - g.width / 2), int(gy - g.height / 2)), g)
    paste_glyph(rifle, cx_units[0], cx_units[1] + 8 * scale, 62 * scale)
    star(d, cx_units[0], cx_units[1] - 26 * scale, 14 * scale, GOLD)


def render_badge(rifle):
    W, H = 300 * S, 100 * S
    bar = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    db = ImageDraw.Draw(bar)
    db.rectangle([0, 25 * S, 300 * S, 74 * S], fill=BLACK)
    hole = Image.new("L", (W, H), 0)
    dh = ImageDraw.Draw(hole)
    cx, cy, r, gap = 50 * S, 50 * S, 50 * S, 5 * S
    dh.ellipse([cx - (r + gap), cy - (r + gap), cx + (r + gap), cy + (r + gap)], fill=255)
    dh.rectangle([0, 0, 5 * S, H], fill=255)
    bar.putalpha(Image.composite(Image.new("L", (W, H), 0), bar.getchannel("A"), hole))

    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    img.alpha_composite(bar)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLACK)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=GOLD, width=3 * S)
    emblem(img, d, (cx, cy), rifle, S)

    f1 = ImageFont.truetype(FONT, 15 * S)
    f2 = ImageFont.truetype(FONT, 10 * S)
    CX = 202 * S
    t1 = "LOADOUT QUALITY"
    w1 = d.textlength(t1, font=f1)
    d.text((CX - w1 / 2, 32 * S), t1, font=f1, fill=WHITE)
    t2 = "for COMBAT EXTENDED"
    K = 1.6 * S
    w2 = sum(d.textlength(c, font=f2) + K for c in t2) - K
    x = CX - w2 / 2
    for ch in t2:
        d.text((x, 55 * S), ch, font=f2, fill=GOLD)
        x += d.textlength(ch, font=f2) + K
    img.resize((300, 100), Image.LANCZOS).save(os.path.join(HERE, "Badge_LQ.png"))
    print("wrote Badge_LQ.png")


def render_preview(rifle):
    P = 4
    W = H = 512 * P
    img = Image.new("RGBA", (W, H), (12, 12, 12, 255))
    d = ImageDraw.Draw(img)
    cx, cy, r = 256 * P, 190 * P, 140 * P
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLACK, outline=GOLD, width=8 * P)

    def paste_glyph(m, gx, gy, target_w):
        sc = target_w / m.width
        g = m.resize((int(m.width * sc), int(m.height * sc)), Image.LANCZOS)
        img.paste(WHITE, (int(gx - g.width / 2), int(gy - g.height / 2)), g)

    paste_glyph(rifle, cx, (190 + 25) * P, 176 * P)
    star(d, cx, (190 - 75) * P, 40 * P, GOLD)

    f1 = ImageFont.truetype(FONT, 36 * P)
    f2 = ImageFont.truetype(FONT, 30 * P)
    f3 = ImageFont.truetype(FONT, 20 * P)
    for text, font, y, color in [
        ("LOADOUT QUALITY", f1, 362 * P, WHITE),
        ("for COMBAT EXTENDED", f2, 406 * P, WHITE),
        ("YOUR PAWNS DESERVE BETTER GUNS", f3, 456 * P, GOLD),
    ]:
        w = d.textlength(text, font=font)
        d.text(((W - w) / 2, y), text, font=font, fill=color)
    img.resize((512, 512), Image.LANCZOS).save(os.path.join(HERE, "..", "About", "Preview.png"))
    print("wrote About/Preview.png")


if __name__ == "__main__":
    rifle = extract_rifle()
    render_badge(rifle)
    render_preview(rifle)
