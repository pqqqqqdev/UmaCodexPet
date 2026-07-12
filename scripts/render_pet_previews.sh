#!/usr/bin/env bash
set -euo pipefail

atlas=${1:?usage: render_pet_previews.sh atlas.png output-directory}
output=${2:?usage: render_pet_previews.sh atlas.png output-directory}

cell_width=192
cell_height=208
columns=8
label_band=24
slugs=(idle run-right run-left wave jump failure waiting active-work review)
labels=("Idle" "Run Right" "Run Left" "Wave" "Jump" "Failure" "Waiting" "Active Work" "Review")
counts=(6 8 8 4 5 8 6 6 6)
sprite_maps=(
  "0 1 2 3 4 5"
  "8 9 10 11 12 13 14 15"
  "16 17 18 19 20 21 22 23"
  "24 25 26 27"
  "32 33 34 35 36"
  "40 41 42 43 44 45 46 47"
  "48 49 50 51 52 53"
  "56 57 58 59 60 61"
  "64 65 66 67 68 69"
)
delays=(6 6 6 6 6 6 6 6 6)
representative_columns=(3 4 4 2 2 4 3 3 3)

dimensions=$(identify -format '%w %h' "$atlas")
read -r width height <<<"$dimensions"
if [[ "$width $height" != "1536 1872" ]]; then
  echo "Expected a 1536x1872 atlas, got ${width}x${height}: $atlas" >&2
  exit 1
fi

temporary=$(mktemp -d)
trap 'rm -rf "$temporary"' EXIT
mkdir -p "$output/states" "$output/stills"
declare -a representatives

for row in "${!slugs[@]}"; do
  state=${slugs[$row]}
  mkdir -p "$temporary/$state"
  read -r -a sprite_indices <<<"${sprite_maps[$row]}"
  for ((column=0; column<counts[row]; column++)); do
    sprite_index=${sprite_indices[$column]}
    atlas_column=$((sprite_index%columns))
    atlas_row=$((sprite_index/columns))
    printf -v frame_number '%02d' "$column"
    raw="$temporary/$state/raw-$frame_number.png"
    labeled="$temporary/$state/frame-$frame_number.png"
    convert "$atlas" \
      -crop "${cell_width}x${cell_height}+$((atlas_column*cell_width))+$((atlas_row*cell_height))" \
      +repage "$raw"
    convert "$raw" \
      -background '#20242C' -gravity south -splice "0x${label_band}" \
      -fill white -font DejaVu-Sans-Bold -pointsize 13 \
      -annotate +0+4 "${labels[$row]}" "$labeled"
    if (( column == representative_columns[row] )); then
      representatives[$row]=$labeled
    fi
  done

  state_frames=("$temporary/$state"/frame-*.png)
  convert -delay "${delays[$row]}" -dispose Background \
    "${state_frames[@]}" -loop 0 -layers Optimize \
    "$output/states/$state.gif"
done

all_states=(convert)
for row in "${!slugs[@]}"; do
  state_frames=("$temporary/${slugs[$row]}"/frame-*.png)
  all_states+=(-delay "${delays[$row]}" -dispose Background "${state_frames[@]}")
  all_states+=(-delay 35 "${state_frames[$((${#state_frames[@]} - 1))]}")
done
all_states+=(-loop 0 -layers Optimize "$output/all-states.gif")
"${all_states[@]}"

ffmpeg -nostdin -y -loglevel error -i "$output/all-states.gif" \
  -filter_complex \
  "color=c=0x20242C:s=${cell_width}x$((cell_height+label_band)):r=25[bg];[0:v]fps=25,format=rgba[fg];[bg][fg]overlay=shortest=1:format=auto,format=yuv420p[v]" \
  -map '[v]' -an -c:v libx264 -crf 18 -preset medium \
  -movflags +faststart "$output/all-states.mp4"

idle_raw=("$temporary/idle"/raw-*.png)
jump_raw=("$temporary/jump"/raw-*.png)
convert \
  -delay "${delays[0]}" -dispose Background "${idle_raw[@]}" \
  -delay "${delays[4]}" -dispose Background "${jump_raw[@]}" \
  -delay "${delays[0]}" -dispose Background "${idle_raw[@]}" \
  -delay 45 "${idle_raw[0]}" \
  -loop 0 -layers Optimize "$output/idle-jump-idle.gif"

montage "${representatives[@]}" \
  -tile 3x3 -geometry "${cell_width}x$((cell_height+label_band))+12+12" \
  -background '#111318' "$output/contact-sheet.png"

still_rows=(0 1 4 7)
for row in "${still_rows[@]}"; do
  cp "${representatives[$row]}" \
    "$output/stills/$(printf '%02d' "$((row+1))")-${slugs[$row]}.png"
done

identify "$output/states/"*.gif "$output/all-states.gif" \
  "$output/idle-jump-idle.gif" "$output/contact-sheet.png" >/dev/null
ffprobe -v error -show_entries stream=width,height,pix_fmt,r_frame_rate \
  -of default=noprint_wrappers=1 "$output/all-states.mp4" >/dev/null
