# Third-party software and content

UmaCodexPet is an independent interoperability tool. The repository contains
only original plugin source, build files, and documentation. It does not
contain or grant rights to game models, textures, animations, audio, database
files, Unity runtime files, UmaViewer binaries, or BepInEx binaries.

## Runtime relationships

| Project or content | Owner / upstream | Relationship to UmaCodexPet | Included here? |
| --- | --- | --- | --- |
| Uma Musume: Pretty Derby | Cygames, Inc. | User-supplied local source data | No |
| UmaViewer | [katboi01/UmaViewer](https://github.com/katboi01/UmaViewer) | Host application and renderer | No |
| Unity runtime | Unity Technologies | Runtime used by UmaViewer | No |
| BepInEx 5 | [BepInEx/BepInEx](https://github.com/BepInEx/BepInEx) | User-installed Unity Mono plugin loader | No |
| Codex desktop custom pets | OpenAI | Target sprite-sheet format and host renderer | No |
| Microsoft .NET Framework reference assemblies | Microsoft | Compile-time `net472` reference package | No; restored by the .NET SDK/NuGet |
| Pillow | Pillow contributors | Optional atlas validation and normalization tooling | No; installed by the developer |
| ImageMagick | ImageMagick Studio LLC and contributors | Optional preview rendering | No |
| FFmpeg | FFmpeg contributors | Optional GIF/MP4 preview rendering | No |

Each dependency and host application remains subject to its own terms and
license. Follow the upstream installation instructions and obtain binaries
from their official release channels.

## Asset boundary

UmaCodexPet resolves asset identifiers at runtime and asks the user's local
UmaViewer process to render them. No game asset is embedded in the plugin or
its configuration. Exported images remain derived from the user's locally
installed content and are **not** covered by this repository's MIT License.

You are responsible for determining whether you may create, use, or share an
export. Personal local use does not automatically grant redistribution rights.
Do not publish generated sprite sheets, extracted assets, or game data unless
you have permission from the relevant rightsholder.

## Trademarks and affiliation

Uma Musume: Pretty Derby and related names, characters, artwork, and marks are
the property of their respective owners. UmaCodexPet is not affiliated with,
authorized by, sponsored by, or endorsed by Cygames, the UmaViewer maintainers,
Unity Technologies, or OpenAI.

References to third-party products are descriptive and do not imply
endorsement.
