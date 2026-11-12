"""Regenerates the StoreCrate icon assets from the vector geometry in assets/icon.svg.

Renders every shape at a high supersampling factor and downsamples with Lanczos, which
gives smooth edges instead of the hard, pixel-dense output of the previous run.

Outputs:
  assets/icon.png                                1024x1024 master
  assets/icon-128.png                            128x128 README image
  src/StoreCrate.App/Assets/StoreCrate-256.png   256x256 window icon
  src/StoreCrate.App/Assets/StoreCrate-64.png    64x64 window icon (small UI)
  src/StoreCrate.App/Assets/StoreCrate.ico       multi-size exe icon

Usage: python tools/generate-icons.py
"""

from __future__ import annotations

import math
import struct
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]

SIZE_MASTER = 1024
RADIUS_MASTER = 224
GRADIENT_START = (79, 70, 229)  # #4F46E5
GRADIENT_END = (6, 182, 212)  # #06B6D4

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def gradient_rgb(size: int) -> Image.Image:
    """Diagonal linear gradient from the top-left to the bottom-right corner."""
    import numpy as np

    ys, xs = np.mgrid[0:size, 0:size]
    t = np.clip((xs + ys) / (2.0 * (size - 1)), 0.0, 1.0)[..., None]
    start = np.array(GRADIENT_START, dtype=float)
    end = np.array(GRADIENT_END, dtype=float)
    rgb = start * (1.0 - t) + end * t
    return Image.fromarray(rgb.astype("uint8"), mode="RGB").convert("RGBA")


def rounded_rect_alpha(size: int, radius: int, ss: int) -> Image.Image:
    """Anti-aliased rounded-square alpha mask for the icon background."""
    big = Image.new("L", (size * ss, size * ss), 0)
    draw = ImageDraw.Draw(big)
    draw.rounded_rectangle(
        [0, 0, size * ss - 1, size * ss - 1],
        radius=radius * ss,
        fill=255,
    )
    return big.resize((size, size), Image.Resampling.LANCZOS)


def _arc(center: tuple[float, float], radius: float, a0: float, a1: float, steps: int = 24):
    cx, cy = center
    return [
        (cx + radius * math.cos(math.radians(a)), cy + radius * math.sin(math.radians(a)))
        for a in [a0 + (a1 - a0) * i / steps for i in range(steps + 1)]
    ]


def crate_points() -> list[tuple[float, float]]:
    """The rounded crate outline from icon.svg, as a polyline in 1024-space."""
    points = [(248.0, 430.0), (248.0, 748.0)]
    points += _arc((300.0, 748.0), 52.0, 180.0, 90.0)
    points += [(724.0, 800.0)]
    points += _arc((724.0, 748.0), 52.0, 90.0, 0.0)
    points += [(776.0, 748.0), (776.0, 430.0)]
    return points


def render(size: int, *, slats: bool = True, boost: float = 1.0, ss: int = 8) -> Image.Image:
    """Renders the icon at `size` pixels with `ss`-fold supersampling."""
    dim = size * ss
    k = dim / SIZE_MASTER

    shapes = Image.new("RGBA", (dim, dim), (0, 0, 0, 0))
    draw = ImageDraw.Draw(shapes)

    def stroke(points, width: int, alpha: int = 255) -> None:
        w = max(1, round(width * k * boost))
        fill = (255, 255, 255, alpha)
        scaled = [(x * k, y * k) for x, y in points]
        for i in range(len(scaled) - 1):
            draw.line([scaled[i], scaled[i + 1]], fill=fill, width=w)
        r = w / 2.0
        for x, y in scaled:
            draw.ellipse([x - r, y - r, x + r, y + r], fill=fill)

    # Crate body + top flanges
    stroke(crate_points(), 56)
    stroke([(248.0, 430.0), (404.0, 430.0)], 56)
    stroke([(620.0, 430.0), (776.0, 430.0)], 56)

    # Slats (dropped on small sizes where they turn to mud)
    if slats:
        stroke([(260.0, 566.0), (764.0, 566.0)], 40, alpha=178)
        stroke([(260.0, 676.0), (764.0, 676.0)], 40, alpha=178)

    # Download arrow
    stroke([(512.0, 188.0), (512.0, 392.0)], 64)
    head = [(414.0 * k, 368.0 * k), (610.0 * k, 368.0 * k), (512.0 * k, 500.0 * k)]
    draw.polygon(head, fill=(255, 255, 255, 255))

    shapes = shapes.resize((size, size), Image.Resampling.LANCZOS)

    base = gradient_rgb(size)
    base.putalpha(rounded_rect_alpha(size, RADIUS_MASTER, ss))
    return Image.alpha_composite(base, shapes)


def write_ico(path: Path, images: dict[int, Image.Image]) -> None:
    """Writes a Windows .ico with one PNG-compressed entry per size."""
    entries = []
    for s in ICO_SIZES:
        blob = images[s]
        buf = __import__("io").BytesIO()
        blob.save(buf, format="PNG", optimize=True)
        entries.append((s, buf.getvalue()))

    with open(path, "wb") as fh:
        fh.write(struct.pack("<HHH", 0, 1, len(entries)))
        offset = 6 + 16 * len(entries)
        for s, blob in entries:
            dim = 0 if s >= 256 else s
            fh.write(
                struct.pack(
                    "<BBBBHHII",
                    dim,
                    dim,
                    0,  # colors
                    0,  # reserved
                    1,  # planes
                    32,  # bits per pixel
                    len(blob),
                    offset,
                )
            )
            offset += len(blob)
        for _, blob in entries:
            fh.write(blob)


def main() -> None:
    icon = render(SIZE_MASTER, ss=4)
    icon.save(ROOT / "assets" / "icon.png", optimize=True)
    icon.resize((128, 128), Image.Resampling.LANCZOS).save(
        ROOT / "assets" / "icon-128.png", optimize=True
    )

    render(256, ss=4).save(ROOT / "src" / "StoreCrate.App" / "Assets" / "StoreCrate-256.png", optimize=True)
    render(64, ss=8).save(ROOT / "src" / "StoreCrate.App" / "Assets" / "StoreCrate-64.png", optimize=True)

    ico_images: dict[int, Image.Image] = {}
    for s in ICO_SIZES:
        if s <= 24:
            # Tiny sizes: drop the slats and thicken strokes so the glyph stays legible.
            img = render(s, slats=False, boost=1.3, ss=8)
        elif s <= 48:
            img = render(s, slats=False, ss=8)
        else:
            img = render(s, ss=8)
        ico_images[s] = img

    write_ico(ROOT / "src" / "StoreCrate.App" / "Assets" / "StoreCrate.ico", ico_images)
    print("icons regenerated")


if __name__ == "__main__":
    main()
