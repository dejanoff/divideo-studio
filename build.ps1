param(
    [string]$Compiler,
    [string]$FfmpegBin
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

# 1. Resolve C# compiler
if (-not $Compiler) {
    if ($env:CSC_PATH -and (Test-Path $env:CSC_PATH)) {
        $Compiler = $env:CSC_PATH
    } elseif (Test-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe') {
        $Compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    } else {
        $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
        if ($cmd) {
            $Compiler = $cmd.Source
        } else {
            throw 'C# compiler (csc.exe) not found. Please install .NET Framework 4.5+ Developer Pack or specify -Compiler.'
        }
    }
}

# 2. Resolve FFmpeg / FFprobe bin directory
if (-not $FfmpegBin) {
    if ($env:FFMPEG_BIN -and (Test-Path (Join-Path $env:FFMPEG_BIN 'ffmpeg.exe'))) {
        $FfmpegBin = $env:FFMPEG_BIN
    } elseif (Test-Path 'C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin\ffmpeg.exe') {
        $FfmpegBin = 'C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin'
    } elseif (Test-Path (Join-Path $PSScriptRoot 'tools\ffmpeg.exe')) {
        $FfmpegBin = Join-Path $PSScriptRoot 'tools'
    } else {
        $cmd = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
        if ($cmd) {
            $candidate = Split-Path $cmd.Source
            if (Test-Path (Join-Path $candidate 'ffprobe.exe')) {
                $FfmpegBin = $candidate
            }
        }
    }
}

if (-not $FfmpegBin -or -not (Test-Path (Join-Path $FfmpegBin 'ffmpeg.exe')) -or -not (Test-Path (Join-Path $FfmpegBin 'ffprobe.exe'))) {
    throw "ffmpeg.exe and ffprobe.exe not found. Specify -FfmpegBin <path> or install ffmpeg (e.g. via choco install ffmpeg)."
}

$divideoPath = Join-Path $PSScriptRoot 'divideo.exe'
if (-not (Test-Path $divideoPath)) {
    throw "divideo.exe not found in $PSScriptRoot"
}

$buildOutput = Join-Path $env:TEMP ('DivideoStudio-build-' + [guid]::NewGuid().ToString('N') + '.exe')
Write-Host "Compiling with $Compiler..."
Write-Host "Bundling FFmpeg from $FfmpegBin..."
Write-Host "Bundling divideo from $divideoPath..."

& $Compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$buildOutput" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll `
    "/resource:$divideoPath,divideo.exe" `
    "/resource:$FfmpegBin\ffmpeg.exe,ffmpeg.exe" `
    "/resource:$FfmpegBin\ffprobe.exe,ffprobe.exe" `
    (Join-Path $PSScriptRoot 'src\DivideoGui.cs') (Join-Path $PSScriptRoot 'src\EditorControls.cs')

if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$targetExe = Join-Path $PSScriptRoot 'DivideoStudio.exe'
Move-Item -LiteralPath $buildOutput -Destination $targetExe -Force
Write-Host "Build succeeded: $targetExe ($("{0:N2} MB" -f ((Get-Item $targetExe).Length / 1MB)))"
