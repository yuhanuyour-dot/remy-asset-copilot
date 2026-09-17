# Remy Asset Copilot 0.5.3 Beta

Turn images and text into scene-ready 3D assets directly inside Rhino.

## What's New

- Translated the full and compact interfaces, tooltips, accessibility labels,
  preview instructions, loading states, sizing explanations, errors, and Rhino
  command prompts into English.
- Added an English installer, user guide, README, and release notes.
- Preserved the existing layout, Remy cat animation, loading animation, PBR preview,
  window switching, model placement, and three sizing modes.
- Kept support for English and Chinese input descriptions. Your models, filenames,
  document names, settings, and task records are preserved.
- Retained the Rhino Framework / .NET 7 / .NET 8 compatibility introduced in 0.5.2.

## Download and Install

| Installer | Choose this when |
| --- | --- |
| `RemyAssetCopilot-Setup-0.5.3-Full.exe` | Installing for the first time with optional offline image size estimation. |
| `RemyAssetCopilot-Setup-0.5.3-Standard.exe` | Installing without the offline image size estimation model. |
| `RemyAssetCopilot-Setup-0.5.3-Update.exe` | Updating an existing 0.5.0 or newer universal installation. |

1. Save your work and close all Rhino windows.
2. Run the installer. Existing users should retain their current program folder.
3. Open Rhino normally, create or open a document, type `AssetCopilot`, and press Enter.
4. Enter your own Tripo API key under **Connection & task recovery** to generate models.

No dedicated launcher or manual runtime change is required. Use **+ > Add GLB model**
to test local preview and placement without spending API credits.

## Requirements and Notes

- Windows 10/11 x64; compatibility target: Rhino 8.0 and subsequent Rhino 8.x releases.
- Built against the Rhino 8.0 SDK. Testing on the original 8.0 host and an independent
  clean Windows machine remains pending. See the repository validation notes.
- Online generation requires your own Tripo API key and credits. Image plus text can
  use two billable stages: image editing and 3D generation.
- Real-world sizes are estimates, not measurements from a photograph.
- This is an unsigned beta release. Windows or your browser may display a reputation warning.
- `SHA256SUMS-0.5.3.txt` contains installer checksums.
- Download an `.exe` installer to install the plugin. The automatic **Source code**
  archives are intended for developers.
