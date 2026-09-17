# Remy Asset Copilot

Turn images and text into scene-ready 3D assets directly inside Rhino.

**Version 0.5.2 Beta · Windows installer**

Install Remy, open Rhino normally, and run `AssetCopilot` in the command line. No dedicated launcher or manual .NET runtime switching is required.

[Download 0.5.2 Beta](https://github.com/yuhanuyour-dot/remy-asset-copilot/releases/tag/v0.5.2) · [All releases](https://github.com/yuhanuyour-dot/remy-asset-copilot/releases)

## Features

- Generate assets from text, an uploaded or pasted image, or an image with a text description.
- Import local GLB files, including supported compressed models.
- Preview PBR materials and place models directly into a Rhino scene.
- Choose manual dimensions, estimated real-world dimensions, or a selected reference face, with conversion to Rhino document units.
- Switch between full and compact windows while retaining input and task state.
- Save models, textures, and task records locally, and resume checking previously submitted generation tasks.

Real-world dimensions are estimates, not measurements from a photograph. Image-based estimation uses a local CLIP model and size rules; text-based estimation uses object descriptions and explicit dimensions.

## Download

Open the **0.5.2 Beta** release and expand **Assets**. Choose an installer:

| File | Use |
| --- | --- |
| `RemyAssetCopilot-Setup-0.5.2-Full.exe` | Recommended for first-time installation. Includes the optional local image-based size estimation component. |
| `RemyAssetCopilot-Setup-0.5.2-Standard.exe` | Smaller installer without the local image-based size estimation model. Supports generation, GLB import, and the other sizing methods. |
| `RemyAssetCopilot-Setup-0.5.2-Update.exe` | Updates an existing 0.5.0 or newer universal installation. Not suitable for first-time installation or migration from 0.4.x. |
| `SHA256SUMS-0.5.2.txt` | SHA-256 checksums for verifying the installers. |

The **Code → Download ZIP** option and automatically generated **Source code** archives contain source files, not an installer. Because 0.5.2 is a pre-release, an older release may still carry GitHub's **Latest** label; select 0.5.2 explicitly.

## Install and Run

1. Save your work and close all Rhino windows.
2. Download and run the appropriate installer.
3. Confirm your Rhino executable, program directory, and data directory. Any supported local drive can be used; a D: drive is not required.
4. Complete installation and open Rhino normally.
5. Create or open a document, type `AssetCopilot` without spaces, and press Enter.
6. Enter your own **Tripo API key** in the plugin to generate models. Your API account needs available credits.

To test without spending API credits, use the **+** menu to import a local GLB. A sample is included at `AssetCopilot\sample\demo-chair.glb` inside the installation directory.

The installer includes the required Node.js component. End users do not need Python, a .NET SDK, or development tools. Installation is per Windows user and normally requires no administrator privileges.

For an existing installation, retain the same program directory when upgrading. The installer preserves the existing data location. Use Standard or Full when migrating from 0.4.x. Uninstalling retains models, textures, settings, and working data.

## Compatibility and Beta Status

- **Platform:** Windows 10/11, x64.
- **Target host:** Rhino 8.0 and subsequent Rhino 8.x releases, built against the Rhino 8.0 SDK.
- **Runtime modes:** .NET Framework, .NET 7, and .NET 8.
- **Verified host:** Rhino 8.35; 161 interface, material, sizing, and placement checks passed in each runtime mode. The installed plugin also passed direct `AssetCopilot` command-launch checks in all three modes.
- **Pending:** Testing on the original Rhino 8.0 host and independent clean-machine validation.
- **Not covered:** macOS, Rhino 7, and Rhino 9.

Version 0.5.2 fixes the .NET Framework initialization failure in 0.5.1. Use the 0.5.2 installer for the normal Rhino command workflow.

This is a beta release. The installer is not code-signed, so Windows or your browser may display a reputation warning.

## API and Local Data

Online generation uses your own Tripo API account. The key is used within the current plugin window; do not include it in source code, screenshots, or shared files. Image-plus-text generation may involve both image processing and 3D generation, with charges determined by the Tripo API.

Default locations:

| Content | Location |
| --- | --- |
| Program | `%LOCALAPPDATA%\Programs\RemyAssetCopilot` |
| Models, settings, and working data | `%LOCALAPPDATA%\RemyAssetCopilot\Data` |

Both locations can be selected during installation. The model save folder can also be changed in the plugin. The local `remy-install.ini` records machine-specific paths and should not be committed to Git.

## Build from Source

Install the **.NET 8 SDK** on Windows, then run:

```bat
AssetCopilot\build.cmd
```

The first build restores pinned Rhino 8.0 and WebView2 SDK packages from NuGet; it does not require local Rhino development references. The source supports `net48` and `net7.0-windows`. The installer distributes the `net48` compatibility assembly tested in all three Rhino runtime modes.

Building the Windows installers additionally requires Python, Inno Setup, and the runtime components described in [the installer build guide](installer/README.md) (currently in Chinese).

Source code and redistributable resources are in `AssetCopilot/src`, `AssetCopilot/viewer`, `AssetCopilot/tools`, and `AssetCopilot/sample`. Personal images, generated assets, API keys, task records, build caches, and large runtime components are excluded from the source export.

## Publishing Updates

Commit the matching source code and README to the repository before creating a release tag. For this version, use `v0.5.2` and mark the release as a pre-release. Attach the Full, Standard, and Update installers and `SHA256SUMS-0.5.2.txt` to the release rather than committing binaries to the source tree.

Publishing a release does not automatically update the README or source files on the default branch. See [RELEASE_NOTES.md](RELEASE_NOTES.md) for the release description.

## License and Credits

See [third-party notices](AssetCopilot/THIRD-PARTY-NOTICES.md) for component licenses. No project-wide source code license has been selected yet. The Remy logo and supplied animation assets are separate from the third-party code licenses.
