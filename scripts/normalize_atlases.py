#!/usr/bin/env python3
"""Deterministically fit UmaCodexPet atlases to the ChatGPT pet cell volume.

This tool never generates or redraws artwork. It applies one common crop and
one common scale per character to pixels already rendered by UmaViewer, so
motion offsets, proportions, transparency, and frame-to-frame alignment remain
intact.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import statistics
from pathlib import Path
from typing import Dict, Iterable, List, Sequence, Tuple

from PIL import Image


CELL_WIDTH = 192
CELL_HEIGHT = 208
COLUMNS = 8
ROWS = 9
ATLAS_SIZE = (CELL_WIDTH * COLUMNS, CELL_HEIGHT * ROWS)
STATE_NAMES = (
    "idle",
    "run_right",
    "run_left",
    "wave",
    "jump",
    "failure",
    "waiting",
    "working",
    "review",
)
STATE_SPRITE_INDICES = (
    (0, 1, 2, 3, 4, 5),
    (8, 9, 10, 11, 12, 13, 14, 15),
    (16, 17, 18, 19, 20, 21, 22, 23),
    (24, 25, 26, 27),
    (32, 33, 34, 35, 36),
    (40, 41, 42, 43, 44, 45, 46, 47),
    (48, 49, 50, 51, 52, 53),
    (56, 57, 58, 59, 60, 61),
    (64, 65, 66, 67, 68, 69),
)
FRAME_COUNTS = tuple(len(indices) for indices in STATE_SPRITE_INDICES)

# The largest pose can use this common region. The five-pixel top/side inset
# and seven-pixel bottom inset keep resampling and every cell isolated.
TARGET_MAX_WIDTH = 182
TARGET_MAX_HEIGHT = 196
TARGET_CENTER_X = 96
TARGET_BOTTOM = 201  # Exclusive bottom coordinate; visible pixels end at 200.
MIN_EDGE_MARGIN = 4

Box = Tuple[int, int, int, int]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def cell_box(sprite_index: int) -> Box:
    row, column = divmod(sprite_index, COLUMNS)
    left = column * CELL_WIDTH
    top = row * CELL_HEIGHT
    return left, top, left + CELL_WIDTH, top + CELL_HEIGHT


def required_cells() -> Iterable[Tuple[int, int, int]]:
    for state_index, sprite_indices in enumerate(STATE_SPRITE_INDICES):
        for frame_index, sprite_index in enumerate(sprite_indices):
            yield state_index, frame_index, sprite_index


def unused_cells() -> Iterable[int]:
    assigned = {sprite_index for indices in STATE_SPRITE_INDICES for sprite_index in indices}
    yield from (index for index in range(COLUMNS * ROWS) if index not in assigned)


def alpha_box(image: Image.Image) -> Box | None:
    return image.getchannel("A").getbbox()


def union_boxes(boxes: Sequence[Box]) -> Box:
    return (
        min(box[0] for box in boxes),
        min(box[1] for box in boxes),
        max(box[2] for box in boxes),
        max(box[3] for box in boxes),
    )


def box_dict(box: Box) -> Dict[str, int]:
    return {
        "left": box[0],
        "top": box[1],
        "right_exclusive": box[2],
        "bottom_exclusive": box[3],
        "width": box[2] - box[0],
        "height": box[3] - box[1],
    }


def validate_source(atlas: Image.Image, source_path: Path) -> Dict[Tuple[int, int], Box]:
    if atlas.size != ATLAS_SIZE:
        raise ValueError(f"{source_path}: expected {ATLAS_SIZE}, got {atlas.size}")
    if "A" not in atlas.getbands():
        raise ValueError(f"{source_path}: atlas has no alpha channel")

    boxes: Dict[Tuple[int, int], Box] = {}
    for state_index, frame_index, sprite_index in required_cells():
        box = alpha_box(atlas.crop(cell_box(sprite_index)))
        if box is None:
            raise ValueError(
                f"{source_path}: required {STATE_NAMES[state_index]} "
                f"frame {frame_index} is empty"
            )
        boxes[(state_index, frame_index)] = box

    for sprite_index in unused_cells():
        if alpha_box(atlas.crop(cell_box(sprite_index))) is not None:
            raise ValueError(
                f"{source_path}: unused sprite index {sprite_index} is not transparent"
            )
    return boxes


def normalize_one(source_path: Path, destination_path: Path) -> Dict[str, object]:
    with Image.open(source_path) as opened:
        source = opened.convert("RGBA")

    source_boxes = validate_source(source, source_path)
    source_union = union_boxes(list(source_boxes.values()))
    union_width = source_union[2] - source_union[0]
    union_height = source_union[3] - source_union[1]
    # v0.1.4 already renders at the final safe envelope. Never enlarge raster
    # pixels here; this pass may only preserve size or reduce an oversized union.
    scale = min(1.0, TARGET_MAX_WIDTH / union_width, TARGET_MAX_HEIGHT / union_height)
    target_width = max(1, min(TARGET_MAX_WIDTH, round(union_width * scale)))
    target_height = max(1, min(TARGET_MAX_HEIGHT, round(union_height * scale)))
    target_left = TARGET_CENTER_X - target_width // 2
    target_top = TARGET_BOTTOM - target_height

    output = Image.new("RGBA", ATLAS_SIZE, (0, 0, 0, 0))
    source_crop_box = source_union
    for state_index, frame_index, sprite_index in required_cells():
        cell = source.crop(cell_box(sprite_index)).crop(source_crop_box)
        # Resize premultiplied RGBA to avoid dark or light fringes at transparent
        # edges, then convert back to ordinary straight-alpha PNG pixels.
        fitted = cell.convert("RGBa").resize(
            (target_width, target_height), Image.Resampling.LANCZOS
        ).convert("RGBA")
        frame = Image.new("RGBA", (CELL_WIDTH, CELL_HEIGHT), (0, 0, 0, 0))
        frame.alpha_composite(fitted, (target_left, target_top))
        atlas_left, atlas_top, _, _ = cell_box(sprite_index)
        output.alpha_composite(frame, (atlas_left, atlas_top))

    destination_path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = destination_path.with_name(destination_path.name + ".tmp")
    try:
        output.save(temporary_path, format="PNG", optimize=True, compress_level=9)
        # Validate the encoded bytes, not only the in-memory image, then publish
        # atomically so an interrupted run can never leave a partial final PNG.
        with Image.open(temporary_path) as encoded:
            encoded.load()
            encoded_rgba = encoded.convert("RGBA")
        final_boxes = validate_source(encoded_rgba, temporary_path)
        output_digest = sha256(temporary_path)
        os.replace(temporary_path, destination_path)
    finally:
        temporary_path.unlink(missing_ok=True)

    if sha256(destination_path) != output_digest:
        raise ValueError(f"{destination_path}: encoded bytes changed after atomic publish")
    final_union = union_boxes(list(final_boxes.values()))
    margins = {
        "left": min(box[0] for box in final_boxes.values()),
        "top": min(box[1] for box in final_boxes.values()),
        "right": min(CELL_WIDTH - box[2] for box in final_boxes.values()),
        "bottom": min(CELL_HEIGHT - box[3] for box in final_boxes.values()),
    }
    if min(margins.values()) < MIN_EDGE_MARGIN:
        raise ValueError(f"{destination_path}: unsafe final margins {margins}")

    idle_heights = [
        final_boxes[(0, frame_index)][3] - final_boxes[(0, frame_index)][1]
        for frame_index in range(FRAME_COUNTS[0])
    ]
    all_heights = [box[3] - box[1] for box in final_boxes.values()]
    all_widths = [box[2] - box[0] for box in final_boxes.values()]

    return {
        "source": str(source_path),
        "output": str(destination_path),
        "source_sha256": sha256(source_path),
        "output_sha256": output_digest,
        "source_union": box_dict(source_union),
        "target_region": {
            "left": target_left,
            "top": target_top,
            "width": target_width,
            "height": target_height,
        },
        "scale": round(scale, 6),
        "final_union": box_dict(final_union),
        "minimum_margins": margins,
        "idle_height_median": statistics.median(idle_heights),
        "frame_height_range": [min(all_heights), max(all_heights)],
        "frame_width_range": [min(all_widths), max(all_widths)],
        "required_cells_nonempty": sum(FRAME_COUNTS),
        "unused_cells_transparent": COLUMNS * ROWS - sum(FRAME_COUNTS),
        "valid": True,
    }


def find_atlases(input_root: Path) -> List[Path]:
    paths = sorted(input_root.glob("*/atlas.png"))
    if paths:
        return paths
    if input_root.name == "atlas.png" and input_root.is_file():
        return [input_root]
    raise FileNotFoundError(f"No character atlas.png files found under {input_root}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="UmaCodexPet timestamp directory")
    parser.add_argument("output", type=Path, help="Destination directory")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    atlases = find_atlases(args.input)
    reports: List[Dict[str, object]] = []
    for source_path in atlases:
        character_name = source_path.parent.name
        destination_path = args.output / character_name / "atlas.png"
        reports.append(normalize_one(source_path, destination_path))

    manifest = {
        "format": "UmaCodexPet normalized atlas report",
        "layout": {
            "atlas": list(ATLAS_SIZE),
            "cell": [CELL_WIDTH, CELL_HEIGHT],
            "columns": COLUMNS,
            "rows": ROWS,
            "states": [
                {
                    "name": name,
                    "frames": FRAME_COUNTS[state_index],
                    "sprite_indices": list(STATE_SPRITE_INDICES[state_index]),
                }
                for state_index, name in enumerate(STATE_NAMES)
            ],
        },
        "method": "one common alpha-union crop per character; downscale only, never enlarge",
        "characters": reports,
    }
    args.output.mkdir(parents=True, exist_ok=True)
    manifest_path = args.output / "normalization-report.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Normalized {len(reports)} atlases -> {args.output}")
    print(f"Validation report -> {manifest_path}")


if __name__ == "__main__":
    main()
