# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-01

### Added
- **Initial Public Release of DivIDEo Studio**.
- Standalone portable Windows x64 GUI converter for ZX Spectrum 128K + DivIDE video.
- Embedded toolchain bundling FFmpeg 7.0.2, FFprobe, and the original 2010 `divideo` encoder (extracts automatically on first run to `%TEMP%`).
- Interactive video fragment selector:
  - Dual-track timeline with seek cursor and draggable start/end handles.
  - Frame navigation via keyboard arrows (0.04s / 1 frame step).
  - Flexible timecode input supporting `hh:mm:ss.fff` and `mm:ss`.
  - Duration and frame-limit constraints.
- Geometry & aspect ratio editor:
  - Pixel-accurate cropping of source margins with live darkening overlay.
  - 4:3 scaling modes: *Letterbox (Fit with black bars)*, *Crop to Fill*, and *Stretch*.
  - Visual preview of crop and 4:3 framing results.
- ZX Spectrum image adjustments:
  - Colour and black-and-white (mono) modes.
  - Ordered dithering algorithms (4×4, 2×2, or disabled).
  - Configurable border color (Spectrum colors 0–7).
  - Contrast and brightness fine-tuning.
  - ASCII title tag for the Spectrum on-screen menu (up to 32 characters).
  - Optional subtitle overlay supporting Cyrillic (Unicode) and Latin characters.
- Audio pipeline:
  - Multi-track audio detection and stream selector.
  - Automatic downmixing to 44.1 kHz mono PCM.
  - Volume scaling multiplier.
  - Mute / silence track synthesis.
- Software ZX Spectrum VRAM screen decoder:
  - Emulates 256×192 bitmap layout and 32×24 attribute grid.
  - Real-time color clash and dithering preview before full export.
- DVO container validation and safety:
  - Automatic 512-byte sector padding for IDE streaming.
  - Container header, packet boundaries, and frame marker integrity checks.
  - Atomic output file replacement via staging files.
  - Graceful cancellation support for active FFmpeg / encoder processes.
- Automated self-test suite (`--self-test` CLI flag and `tests/run-tests.ps1` script).
- GitHub Actions CI workflow for build and self-test verification.

### System Requirements
- OS: Windows 7 / 8 / 10 / 11 (64-bit).
- Runtime: .NET Framework 4.5 or newer (pre-installed on Windows 10/11).
- Target Hardware: ZX Spectrum 128K with DivIDE interface (DivMMC is not supported by the original player).
- Media: Non-fragmented `.dvo` file on FAT-formatted storage media (CF card / IDE drive).

### Known Limitations
- The encoding process creates an uncompressed intermediate `rawvideo` file in `%TEMP%` (~225 MB per minute of selected video). Ensure sufficient temporary disk space for long clips.
- DivMMC interfaces are not compatible due to the original player's direct IDE port access implementation.
