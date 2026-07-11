# Changelog

All notable user-facing changes are recorded here.

## 0.2.0 - 2026-07-12

Initial public alpha.

- Added a searchable <kbd>F6</kbd> character picker inside UmaViewer with
  **Select All**, **Select Visible**, **Clear**, **Save**, and
  **Save & Export** actions.
- Fresh installs now start with no selected characters. The exact legacy
  12-character default is migrated to an empty selection, while custom rosters
  are preserved.
- Added searchable, per-character Mini clothes selection from outfits that are
  actually available in the local UmaViewer asset index, with **Auto** as the
  default.
- **Save** accepts an empty selection; **Save & Export** requires at least one
  character.
- Added stable character and mini-animation catalogs generated with <kbd>F7</kbd>.
- Added per-character and wildcard motion selection through an optional CSV file.
- Filtered the catalog to renderable Mini body clips and preserved signed
  64-bit motion keys as strings for browser-safe selection.
- Kept automatic motion resolution as the default; animation overrides remain
  an optional CSV-based advanced feature.
- Encoded the exact Windows Work-pet `8 × 9` layout: six idle frames, six
  working frames, the canonical slots for the other seven states, and all 15
  unassigned cells fully transparent.
- Added explicit documentation for the host-controlled frame rate and 192 by 208
  cell-resolution limits.
- Documented `atlas.png` as the canonical encoded sprite sheet and added a
  pre-release visual inspection and target-app smoke-test checklist.
- Added allowlisted release packaging with config templates and SHA-256 checksums.
