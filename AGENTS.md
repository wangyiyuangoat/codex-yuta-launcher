# Working with this repository

This repository contains a Windows-only, unofficial Codex startup animation. Follow the user's request and all higher-priority instructions. This file is project guidance; it does not authorize unrelated actions or override user choices or tool restrictions.

## Installing for a user

1. Read `README.md`, `docs/INSTALL.md`, `Install.ps1`, and the referenced build script before executing installation.
2. Confirm that the machine is Windows and inspect the installer changes to the install directory, manifest, and desktop shortcut.
3. Install with `powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1` when installation is within the user's requested scope. Use `-InstallDir` if the user chooses a different directory; use `-AppId` for a confirmed Codex desktop AUMID when needed.
4. The intended result is an isolated installation and a separate `Codex - Yuta` desktop shortcut. Preserve the official application files and shortcut. Installation must not launch Codex by default.
5. Inspect the install result and report the actual installation path and shortcut. Do not claim preview playback or successful launch unless verified.

Do not install dependencies, change the user's Codex theme, publish updates, send messages, or alter unrelated applications merely because this repository mentions them. The optional theme is a manual user choice.

## Development

- `source/Launcher.cs`: WPF renderer, animation timing, application startup, and window matching.
- `source/build.ps1`: framework compiler invocation.
- `assets/`: animation images and icons.
- `prompts/`: image-generation provenance.
- `docs/preview.mp4` and `docs/cover.png`: public previews.

Keep source and packaged assets consistent. Preserve continuous eye opening, stable hand/ring placement, restrained camera motion, and the separate final parallax layers. Current playback is approximately 9.2 seconds. Onscreen text is limited to CODEX and the skip hint.

Build and inspect the relevant rendered frames after visual changes. Validate that preview mode exits, the normal launcher can locate the installed desktop app, and cleanup is scoped to the project installation manifest. Preserve user files and older versions unless the user asks to replace them.

## Uninstalling

Read `Uninstall.ps1` before running it. Use the same install directory as the original installation. `-RemoveFiles` is explicitly destructive to this project's installed files and must stay constrained by the installation manifest and validated path; never widen cleanup to parent directories.
