param(
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $repoRoot 'DivideoStudio.exe'

if (-not (Test-Path $exe)) {
    Write-Host "DivideoStudio.exe not found. Building first via build.ps1..."
    & (Join-Path $repoRoot 'build.ps1')
}

$cleanup = $false
if (-not $OutputDir) {
    $OutputDir = Join-Path $env:TEMP ("DivideoSelfTest-" + [guid]::NewGuid().ToString('N'))
    $cleanup = $true
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
Write-Host "Running DivIDEo Studio self-test in: $OutputDir"

$p = Start-Process -FilePath $exe -ArgumentList @('--self-test', $OutputDir) -Wait -PassThru

$reportPath = Join-Path $OutputDir 'report.txt'
if (Test-Path $reportPath) {
    Get-Content $reportPath
}

if ($p.ExitCode -ne 0) {
    throw "Self-test failed with exit code $($p.ExitCode)"
}

Write-Host "All tests passed successfully!"
if ($cleanup) {
    try { Remove-Item -LiteralPath $OutputDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
}
