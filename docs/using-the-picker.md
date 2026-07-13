# Using the UmaCodexPet picker

This guide covers character, outfit, motion, and face selection in the F6
picker, plus the advanced F7 motion-override workflow.

[Back to the main README](../README.md)

## Selecting characters, clothes, motions, and faces

### Choosing characters in UmaViewer

Press <kbd>F6</kbd> after UmaViewer finishes loading. The picker searches the
loaded character list by display name, internal/English name, or numeric ID.
Use **Select All** to choose every loaded character, **Select Visible** to add
every current search result, or **Clear** to start over. A full **Select All**
export can take a long time.

Toggling an individual character on queues that character's Mini and current
draft clothes for live preview without saving the picker. Bulk selection does
not choose an arbitrary preview target. If several individual choices are made
while UmaViewer is loading, UmaCodexPet finishes the active load safely and
keeps only the latest queued choice.

Every F6 session starts with zero selected characters, Auto clothes, and Auto
motion/face choices. **Set F8 Batch** may set an empty batch, while **Set F8
Batch & Export** requires at least one selected character. Reopening the picker
starts clean even after setting or exporting a batch. The last F8 batch remains
in the generated config for closed-picker F8 and advanced manual editing, but
it is never loaded back into a new picker draft.

Editing `Characters` directly remains supported as an advanced fallback. It
accepts a comma-separated set of display names or numeric IDs:

```text
<UmaViewer>\BepInEx\config\dev.pqqqqq.umacodexpet.cfg
```

For example:

```ini
Characters = Special Week, 1003, Rice Shower
```

### Choosing Mini clothes in UmaViewer

Select a character in the <kbd>F6</kbd> picker, then click the **Clothes**
button on that row. The second screen is searchable by outfit display name or
costume ID and contains only Mini outfits discovered in the local UmaViewer
asset index. Choose one outfit to return to the character list, or choose
**Auto / character default** to let UmaCodexPet select the normal Mini outfit.
Each selected character can use a different outfit. Either choice immediately
queues a reload of that character for live preview; changing clothes quickly
replaces obsolete queued reloads with the latest choice.

**Set F8 Batch** and **Set F8 Batch & Export** store explicit outfit choices for
selected characters in the generated BepInEx setting `CharacterCostumes`. A
new F6 picker does not restore them. Editing the setting directly is an
advanced fallback; its format is a semicolon-separated set of
`characterId=costumeId` pairs:

```ini
CharacterCostumes = 1001=01;1067=02
```

Leave it empty to use **Auto** for every character. If a saved outfit is no
longer available after a game update, the export warns and falls back to Auto.

### Choosing motions and faces in UmaViewer

Select a character in the <kbd>F6</kbd> picker, then click
**Animations/Face**. The page exposes all nine canonical states: four direct
interaction states plus five task-status states. The state list scrolls
when the picker is small, and the bottom-right grip resizes the whole picker:

| Picker state | Codex behavior |
| --- | --- |
| **Idle / resting** | The pet is waiting on screen |
| **Run right** | The pet is dragged or moves toward the right |
| **Run left** | The pet is dragged or moves toward the left |
| **Wave / greeting** | The canonical greeting slot |
| **Hover / jump** | The pointer moves over the pet |
| **Failure / error** | The canonical failure-reaction slot |
| **Waiting** | The canonical waiting slot |
| **Working** | The active-work or processing slot |
| **Review** | The review or inspection slot |

While F6 is open, the picker is modal: mouse-wheel, click, and drag input is
kept away from UmaViewer's camera and controls behind it.

Each state's **Motion** button opens a searchable list of compatible Mini
clips. A blank search pins every compatible dance clip for **Idle**, `near05`
for **Hover / jump**, and matching character-specific special clips alongside
the normal recommendations for that state. Searches such as `dance`, `run`,
`walk`, `happy`, `jump`, `character`, or `general` browse the wider local
catalog. Friendly labels end in **Character** or **General**, so character-only
motions can be distinguished from shared motions without reading asset paths.

Selecting a motion previews it immediately on a matching loaded Mini. If that
Mini is absent, UmaCodexPet automatically loads the selected character with
the outfit currently chosen in F6 and then starts the chosen clip. The list
stays open after a selection so several motions can be clicked and compared
back-to-back. Use **Load Mini for preview** when no preview is ready, **Retry
preview** after a failed load, or **Reload selected clothes** to rebuild the
Mini after changing its F6 outfit. **Auto / advanced CSV fallback** removes the
F6 choice: a valid CSV override is then used if one exists, otherwise
UmaCodexPet uses its automatic motion resolver.

Each state's **Face** button controls a static set of Mini texture slots:
left and right eye (`0`–`14`), mouth (`0`–`18`), and left and right eyebrow
(`0`–`8`). These values hold for every captured frame in that state; they do
not create a separately animated facial sequence. Opening the face editor
automatically queues the selected character and its current F6 clothes when a
matching Mini is not already loaded; the controls unlock when that preview is
ready. The same **Retry preview** and **Reload selected clothes** controls can
recover or refresh it. **Auto / default face** keeps the normal Mini face
without a static override.

**Set F8 Batch** and **Set F8 Batch & Export** store F6 choices for selected
characters, but a new F6 session always starts on Auto. **Set F8 Batch &
Export** also closes the picker and begins the export immediately. Editing the
generated settings directly is supported only as an advanced fallback:

```ini
CharacterStateMotions = 1001:idle=-4064598427829042606;1001:run_left=4494058001413988142
CharacterStateFaces = 1001:idle=0,0,3,1,1;1001:jump=4,4,8,2,2
```

For all nine F6 states, selection precedence is: valid F6 motion, exact CSV
row, wildcard CSV row, then automatic resolution. An unavailable saved motion
is discarded with a warning and falls through to the next source. Face
choices are independent of motion selection and default to Auto.

### Choosing source motions (advanced)

Press <kbd>F7</kbd> after UmaViewer finishes loading to refresh the stable,
non-timestamped catalog directory:

```text
<UmaViewer>\UmaCodexPet_Output\catalog\
├── characters.json
├── mini-animation-catalog.json
└── HOW_TO_SELECT.txt
```

With the default `MotionOverridesFile` setting, it also creates
`UmaCodexPet_Overrides.csv` beside `UmaViewer.exe` if that file does not already
exist.

To choose a source clip for a state, edit `UmaCodexPet_Overrides.csv`. Each
enabled row has this form:

```csv
character_id,state,motion_key_or_exact_asset_name
1001,idle,-4064598427829042606
*,wave,4494058001413988142
```

The motion value may be a signed numeric key or the exact asset name shown in
`mini-animation-catalog.json`. Motion keys are stored as strings so their full
64-bit values survive browser and JavaScript tooling. Catalog rows also label
their scope, compatible character ID when applicable, and suggested states.
The CSV intentionally uses exactly three unquoted fields; commas inside values
are not supported. Use `*` as the character ID for a compatible override
applied to every selected character; an exact character row wins over
the matching wildcard row. Set that exact row's value to `auto` to opt one
character out of a wildcard. Valid states are `idle`, `run_right`, `run_left`,
`wave`, `jump`, `failure`, `waiting`, `working`, and `review`.

A missing or comments-only override CSV preserves automatic motion selection
for states without an explicit F6 choice. The CSV remains useful for wildcard
rules, bulk editing, and reproducible advanced overrides. Pressing
<kbd>F8</kbd> reloads the BepInEx config and override CSV before each export,
so changing an advanced selection does not require restarting UmaViewer.

See the committed
[`dev.pqqqqq.umacodexpet.example.cfg`](../config/dev.pqqqqq.umacodexpet.example.cfg)
and
[`UmaCodexPet_Overrides.example.csv`](../config/UmaCodexPet_Overrides.example.csv)
for copyable defaults and examples.
