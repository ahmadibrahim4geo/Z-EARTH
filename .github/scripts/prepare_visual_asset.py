#!/usr/bin/env python3
"""Prepare reviewed local raster assets. No network, generation or license verification."""
import argparse
import hashlib
import io
import json
import re
import shutil
import sys
import warnings
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlsplit

ROLES = {"thumbnail", "photo", "logo", "screenshot", "illustration",
         "scientific-preview", "cover", "background"}
REPRESENTATIONS = {"actual-preview", "illustrative", "brand-mark",
                   "photograph", "screenshot", "generated"}
MAX_BYTES = 20 * 1024 * 1024
MAX_PIXELS = 30_000_000


def metadata_errors(data):
    """Check declarations only; the host must actually inspect source and terms."""
    if not isinstance(data, dict):
        return ["Metadata must be an object"]
    errors = []
    for key in ("asset_id", "creator", "license", "attribution", "alt", "intended_use"):
        if not isinstance(data.get(key), str) or not data[key].strip():
            errors.append(key + " must be a nonempty string")
    if not isinstance(data.get("asset_id"), str) or not re.fullmatch(
            r"[a-z0-9]+(?:-[a-z0-9]+)*", data.get("asset_id", "")):
        errors.append("asset_id must be lowercase kebab-case")
    for key in ("source_page", "license_url"):
        try:
            value = data.get(key)
            parsed = urlsplit(value) if isinstance(value, str) else None
            if (not parsed or parsed.scheme != "https" or not parsed.hostname
                    or parsed.username or parsed.password):
                raise ValueError()
        except ValueError:
            errors.append(key + " must be an HTTPS URL without credentials")
    for key, choices in (("role", ROLES), ("representation", REPRESENTATIONS)):
        if not isinstance(data.get(key), str) or data[key] not in choices:
            errors.append(key + " is invalid")
    for key in ("source_verified", "license_verified"):
        if data.get(key) is not True:
            errors.append(key + " must record a completed host review")
    if isinstance(data.get("license"), str) and data["license"].strip().casefold() in {
            "unknown", "unverified", "pending", "tbd"}:
        errors.append("license must identify reviewed terms")
    if data.get("role") == "logo" and data.get("representation") != "brand-mark":
        errors.append("A logo requires representation brand-mark")
    if data.get("role") == "scientific-preview":
        if data.get("representation") != "actual-preview":
            errors.append("A scientific preview must be an actual-preview")
        if not isinstance(data.get("data_reference"), str) or not data["data_reference"].strip():
            errors.append("A scientific preview requires data_reference")
    if data.get("representation") == "generated":
        for key in ("generator", "generation_reference"):
            if not isinstance(data.get(key), str) or not data[key].strip():
                errors.append("Generated assets require " + key)
    return errors


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate metadata key: " + key)
        result[key] = value
    return result


def read_metadata(path):
    path = Path(path)
    if not path.is_file() or path.stat().st_size > 65536:
        raise ValueError("Metadata file missing or larger than 64 KiB")
    return json.loads(path.read_text("utf-8-sig"), object_pairs_hook=unique_pairs,
                      parse_constant=lambda x: (_ for _ in ()).throw(ValueError("Non-finite metadata")))


def prepare(source, metadata, output_dir, width=64, height=64, scales=(1, 2), fit="contain"):
    """Create a new directory; never overwrite a source or pre-existing output."""
    errors = metadata_errors(metadata)
    if errors:
        raise ValueError("; ".join(errors))
    if (type(width) is not int or type(height) is not int or
            not 1 <= width <= 4096 or not 1 <= height <= 4096):
        raise ValueError("Dimensions must be integers in 1..4096")
    if (not isinstance(scales, (list, tuple)) or not scales
            or any(type(x) is not int or x not in (1, 2, 3, 4) for x in scales)
            or len(set(scales)) != len(scales)):
        raise ValueError("Scales must be distinct integers in 1..4")
    if max(scales) * max(width, height) > 4096:
        raise ValueError("Variant dimensions must not exceed 4096")
    if fit not in ("contain", "cover"):
        raise ValueError("fit must be contain or cover")
    if metadata["role"] in {"logo", "scientific-preview"} and fit != "contain":
        raise ValueError("Logos and scientific previews must use contain")
    source, output_dir = Path(source), Path(output_dir)
    if output_dir.exists() or output_dir.is_symlink():
        raise ValueError("Destination already exists; choose a new directory")
    if not source.is_file() or source.stat().st_size > MAX_BYTES:
        raise ValueError("Source missing or larger than 20 MiB")
    try:
        from PIL import Image, ImageOps
    except ImportError as exc:
        raise ValueError("Pillow is required in the active Python environment; source was not changed") from exc
    with source.open("rb") as stream:
        raw = stream.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES:
        raise ValueError("Source larger than 20 MiB")
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("error", Image.DecompressionBombWarning)
            with Image.open(io.BytesIO(raw)) as original:
                if original.format not in {"PNG", "JPEG", "WEBP"}:
                    raise ValueError("Only PNG/JPEG/WebP are supported; use a reviewed vector/animation pipeline")
                if getattr(original, "n_frames", 1) != 1:
                    raise ValueError("Animated input is not supported")
                if original.width * original.height > MAX_PIXELS:
                    raise ValueError("Source exceeds 30 million pixels")
                icc = original.info.get("icc_profile")
                original.load()
                raster = ImageOps.exif_transpose(original).convert("RGBA")
    except (Image.DecompressionBombError, Image.DecompressionBombWarning) as exc:
        raise ValueError("Source exceeds Pillow's safe decode limit") from exc
    if fit == "cover" and any(
            width * scale > raster.width or height * scale > raster.height for scale in scales):
        raise ValueError("Cover would upscale; choose a larger original or smaller dimensions")
    # Validate/decode fully before making any output directory.
    output_dir.mkdir(parents=True, exist_ok=False)
    try:
        variants = []
        for scale in scales:
            target = (width * scale, height * scale)
            if fit == "cover":
                result = ImageOps.fit(raster, target, method=Image.Resampling.LANCZOS)
            else:
                ratio = min(1.0, target[0] / raster.width, target[1] / raster.height)
                dims = (max(1, round(raster.width * ratio)), max(1, round(raster.height * ratio)))
                resized = raster.resize(dims, Image.Resampling.LANCZOS)
                result = Image.new("RGBA", target, (0, 0, 0, 0))
                result.alpha_composite(resized, ((target[0] - dims[0]) // 2,
                                                 (target[1] - dims[1]) // 2))
            name = "{}-{}x{}.png".format(metadata["asset_id"], *target)
            destination = output_dir / name
            kwargs = {"icc_profile": icc} if icc else {}
            result.save(destination, format="PNG", optimize=True, **kwargs)
            with Image.open(destination) as check:
                check.load()
                if check.size != target or check.mode != "RGBA":
                    raise ValueError("Variant readback failed")
            payload = destination.read_bytes()
            variants.append({"path": name, "width": target[0], "height": target[1],
                             "scale": scale, "bytes": len(payload),
                             "sha256": hashlib.sha256(payload).hexdigest()})
        record = dict(metadata)
        record.update({
            "schema_version": "1.0",
            "prepared_at": datetime.now(timezone.utc).isoformat(),
            "original_filename": source.name,
            "original_sha256": hashlib.sha256(raw).hexdigest(),
            "source_dimensions": {"width": raster.width, "height": raster.height},
            "fit": fit, "variants": variants,
            "verification_scope": "Local raster decode/readback only; source review, licensing and UI loading are host responsibilities",
        })
        (output_dir / "asset.json").write_text(
            json.dumps(record, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8")
        return record
    except Exception:
        # This directory was created exclusively by this invocation.
        shutil.rmtree(output_dir)
        raise


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--width", type=int, default=64)
    parser.add_argument("--height", type=int, default=64)
    parser.add_argument("--scales", nargs="+", type=int, default=[1, 2])
    parser.add_argument("--fit", choices=("contain", "cover"), default="contain")
    args = parser.parse_args(argv)
    try:
        record = prepare(args.input, read_metadata(args.metadata), args.output_dir,
                         args.width, args.height, args.scales, args.fit)
    except (OSError, ValueError, SyntaxError) as exc:
        print("ERROR: " + str(exc), file=sys.stderr)
        return 1
    print("Prepared {} local variants and asset.json in {}".format(
        len(record["variants"]), args.output_dir))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

