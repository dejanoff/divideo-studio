# Contributing to DivIDEo Studio

Thank you for your interest in contributing to DivIDEo Studio! We welcome bug reports, feature suggestions, documentation improvements, and code contributions.

## How to Report Bugs

When submitting a bug report via GitHub Issues, please include:
- Windows version (e.g., Windows 11 23H2).
- Input video details (container format, resolution, fps, audio format).
- Reproduction steps.
- Output from the application log box or console error message.
- Screenshots if relevant.

## Development Setup

### Prerequisites
- Windows 10/11 x64
- .NET Framework 4.5+ Developer Pack (or `csc.exe` in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319`)
- FFmpeg and FFprobe binaries (can be installed via `choco install ffmpeg` or `winget install Gyan.FFmpeg`)
- PowerShell 5.1+ or PowerShell 7+

### Building the Project

Run `build.ps1` from PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

If your FFmpeg installation is in a custom path:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -FfmpegBin "C:\Tools\ffmpeg\bin"
```

The build script compiles `src/DivideoGui.cs` and `src/EditorControls.cs`, embeds the required binaries (`ffmpeg.exe`, `ffprobe.exe`, and `divideo.exe`) as resources, and outputs `DivideoStudio.exe`.

### Running Tests

Before submitting changes, run the automated self-test suite:

```powershell
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1
```

The test runner synthesizes test videos, verifies all 3 geometry modes, checks audio/silence paths, validates DVO packet boundaries, and tests the UI components and frame decoding.

## Pull Request Guidelines

1. Fork the repository and create your feature branch: `git checkout -b feature/my-feature`.
2. Keep changes focused and atomic.
3. Preserve backward compatibility and ensure `tests/run-tests.ps1` passes cleanly.
4. Update documentation (`README.md`, `ИНСТРУКЦИЯ.md`, `CHANGELOG.md`) if UI or behavioral changes were made.
5. Submit a Pull Request targeting `main`.

## License

By contributing to DivIDEo Studio, you agree that your contributions will be licensed under the [GNU General Public License v3.0](LICENSE).
