# Validation - 0.5.3 English Edition

This release translates product-owned text to English. It preserves user input,
file and document names, saved data, and English/Chinese input recognition.

## Completed checks

- Universal `net48` plugin build and `net7.0-windows` development build: successful,
  with zero compiler warnings or errors in the product builds.
- Core regression suite: 134 checks passed on .NET Framework, including local
  compressed GLB decoding, image recognition, text dimensions, cancellation,
  argument quoting, request construction, and redacted API error messages.
- Rhino 8.35 native UI suite: 168 checks passed in each of .NET Framework 4.8,
  .NET 7.0.1, and .NET 8.0.24. Sessions were run sequentially.
- Full/compact layout verified at existing widths, including 450, 540, and 720 DIP.
  English model labels fit the original selector. Settings, sizing, and compact
  screenshots were visually reviewed. The complete input and toolbar remain visible.
- Preview uses English instructions, accessible labels, progress, and error text.
  The idle-status filter was updated with its translated messages.
- Existing PBR materials, placement, scroll clipping, focus border, compact-window
  sizing, and reference-face behavior passed their regression checks.
- Logo and loading animation files are byte-for-byte unchanged. Blink, tail, and
  loading loop behavior passed existing tests.
- Installer path tests: 5 checks passed. The launcher contract keeps Rhino's
  current runtime and invokes the normal `AssetCopilot` command.
- Source audit found no remaining Chinese presentation text in the active plugin,
  preview, startup messages, or shipped English guides. Multilingual parser terms
  and historical source documentation are intentionally retained.

- Installed plugin version 0.5.3.0 opened successfully using the normal
  `AssetCopilot` command in all three tested runtime modes. The normal launch
  used the current Rhino runtime setting without an override.
- The update installer completed in the existing program directory. The installed
  plugin hash matches the build; existing settings and task-pointer hashes are
  unchanged. The model/data location is retained.

## Scope and limits

All generation request tests use mocks. No paid API generation was performed for
this localization release. Local preview and image recognition use existing test
assets. API responses and user-authored content retain their source language.

The SDK baseline remains Rhino 8.0. Actual host verification used Rhino 8.35;
original Rhino 8.0 and independent clean-machine validation are still pending.
The installers are unsigned beta builds. Compatibility dependencies are unchanged
from 0.5.2; the `dotnet-0.5.2.json` dependency manifest remains applicable.
