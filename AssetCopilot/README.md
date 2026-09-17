# Remy Asset Copilot 0.5.3

This directory contains the Rhino plugin source and redistributable resources.
See the [main README](../README.md) for installation and usage, and the
[installer guide](../installer/README.md) for packaging instructions.

Run `build.cmd` to build the universal `net48` plugin into `dist-universal`.
The source also supports `net7.0-windows` for development checks. Distribute the
universal build using the Windows installers; do not mix framework build outputs.

After installation, open Rhino normally and run `AssetCopilot`. No separate
launcher or runtime switch is needed. The interface, messages, and installer
are in English; descriptions and user file names are preserved as entered.
