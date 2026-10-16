<#
.SYNOPSIS
  Builds the user guide (docs\USER_GUIDE_AR.pdf and docs\guide\USER_GUIDE_AR.html).

.DESCRIPTION
  1. Starts the PUBLISHED program (the newest folder under artifacts\release, or -ReleaseFolder) on a brand-new temporary database,
     never the owner's own database, on a free local port.
  2. Runs the capture scenario (frontend\guide, fictional school only) that saves the figures to docs\guide\img.
  3. Stops that server (only the process this script started), renders the first pages of the print PDFs as figures,
     then builds the HTML and the PDF of the guide (frontend\guide\build.mjs, Chromium page.pdf, no network).
  The script deletes nothing. The temporary data folder it creates (under %TEMP%, named sst-guide-*) is left for the cleanup list.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\Build-UserGuide.ps1
  powershell -ExecutionPolicy Bypass -File tools\Build-UserGuide.ps1 -SkipCapture   # only rebuild the HTML and the PDF from the saved figures
#>
[CmdletBinding()]
param(
    [string]$ReleaseFolder = "",
    [switch]$SkipCapture
)

$ErrorActionPreference = "Stop"
$scriptFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
$repository = (Resolve-Path (Join-Path $scriptFolder "..")).Path
$frontend = Join-Path $repository "frontend"

if (-not $SkipCapture) {
    if (-not $ReleaseFolder) {
        $newest = Get-ChildItem -LiteralPath (Join-Path $repository "artifacts\release") -Directory | Sort-Object Name | Select-Object -Last 1
        if (-not $newest) { throw "No release folder: run tools\Publish-Release.ps1 first." }
        $ReleaseFolder = $newest.FullName
    }
    $exe = Join-Path $ReleaseFolder "SmartSchoolTimetable.Api.exe"
    if (-not (Test-Path -LiteralPath $exe)) { throw "SmartSchoolTimetable.Api.exe not found in $ReleaseFolder" }

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $data = Join-Path ([IO.Path]::GetTempPath()) ("sst-guide-" + [IO.Path]::GetRandomFileName().Replace(".", ""))
    New-Item -ItemType Directory -Path $data | Out-Null

    $env:Database__Path = Join-Path $data "timetable.db"
    $env:LocalHost__Port = "$port"
    $server = Start-Process -FilePath $exe -WorkingDirectory $ReleaseFolder -PassThru -WindowStyle Hidden
    try {
        $ready = $false
        for ($i = 0; $i -lt 90 -and -not $ready; $i++) {
            Start-Sleep -Seconds 1
            try { $ready = (Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$port/" -TimeoutSec 2).StatusCode -eq 200 } catch { }
        }
        if (-not $ready) { throw "The program did not start on port $port." }
        Write-Host "Capturing from $exe on port $port (data: $data)"
        Push-Location $frontend
        try {
            $env:GUIDE_BASE_URL = "http://127.0.0.1:$port"
            $env:CI = "1"
            & npx playwright test -c playwright.guide.config.ts
            if ($LASTEXITCODE -ne 0) { throw "The capture failed." }
        } finally { Pop-Location }
    } finally {
        if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force }
        Remove-Item Env:\Database__Path, Env:\LocalHost__Port, Env:\GUIDE_BASE_URL -ErrorAction SilentlyContinue
    }
    & python (Join-Path $repository "tools\guide\render_prints.py")
    if ($LASTEXITCODE -ne 0) { throw "Rendering the print figures failed." }
}

Push-Location $frontend
try {
    & node "guide\build.mjs"
    if ($LASTEXITCODE -ne 0) { throw "Building the guide failed." }
} finally { Pop-Location }
Write-Host "Done: docs\USER_GUIDE_AR.pdf"
