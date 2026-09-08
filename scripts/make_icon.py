#!/usr/bin/env python3
"""
Generate a 256x256 Thunderstore icon for MyAwesomeWhitelist.
Design: dark card + central shield split into whitelist(green ✓) /
blacklist(red ✗), with a kick arrow accent.
Requires Pillow (pip install Pillow).
"""
from PIL import Image, ImageDraw, ImageFont
import math, os

W = H = 256
OUT = os.path.join(os.path.dirname(__file__), "..", "thunderstore", "icon.png")

# ── Palette ────────────────────────────────────────────────────────────
BG_TOP    = (20, 24, 34)
BG_BOT    = (14, 18, 26)
CARD      = (28, 33, 46)
CARD_EDGE = (50, 60, 85)
SHIELD_BG = (36, 42, 58)
SHIELD_EDGE = (65, 95, 200)
ALLOW     = (52, 200, 135)
DENY      = (235, 75, 75)
KICK      = (255, 175, 55)
TEXT      = (170, 180, 200)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


# Try to load a CJK-capable font; fall back gracefully.
def load_font(size):
    candidates = [
        "/System/Library/Fonts/STHeiti Light.ttc",
        "/System/Library/Fonts/Hiragino Sans GB.ttc",
        "/Library/Fonts/Arial Unicode.ttf",
    ]
    for path in candidates:
        try:
            return ImageFont.truetype(path, size)
        except Exception:
            continue
    try:
        return ImageFont.load_default()
    except Exception:
        return None


img = Image.new("RGB", (W, H))
draw = ImageDraw.Draw(img)

# 1) Background gradient (top → bottom, quadratic falloff)
for y in range(H):
    t = y / float(H - 1)
    c = lerp(BG_TOP, BG_BOT, t * t)
    draw.line([(0, y), (W, y)], fill=c)

# 2) Rounded card
cx, cy = W // 2, H // 2
card_w, card_h = 218, 218
card_x0 = cx - card_w // 2
card_y0 = cy - card_h // 2
r = 22
draw.rounded_rectangle(
    [card_x0, card_y0, card_x0 + card_w, card_y0 + card_h],
    radius=r, fill=CARD, outline=CARD_EDGE, width=3,
)

# 3) Shield shape — classic flat-top pointed-bottom
sx, sy = cx, cy - 6
sw, sh = 128, 148

def shield_poly(cx, cy, w, h):
    top = cy - h * 0.40
    bot = cy + h * 0.48
    hw = w * 0.5
    return [
        (cx - hw, top),
        (cx + hw, top),
        (cx + hw, cy - h * 0.06),
        (cx, bot),
        (cx - hw, cy - h * 0.06),
    ]

shield_pts = shield_poly(sx, sy, sw, sh)
draw.polygon(shield_pts, fill=SHIELD_BG, outline=SHIELD_EDGE, width=4)

# 4) Vertical split down the middle
split_x = sx
draw.line([(split_x, sy - sh * 0.38), (split_x, sy + sh * 0.44)],
         fill=CARD_EDGE, width=2)

# 5) Left half: green checkmark (whitelist)
chk_cx = sx - sw * 0.25
chk_cy = sy + 2
cs = 30
draw.line(
    [(chk_cx - cs * 0.52, chk_cy + cs * 0.10),
     (chk_cx - cs * 0.12, chk_cy + cs * 0.48)],
    fill=ALLOW, width=8, joint="curve")
draw.line(
    [(chk_cx - cs * 0.12, chk_cy + cs * 0.48),
     (chk_cx + cs * 0.56, chk_cy - cs * 0.38)],
    fill=ALLOW, width=8, joint="curve")

# 6) Right half: red X (blacklist)
xc = sx + sw * 0.25
yc = sy + 2
xs = 28
off = xs * 0.38
draw.line([(xc - off, yc - off), (xc + off, yc + off)], fill=DENY, width=8)
draw.line([(xc + off, yc - off), (xc - off, yc + off)], fill=DENY, width=8)

# 7) Kick arrow at lower-right of shield (amber curved eject arrow)
kcx = sx + sw * 0.36
kcy = sy + sh * 0.28
kr = 16
arrow_pts = []
for deg in range(-35, 55, 7):
    rad = math.radians(deg)
    ax = kcx + kr * math.cos(rad)
    ay = kcy + kr * math.sin(rad) * 0.65
    arrow_pts.append((ax, ay))
for i in range(len(arrow_pts) - 1):
    draw.line([arrow_pts[i], arrow_pts[i+1]], fill=KICK, width=3)
hx, hy = arrow_pts[-1]
draw.polygon([(hx + 7, hy - 5), (hx + 1, hy + 1), (hx + 7, hy + 7)], fill=KICK)

# 8) Bottom label "AWL"
font_big = load_font(24)
label = "AWL"
bbox = draw.textbbox((0, 0), label, font=font_big)
tw = bbox[2] - bbox[0]
th = bbox[3] - bbox[1]
draw.text((cx - tw // 2, card_y0 + card_h - th - 16), label, fill=TEXT, font=font_big)

# 9) Version badge top-right of card
badge_r = 13
bcx = card_x0 + card_w - badge_r - 8
bcy = card_y0 + badge_r + 8
draw.ellipse([bcx - badge_r, bcy - badge_r, bcx + badge_r, bcy + badge_r],
             fill=(45, 110, 210))
font_sm = load_font(11)
vt = "v1"
vbb = draw.textbbox((0, 0), vt, font=font_sm)
vbw = vbb[2] - vbb[0]
draw.text((bcx - vbw // 2, bcy - 5), vt, fill=(255, 255, 255), font=font_sm)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT, "PNG")
print(f"wrote {os.path.abspath(OUT)} ({os.path.getsize(OUT)} bytes)")
