<#
.SYNOPSIS
  Builds the ready-to-run folder for the school computer (Phase 4 M6, ADR 0042).

.DESCRIPTION
  Publishes the server (SmartSchoolTimetable.Api.exe) and the launcher (SmartSchoolTimetable.exe: no console, tray icon, own window) into one
  self-contained win-x64 folder (ReadyToRun, not trimmed; no .NET install needed). The folder is checked to contain no database, log, symbol file,
  test data or user path (a failure stops the script). The publish build runs the
  frontend build, whose output goes to wwwroot. The script then adds «تشغيل البرنامج.bat», which starts the server on
  127.0.0.1 and opens the browser, and «اقرأني.txt» with Arabic instructions.

  Safety: the output goes to a NEW timestamped folder under artifacts\release (ignored by git). The script refuses to
  write into a folder that already exists and deletes nothing.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\Publish-Release.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$scriptFolder = Split-Path -Parent $MyInvocation.MyCommand.Path
$repository = Resolve-Path (Join-Path $scriptFolder "..")
if (-not $OutputRoot) { $OutputRoot = Join-Path $repository "artifacts\release" }
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$target = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) "SmartSchoolTimetable-$stamp"
if (Test-Path -LiteralPath $target) {
    Write-Host "المجلد الناتج موجود من قبل: $target. لم يُكتب أي شيء؛ انتظر ثانية وأعد التشغيل." -ForegroundColor Red
    exit 1
}

function Stop-WithArabicError([string]$message, [string[]]$details = @()) {
    Write-Host ""
    Write-Host "تعذّر إنشاء مجلد التشغيل." -ForegroundColor Red
    Write-Host $message -ForegroundColor Red
    foreach ($line in $details) { Write-Host "  $line" }
    exit 1
}

# A running copy of the app built from this repository holds its files open; publishing then fails on locked files.
$running = Get-CimInstance Win32_Process -Filter "Name='SmartSchoolTimetable.Api.exe' OR Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine -like "*$repository*SmartSchoolTimetable.Api*" -and $_.CommandLine -notlike "*publish*" }
if ($running) {
    Write-Host "تنبيه: البرنامج يعمل الآن من مجلد المصدر (رقم العملية: $(($running.ProcessId) -join '، ')). إذا فشل النشر بسبب ملف مقفل فأوقفه ثم أعد تشغيل هذا السكربت." -ForegroundColor Yellow
}

$project = Join-Path $repository "src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj"
$log = "$target.publish.log"
Write-Host "Publishing to $target (log: $log)"
New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
# The whole output goes to the log; cmd keeps stderr lines (npm, NuGet) as plain text instead of PowerShell errors.
$networkFailure = 'NU1301|NU1101|NU1102|Unable to load the service index|Failed to download|ENOTFOUND|ETIMEDOUT|EAI_AGAIN'
# A full rebuild with no debug information, so no assembly keeps a path of this computer (a reused incremental output could).
cmd /c "dotnet build `"$project`" -c Release -r win-x64 --self-contained true --no-incremental -p:DebugType=None -p:DebugSymbols=false > `"$log`" 2>&1"
$buildExit = $LASTEXITCODE
if ($buildExit -eq 0) { cmd /c "dotnet publish `"$project`" -c Release -r win-x64 --no-build --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -o `"$target`" >> `"$log`" 2>&1" } else { $global:LASTEXITCODE = $buildExit }
$exitCode = $LASTEXITCODE
# The first publish on a computer downloads the win-x64 runtime packs; a transient download failure is retried once.
if ($exitCode -ne 0 -and (Get-Content -LiteralPath $log -Raw) -match $networkFailure) {
    Write-Host "فشل تنزيل بعض الحزم؛ إعادة المحاولة مرة واحدة..." -ForegroundColor Yellow
    cmd /c "dotnet publish `"$project`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -o `"$target`" >> `"$log`" 2>&1"
    $exitCode = $LASTEXITCODE
}
if ($exitCode -ne 0) {
    $errors = @(Select-String -LiteralPath $log -Pattern '(error|خطأ)\s*[A-Z]*\d*\s*:|npm ERR!|ERR_|EBUSY|EPERM' | Select-Object -First 8 | ForEach-Object { $_.Line.Trim() })
    $text = Get-Content -LiteralPath $log -Raw
    $hint = if ($text -match 'being used by another process|EBUSY|EPERM|MSB3021|MSB3027') {
        "السبب: ملف مقفل لأن البرنامج أو عملية بناء أخرى تعمل من مجلد المصدر. أغلق نافذة البرنامج وأي أمر dotnet أو npm يعمل، ثم أعد التشغيل."
    } elseif ($text -match $networkFailure) {
        "السبب: تعذّر تنزيل الحزم من الإنترنت (يحتاج النشر الأول إلى الاتصال لتنزيل حزم التشغيل). تحقق من الاتصال ثم أعد التشغيل."
    } elseif ($text -match 'npm ERR!|MSB3073') {
        "السبب: فشل بناء الواجهة (npm). شغّل npm ci ثم npm run build داخل مجلد frontend لرؤية الخطأ، ثم أعد التشغيل."
    } else {
        "السبب غير معروف. افتح ملف السجل وابحث عن السطر الذي فيه error."
    }
    Stop-WithArabicError "$hint`nملف السجل: $log`nرمز الخروج: $exitCode" $errors
}

$launcherProject = Join-Path $repository "src\SmartSchoolTimetable.Launcher\SmartSchoolTimetable.Launcher.csproj"
cmd /c "dotnet build `"$launcherProject`" -c Release -r win-x64 --self-contained true --no-incremental -p:DebugType=None -p:DebugSymbols=false >> `"$log`" 2>&1"
if ($LASTEXITCODE -eq 0) {
    cmd /c "dotnet publish `"$launcherProject`" -c Release -r win-x64 --no-build --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false -o `"$target`" >> `"$log`" 2>&1"
}
if ($LASTEXITCODE -ne 0) {
    Stop-WithArabicError "فشل نشر مشغّل البرنامج (SmartSchoolTimetable.exe). ملف السجل: $log" @()
}

foreach ($required in @("SmartSchoolTimetable.Api.exe", "SmartSchoolTimetable.exe", "wwwroot\index.html", "google-ortools-native.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $target $required))) {
        Stop-WithArabicError "المجلد الناتج ينقصه الملف $required. ملف السجل: $log"
    }
}

# The licence and (when it exists) the Arabic user guide travel with the program.
Copy-Item -LiteralPath (Join-Path $repository "LICENSE") -Destination (Join-Path $target "LICENSE.txt")
$guide = Join-Path $repository "docs\USER_GUIDE_AR.pdf"
if (Test-Path -LiteralPath $guide) { Copy-Item -LiteralPath $guide -Destination (Join-Path $target "USER_GUIDE_AR.pdf") }

# Clean build: nothing of a computer or of a school may end up in the program (no database, log, symbol file, test data or user path).
$forbiddenNames = '*.db', '*.db-wal', '*.db-shm', '*.sqlite', '*.sqlite3', '*.sqlite-*', '*.log', '*.pdb', 'startup-error.txt', '*.xlsx', 'timetable-backup-*', 'auto-backup-*', 'pre-upgrade-*', 'pre-restore-*'
# (-Include is ignored together with -LiteralPath in Windows PowerShell, so the names are matched explicitly.)
$allFiles = @(Get-ChildItem -LiteralPath $target -Recurse -File)
$found = @($allFiles | Where-Object { $name = $_.Name; $forbiddenNames | Where-Object { $name -like $_ } | Select-Object -First 1 })
$found += @(Get-ChildItem -LiteralPath $target -Recurse -Directory | Where-Object { $_.Name -in 'backups', 'test-results', 'e2e', 'fixtures', 'node_modules' })
# A user path in OUR files (configuration, scripts, styles and our own assemblies); third-party binaries are not scanned.
$userPath = [regex]'(?i)[A-Z]:\\Users\\[^\\\s"]+'
$ours = $allFiles | Where-Object { $_.Extension -in '.json', '.config', '.txt', '.xml', '.js', '.html', '.css' -or $_.Name -like 'SmartSchoolTimetable*.dll' -or $_.Name -like 'SmartSchoolTimetable*.exe' }
foreach ($file in $ours) {
    if ($file.Name -like 'LICENSE*' -or $file.Length -gt 40MB) { continue }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $text = [Text.Encoding]::UTF8.GetString($bytes) + [Text.Encoding]::Unicode.GetString($bytes)
    if ($userPath.IsMatch($text)) { $found += $file }
}
if ($found.Count -gt 0) {
    Stop-WithArabicError "المجلد الناتج يحتوي ملفات لا يجوز أن تُشحن (قاعدة بيانات أو سجل أو رموز تصحيح أو بيانات اختبار أو مسار مستخدم):" @($found | ForEach-Object { $_.FullName.Substring($target.Length) })
}

$size = (Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Done: {0} ({1:N0} MB)" -f $target, ($size / 1MB))
