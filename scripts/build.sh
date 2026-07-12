#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project="$repo_root/src/UmaCodexPet/UmaCodexPet.csproj"
configuration="${CONFIGURATION:-Release}"
viewer_dir="${UMAVIEWER_DIR:-}"
version="${VERSION:-}"
install=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --viewer-dir)
      viewer_dir="${2:?missing value for --viewer-dir}"
      shift 2
      ;;
    --configuration)
      configuration="${2:?missing value for --configuration}"
      shift 2
      ;;
    --version)
      version="${2:?missing value for --version}"
      shift 2
      ;;
    --install)
      install=true
      shift
      ;;
    -h|--help)
      echo "Usage: scripts/build.sh [--viewer-dir PATH] [--configuration Release|Debug] [--version VERSION] [--install]"
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK 8 or newer is required." >&2
  exit 1
fi
dotnet_bin="$(command -v dotnet)"

if [[ -z "$viewer_dir" ]]; then
  echo "Set UMAVIEWER_DIR or pass --viewer-dir with the folder containing UmaViewer.exe." >&2
  exit 1
fi

viewer_dir="$(cd "$viewer_dir" && pwd)"
[[ -f "$viewer_dir/UmaViewer.exe" ]] || { echo "UmaViewer.exe not found in $viewer_dir" >&2; exit 1; }
[[ -f "$viewer_dir/UmaViewer_Data/Managed/umamusume.dll" ]] || { echo "umamusume.dll not found under $viewer_dir/UmaViewer_Data/Managed" >&2; exit 1; }
[[ -f "$viewer_dir/BepInEx/core/BepInEx.dll" ]] || { echo "BepInEx 5 is not installed under $viewer_dir/BepInEx" >&2; exit 1; }

build_args=(build "$project" --configuration "$configuration" "-p:UmaViewerDir=$viewer_dir")
if [[ -n "$version" ]]; then
  build_args+=("-p:Version=$version")
fi
"$dotnet_bin" "${build_args[@]}"

build_dir="$repo_root/artifacts/bin/$configuration/net472"
plugin_dll="$build_dir/UmaCodexPet.dll"
[[ -f "$plugin_dll" ]] || { echo "Expected build output was not produced: $plugin_dll" >&2; exit 1; }

package_root="$repo_root/artifacts/package"
package_dir="$package_root/BepInEx/plugins/UmaCodexPet"
rm -rf "$repo_root/artifacts/package"
mkdir -p "$package_dir"
cp "$plugin_dll" "$package_dir/"
if [[ -f "$build_dir/UmaCodexPet.pdb" ]]; then
  cp "$build_dir/UmaCodexPet.pdb" "$package_dir/"
fi

# Keep release contents on an explicit allowlist. In particular, never copy
# exporter output or game assets into the distributable archive.
for release_file in README.md CHANGELOG.md LICENSE THIRD_PARTY.md; do
  [[ -f "$repo_root/$release_file" ]] || { echo "Required release file is missing: $release_file" >&2; exit 1; }
  cp "$repo_root/$release_file" "$package_root/"
done

package_config_dir="$package_root/config"
mkdir -p "$package_config_dir"
for config_file in dev.pqqqqq.umacodexpet.example.cfg UmaCodexPet_Overrides.example.csv; do
  [[ -f "$repo_root/config/$config_file" ]] || { echo "Required config example is missing: config/$config_file" >&2; exit 1; }
  cp "$repo_root/config/$config_file" "$package_config_dir/"
done

if [[ "$install" == true ]]; then
  plugins_root="$viewer_dir/BepInEx/plugins"
  legacy_install_dir="$plugins_root/UmaPetForge"
  legacy_files_removed=0
  while IFS= read -r -d '' legacy_file; do
    rm -f "$legacy_file"
    legacy_files_removed=$((legacy_files_removed + 1))
  done < <(find "$plugins_root" -type f \( -iname 'UmaPetForge.dll' -o -iname 'UmaPetForge.pdb' \) -print0)
  rmdir "$legacy_install_dir" 2>/dev/null || true
  if (( legacy_files_removed > 0 )); then
    echo "Removed $legacy_files_removed legacy UmaPetForge plugin file(s)"
  fi
  install_dir="$viewer_dir/BepInEx/plugins/UmaCodexPet"
  mkdir -p "$install_dir"
  cp "$plugin_dll" "$install_dir/UmaCodexPet.dll"
  echo "Installed $install_dir/UmaCodexPet.dll"
fi

echo "Built $plugin_dll"
echo "Packaged $package_root"
