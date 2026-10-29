"""Draws the Potato Keys+ icon and installer wizard images with Pillow.

Run from the repo root:  python tools/make-art.py
Writes Assets/PotatoKeysPlus.ico, Assets/icon-256.png and installer/wizard-*.bmp
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
BG = (24, 25, 30)
PANEL = (31, 33, 40)
GOLD = (244, 185, 66)
POTATO = (201, 143, 85)
POTATO_DARK = (138, 90, 46)
SPOT = (122, 78, 38)
ON_GOLD = (26, 20, 8)


def potato(size: int, with_bg: bool = True) -> Image.Image:
    """Draws the logo at 4x then downsamples for smooth edges."""
    s = size * 4
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if with_bg:
        d.rounded_rectangle((0, 0, s - 1, s - 1), radius=s * 0.22, fill=BG)

    # Potato body: rotated ellipse with a soft shadow
    body = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    bd = ImageDraw.Draw(body)
    box = (s * 0.12, s * 0.24, s * 0.84, s * 0.74)
    bd.ellipse(box, fill=POTATO, outline=POTATO_DARK, width=max(2, s // 48))
    for cx, cy, r in [(0.32, 0.47, 0.035), (0.50, 0.38, 0.03), (0.47, 0.60, 0.03), (0.66, 0.50, 0.025)]:
        bd.ellipse((s * (cx - r), s * (cy - r), s * (cx + r), s * (cy + r)), fill=SPOT)
    # little highlight
    bd.ellipse((s * 0.24, s * 0.32, s * 0.40, s * 0.40), fill=(222, 172, 118))
    body = body.rotate(18, resample=Image.BICUBIC, center=(s / 2, s / 2))

    shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse((s * 0.16, s * 0.34, s * 0.84, s * 0.82), fill=(0, 0, 0, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(s * 0.03))
    img.alpha_composite(shadow)
    img.alpha_composite(body)

    # Gold "+" badge
    r = s * 0.2
    cx, cy = s * 0.73, s * 0.73
    ring = s * 0.035
    d.ellipse((cx - r - ring, cy - r - ring, cx + r + ring, cy + r + ring), fill=BG if with_bg else (0, 0, 0, 0))
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=GOLD)
    arm, thick = r * 0.55, r * 0.22
    d.rounded_rectangle((cx - arm, cy - thick, cx + arm, cy + thick), radius=thick, fill=ON_GOLD)
    d.rounded_rectangle((cx - thick, cy - arm, cx + thick, cy + arm), radius=thick, fill=ON_GOLD)

    return img.resize((size, size), Image.LANCZOS)


def font(size: int, bold: bool = True):
    for name in (["segoeuib.ttf", "arialbd.ttf"] if bold else ["segoeui.ttf", "arial.ttf"]):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def wizard_large(scale: int) -> Image.Image:
    w, h = 164 * scale, 314 * scale
    img = Image.new("RGB", (w, h), BG)
    d = ImageDraw.Draw(img)
    # vertical gradient
    for y in range(h):
        t = y / h
        c = tuple(int(BG[i] * (1 - t) + PANEL[i] * t) for i in range(3))
        d.line((0, y, w, y), fill=c)
    # faint keycaps pattern
    key = 22 * scale
    for row in range(0, h // key + 1):
        for col in range(0, w // key + 1):
            x, y = col * key + (row % 2) * key // 2, row * key
            d.rounded_rectangle((x + 2 * scale, y + 2 * scale, x + key - 3 * scale, y + key - 3 * scale),
                                radius=4 * scale, outline=(40, 42, 50), width=scale)
    logo = potato(110 * scale, with_bg=False)
    img.paste(logo, ((w - logo.width) // 2, 70 * scale), logo)
    f = font(19 * scale)
    text = "Potato Keys"
    tw = d.textlength(text, font=f) + d.textlength("+", font=f)
    x = (w - tw) / 2
    d.text((x, 196 * scale), text, font=f, fill=(236, 237, 239))
    d.text((x + d.textlength(text, font=f), 196 * scale), "+", font=f, fill=GOLD)
    small = font(10 * scale, bold=False)
    tag = "Your keyboard, supercharged."
    d.text(((w - d.textlength(tag, font=small)) / 2, 226 * scale), tag, font=small, fill=(144, 151, 163))
    return img


def main():
    assets = ROOT / "Assets"
    installer = ROOT / "installer"
    assets.mkdir(exist_ok=True)
    installer.mkdir(exist_ok=True)

    big = potato(256)
    big.save(assets / "icon-256.png")
    big.save(assets / "PotatoKeysPlus.ico", sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])

    wizard_large(1).save(installer / "wizard-large.bmp")
    wizard_large(2).save(installer / "wizard-large-200.bmp")
    for px, name in [(55, "wizard-small.bmp"), (110, "wizard-small-200.bmp")]:
        small = Image.new("RGB", (px, px), (255, 255, 255))
        logo = potato(px)
        small.paste(logo, (0, 0), logo)
        small.save(installer / name)
    print("art written")


if __name__ == "__main__":
    main()
