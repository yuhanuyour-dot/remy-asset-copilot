# Build the Windows Installers

End users need Windows 10/11 x64 and Rhino 8.0 or a later Rhino 8.x release.
They do not need Python, a .NET SDK, Inno Setup, or a separate Node.js installation.
The plugin supports Rhino's Framework and Core modes without changing runtime
preferences. Open Rhino normally and run `AssetCopilot`.

## Build requirements

- Windows x64 and the .NET 8 SDK. NuGet restores RhinoCommon 8.0.23304.9001,
  WebView2 1.0.1938.49, and the pinned compatibility dependencies.
- Source targets: `net48` and `net7.0-windows`. Installers use the universal
  `net48` assembly. Local Rhino development references are not needed for it.
- Python 3.9+ (standard library only) and Inno Setup 6.7.3.
- Redistributable `runtime/` from an installed Full release, supplied through
  `-RuntimeSource`. Large runtime components are not committed to Git.
- Bundled components: Node 24.19.0, Transformers.js 3.8.1, and ONNX Runtime
  1.21.0. See `runtime-manifests/` for dependency and model details.

From the repository root, replacing the example paths:

```powershell
.\installer\Build-Installer.ps1 -Iscc "<Inno Setup folder>\ISCC.exe" -RuntimeSource "<Full installation>\runtime"
```

If Python is not available as `python`, add `-Python "<path to python.exe>"`.
Use `-Variants Standard`, `Full`, or `Update` to build a subset.

The script builds the plugin, creates a fresh staging folder in `work`, copies
redistributable files, compiles installers, and writes them with SHA-256 checksums
to `releases`. It does not include settings, keys, task records, or personal assets.

Standard includes the GLB decoder runtime. Full adds offline image size estimation.
Update contains program changes only and requires an existing universal installation.
`stage.py` excludes unused operating-system binaries and redundant model weights
only in the staging copy. It retains the joint CLIP model and component licenses.

## Validation

```powershell
dotnet run --project .\installer\tests\AppPaths.Tests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\tests\LaunchContract.Tests.ps1
```

Compiling Inno Setup with `/DQA=1` creates a separate test installer identity and
Rhino test registry branch. Do not use this flag for public installers.

Setup accepts `/DIR=...`, `/DATADIR=...`, `/RHINOEXE=...`, and `/LANG=en`.
The installer and user guide are English. Existing DataRoot takes precedence over
command-line values during upgrades to avoid accidentally switching user data.

`remy-install.ini` uses UTF-16 and records local paths. Uninstall retains user
models, textures, settings, and working data. Upgrades back up the previous plugin
folder under the data folder's `backups` directory. Setup never terminates Rhino.

## Rhino compatibility

The distributed assembly uses the Rhino 8.0 SDK as its API baseline. Rhino 8.35
supports it in Framework, .NET 7, and .NET 8 modes. Original Rhino 8.0 host testing
and independent clean-machine validation remain pending. Keep Core development
outputs separate from the `net48` distribution folder.

The plugin GUID and `AssetCopilot` command are stable. The launcher invokes only
that command and does not set `/netcore`, `/netfx`, or a global runtime preference.
Compatibility helpers are in `Compat.cs`. The optional `Texture.TreatAsLinear`
property on newer Rhino versions is accessed reflectively; older hosts retain
their native defaults.

Dependency versions remain as recorded in `runtime-manifests/dotnet-0.5.2.json`;
0.5.4 fixes preview startup, image drops, and loading feedback without upgrading dependencies.
See [VALIDATION-0.5.4.md](VALIDATION-0.5.4.md) for this release's verification.

Installers are currently unsigned beta builds. Code signing and independent
clean-machine testing are still required before treating them as production releases.
