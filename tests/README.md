# DivIDEo Studio Tests

This folder contains automated test tooling for DivIDEo Studio.

## Running Tests

To run the automated self-test suite:

```powershell
powershell -ExecutionPolicy Bypass -File tests/run-tests.ps1
```

Or run directly from the compiled executable:

```powershell
.\DivideoStudio.exe --self-test C:\Path\To\TestOutputFolder
```

The test runner will:
1. Synthesize test video streams using FFmpeg (`testsrc2` + audio `sine`).
2. Test Probe functionality.
3. Test conversion in all 3 geometry modes (Letterbox, Crop/Fill, Stretch).
4. Verify valid `.dvo` container structure, header, packet boundaries, and 512-byte sector alignment.
5. Test encoding of video with no audio track (silence synthesis).
6. Test asynchronous cancellation of active encoding processes.
7. Test GUI controls, timeline dragging, crop calculations, and DVO preview decoding.
8. Output test report (`report.txt`) and pass status.
