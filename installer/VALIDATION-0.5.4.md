# Validation - 0.5.4 Preview Fixes

## Reproduced regression

The installed 0.5.3 plugin opened its preview on the first invocation of
`AssetCopilot`, but failed on the second invocation in the same Rhino process.
The captured exception originated in `SetLoaderDllFolderPath`: the shared
WebView2 loader was already loaded. This also removed browser-owned loading
feedback. The preview had no browser file-drop handler.

## Completed checks

- Product builds: `net48` and `net7.0-windows`, zero warnings and errors.
- Core suite: 134 local checks passed, including mocked generation/recovery,
  image-size estimation, units, compressed GLB decoding, and argument quoting.
  The vision portion was rerun in isolation after a concurrent worker failure.
- Rhino 8.35 UI suite: 184 checks passed in each of .NET Framework 4.8,
  .NET 7.0.1, and .NET 8.0.24, in sequential sessions.
- Chromium file drag events exercised the real FileReader-to-native attachment
  path, including original filename/content preservation, busy-state rejection,
  unsupported files, and multiple-file rejection. The native startup/failure
  placeholder also accepted Windows file-drop data.
- The supplied pixel animation plays, loops across its end, keeps its position
  across progress updates, and stops on completion or cancellation.
- A simulated browser failure preserved native progress text and pixel video.
  Compact mode paused the hidden fallback animation and expansion resumed it.
- Retry created a working PBR viewer without restarting Rhino, and retained the
  attachment. Two additional window opens rendered PBR after the shared loader
  was already in use.
- Existing full/compact layouts, input focus border, scroll clipping, logo
  timing, dimensions, materials, and reference-face behavior passed regression.
- Installer path suite: 5 checks passed; the launcher contract retains Rhino's
  current runtime and invokes the normal AssetCopilot command.
- The Remy logo and pixel-block video files are unchanged.

## Installed release check

The 0.5.4 Update installer completed in the existing program folder. The installed
RHP hash matches the release build; settings and task-pointer hashes are unchanged.
Full, Standard, Update and SHA256SUMS files were produced and their checksums verified.

The installed plugin was opened twice through the normal `AssetCopilot` command
in each tested mode: normal startup (.NET Framework), .NET 7 and .NET 8. Each open
verified the actual installed assembly path/version, first rendered frame, PBR
materials, looping pixel animation, progress text, and a Chromium image-file drop.

## Scope and limits

No paid API generation was performed. Generation status, cancellation and recovery
were exercised locally with mocks/status events; actual PBR models and local
image recognition were used. Host testing used Rhino 8.35. The SDK baseline
remains Rhino 8.0; original Rhino 8.0 and independent clean-machine tests are
pending. This remains an unsigned beta. No dependency versions were changed.
