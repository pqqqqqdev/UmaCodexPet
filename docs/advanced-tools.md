# Deterministic fitting and previews

Use these optional tools to validate normalized atlases and generate a complete
diagnostic preview suite without changing the upload input.

[← Back to the main README](../README.md)

The repository also includes an auditable post-render validator for sheets that
have safe pixels but excess transparent padding. It never generates, redraws,
or enlarges artwork: one common alpha-union crop is applied to every frame and
oversized captures may only be reduced.

From the repository root, install Python 3.10 or newer and Pillow, then point
the normalizer at one export timestamp directory:

```bash
python -m pip install -r scripts/requirements.txt
python scripts/normalize_atlases.py \
  "/path/to/UmaCodexPet_Output/<timestamp>" \
  "/path/to/normalized-output"
```

The command writes one validated atlas per character plus
`normalization-report.json` with input/output hashes, alpha bounds, scale,
margins, required-cell checks, and unused-cell checks. Final files are
validated from their encoded PNG bytes and published atomically.

With ImageMagick 6 and FFmpeg installed, render the complete review suite for
one normalized atlas:

```bash
bash scripts/render_pet_previews.sh \
  "/path/to/normalized-output/Special Week_1001/atlas.png" \
  "/path/to/previews/Special Week"
```

That produces nine labeled state GIFs, an all-state GIF and MP4, a native-cell
idle-to-jump-to-idle loop, a labeled contact sheet, and four representative
stills. Preview files are never used as upload input; the validated atlas is.
