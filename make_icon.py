# -*- coding: utf-8 -*-
"""Draws the plugin icon in the same style as the other plugins in this folder.

512x512, transparent outside a rounded square, diagonal purple-to-blue gradient,
flat white shapes with a translucent tint for interior detail. The palette is
sampled from the existing icons: purple (157, 98, 197) to blue (13, 159, 218),
which meet at (85, 128, 208) on the diagonal.

Subject: a media timeline whose segments are marked, and a skip glyph on the
badge - what the plugin does, in the one shape that reads at 64 pixels.
"""
from PIL import Image, ImageDraw

S = 512
SS = 4                      # supersample factor, downscaled at the end for clean edges
W = S * SS

PURPLE = (157, 98, 197)
BLUE = (13, 159, 218)
RADIUS = int(110 * SS)


def gradient():
    """Diagonal purple (top left) to blue (bottom right)."""
    img = Image.new('RGB', (S, S))
    px = img.load()
    for y in range(S):
        for x in range(S):
            t = (x + y) / (2 * (S - 1))
            px[x, y] = (
                round(PURPLE[0] + (BLUE[0] - PURPLE[0]) * t),
                round(PURPLE[1] + (BLUE[1] - PURPLE[1]) * t),
                round(PURPLE[2] + (BLUE[2] - PURPLE[2]) * t),
            )
    return img.resize((W, W), Image.LANCZOS)


def main():
    base = gradient().convert('RGBA')

    # Rounded-square mask, so the corners come out transparent like the others.
    mask = Image.new('L', (W, W), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, W - 1], RADIUS, fill=255)

    icon = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    icon.paste(base, (0, 0), mask)

    layer = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    white = (255, 255, 255, 255)

    # --- skip-forward glyph, above ---------------------------------------
    # Two triangles and a bar. Stacked above the timeline rather than set in a
    # badge over it: both shapes are white, and an overlapping badge merges with
    # the bar into one blob instead of reading as two things.
    cx, cy = 256 * SS, 170 * SS
    t_w, t_h = 62 * SS, 68 * SS
    for dx in (-96 * SS, -26 * SS):
        d.polygon(
            [(cx + dx, cy - t_h), (cx + dx, cy + t_h), (cx + dx + t_w, cy)],
            fill=white)
    d.rounded_rectangle(
        [cx + 48 * SS, cy - t_h, cx + 74 * SS, cy + t_h], 11 * SS, fill=white)

    # --- the timeline bar, below -----------------------------------------
    d.rounded_rectangle(
        [58 * SS, 300 * SS, 454 * SS, 396 * SS], 48 * SS, fill=white)

    # Marked segments, tinted rather than solid so the bar still reads as one
    # shape with parts picked out, the way the other icons treat interior detail.
    tint = (70, 120, 205, 235)
    for x0, x1 in ((94, 172), (212, 268), (322, 418)):
        d.rounded_rectangle(
            [x0 * SS, 322 * SS, x1 * SS, 374 * SS], 17 * SS, fill=tint)

    icon = Image.alpha_composite(icon, layer)
    icon = icon.resize((S, S), Image.LANCZOS)

    out = 'images/icon.png'
    icon.save(out)
    print('wrote %s (%dx%d)' % (out, icon.size[0], icon.size[1]))
    print('corner alpha:', icon.getpixel((2, 2))[3])
    print('TL/BR:', icon.getpixel((40, 40))[:3], icon.getpixel((S - 40, S - 40))[:3])


if __name__ == '__main__':
    main()
