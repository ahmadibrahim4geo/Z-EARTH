"""Rebuild Z EARTH brand assets from the approved master reference.

The reference artwork is authoritative: the cyan orbit intentionally crosses
in front of the white Z. This script copies those pixels exactly instead of
redrawing or re-layering the mark, then regenerates the Windows multi-size ICO.
"""

from __future__ import annotations

import argparse

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "ZEarth.App" / "Assets"
APPROVED_ICON_CROP = (86, 290, 425, 620)
LOGO_ICON_POSITION = (47, 29)
LOGO_ICON_SIZE = (337, 330)


def rebuild(reference_path: Path) -> None:
    icon_path = ASSETS / "zearth_approved_icon.png"
    logo_path = ASSETS / "zearth_approved_logo_dark_theme.png"
    ico_path = ASSETS / "zearth_icon.ico"

    reference = Image.open(reference_path).convert("RGB")
    if reference.width < APPROVED_ICON_CROP[2] or reference.height < APPROVED_ICON_CROP[3]:
        raise ValueError(
            f"Approved reference is too small: {reference.size}; expected at least "
            f"{APPROVED_ICON_CROP[2]}x{APPROVED_ICON_CROP[3]}."
        )

    current_icon = Image.open(icon_path).convert("RGBA")
    approved_rgb = reference.crop(APPROVED_ICON_CROP)
    if approved_rgb.size != current_icon.size:
        approved_rgb = approved_rgb.resize(current_icon.size, Image.Resampling.LANCZOS)

    # Reuse only the established rounded-square transparency silhouette. Every
    # visible RGB pixel—including the exact Z/orbit overlap—comes from the master.
    approved_icon = approved_rgb.convert("RGBA")
    approved_icon.putalpha(current_icon.getchannel("A"))
    approved_icon.save(icon_path, optimize=True)

    # Keep the dark-theme lockup's light wordmark and replace just its icon.
    logo = Image.open(logo_path).convert("RGBA")
    clear_right = LOGO_ICON_POSITION[0] + LOGO_ICON_SIZE[0]
    logo.paste((0, 0, 0, 0), (0, 0, clear_right, logo.height))
    logo_icon = approved_icon.resize(LOGO_ICON_SIZE, Image.Resampling.LANCZOS)
    logo.alpha_composite(logo_icon, LOGO_ICON_POSITION)
    logo.save(logo_path, optimize=True)

    square_size = max(approved_icon.size)
    square = Image.new("RGBA", (square_size, square_size), (0, 0, 0, 0))
    square.alpha_composite(
        approved_icon,
        ((square_size - approved_icon.width) // 2, (square_size - approved_icon.height) // 2),
    )
    square.save(
        ico_path,
        format="ICO",
        sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
    )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path, help="Path to the approved Z EARTH reference PNG")
    args = parser.parse_args()
    rebuild(args.reference.resolve())


if __name__ == "__main__":
    main()
