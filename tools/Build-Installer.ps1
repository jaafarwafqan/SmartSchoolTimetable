<#
.SYNOPSIS
  Builds «الجدول الذكي» Setup.exe (Inno Setup, Arabic, 64-bit Windows 10 only) from a clean published folder (M5, ADR 0052).

.DESCRIPTION
  1. Publishes the program with tools\Publish-Release.ps1 (or uses -SourceDir), which fails when the output holds a database, log, symbol file,
     test data or a user path.
  2. Finds the Inno Setup compiler: artifacts\tools\innosetup (downloaded from NuGet on first use, nothing is installed on the computer) or an installed copy.
  3. Compiles installer\SmartSchoolTimetable.iss into artifacts\installer\SmartSchoolTimetable-Setup-<version>.exe and writes its SHA-256 file.

  Nothing outside the repository is changed and nothing is deleted.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\Build-Installer.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$SourceDir = "",
    # Adds a timestamp to the file name, so repeated test builds never need an old file removed.
    [switch]$Stamp
)

$ErrorActionPreference = "Stop"
$scriptFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
$repository = (Resolve-Path (Join-Path $scriptFolder "..")).Path

function Stop-WithArabicError([string]$message) {
    Write-Host ""
    Write-Host "تعذّر بناء المثبّت." -ForegroundColor Red
    Write-Host $message -ForegroundColor Red
    exit 1
}

# 1. The clean published folder.
if (-not $SourceDir) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptFolder "Publish-Release.ps1")
    if ($LASTEXITCODE -ne 0) { Stop-WithArabicError "فشل نشر البرنامج (انظر الرسالة أعلاه)." }
    $SourceDir = (Get-ChildItem -LiteralPath (Join-Path $repository "artifacts\release") -Directory | Sort-Object Name | Select-Object -Last 1).FullName
}
if (-not (Test-Path -LiteralPath (Join-Path $SourceDir "SmartSchoolTimetable.exe"))) { Stop-WithArabicError "المجلد $SourceDir لا يحتوي SmartSchoolTimetable.exe." }

# 2. The compiler.
$compiler = @(
    (Join-Path $repository "artifacts\tools\innosetup\tools\ISCC.exe"),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    $tools = Join-Path $repository "artifacts\tools"
    New-Item -ItemType Directory -Path $tools -Force | Out-Null
    $package = Join-Path $tools "innosetup.nupkg"
    Write-Host "Downloading the Inno Setup compiler (Tools.InnoSetup 6.4.3 from NuGet) into artifacts\tools ..."
    & curl.exe -sS -L -o $package "https://api.nuget.org/v3-flatcontainer/tools.innosetup/6.4.3/tools.innosetup.6.4.3.nupkg"
    if ($LASTEXITCODE -ne 0) { Stop-WithArabicError "تعذّر تنزيل مترجم Inno Setup (يحتاج أول بناء إلى الاتصال بالإنترنت)." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($package, (Join-Path $tools "innosetup"))
    $compiler = Join-Path $tools "innosetup\tools\ISCC.exe"
}

# 3. The licence page needs a UTF-8 file with a BOM to show Arabic.
$staging = Join-Path $repository "artifacts\installer-staging"
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$license = Join-Path $staging "LICENSE.setup.txt"
[IO.File]::WriteAllText($license, [IO.File]::ReadAllText((Join-Path $repository "LICENSE")), (New-Object System.Text.UTF8Encoding $true))

$outputDir = Join-Path $repository "artifacts\installer"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$suffix = if ($Stamp) { "-" + (Get-Date -Format "yyyyMMdd-HHmmss") } else { "" }
$setup = Join-Path $outputDir "SmartSchoolTimetable-Setup-$Version$suffix.exe"
if (Test-Path -LiteralPath $setup) { Stop-WithArabicError "الملف $setup موجود من قبل؛ لم أستبدله. غيّر الإصدار أو انقل الملف القديم بنفسك." }

Write-Host "Compiling $setup ..."
& $compiler "/DSourceDir=$SourceDir" "/DAppVersion=$Version" "/DOutputBase=SmartSchoolTimetable-Setup-$Version$suffix" "/DLicenseFile=$license" "/DOutputDir=$outputDir" (Join-Path $repository "installer\SmartSchoolTimetable.iss")
if ($LASTEXITCODE -ne 0) { Stop-WithArabicError "فشل ترجمة سكربت المثبّت (انظر الرسالة أعلاه)." }

$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$setup.sha256", "$hash  $(Split-Path -Leaf $setup)`n", (New-Object System.Text.UTF8Encoding $false))
Write-Host ("Done: {0} ({1:N0} MB)" -f $setup, ((Get-Item -LiteralPath $setup).Length / 1MB))
Write-Host "SHA-256: $hash"
