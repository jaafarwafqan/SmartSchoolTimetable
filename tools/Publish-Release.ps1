<#
.SYNOPSIS
  Builds the ready-to-run folder for the school computer (Phase 4 M6, ADR 0042).

.DESCRIPTION
  Publishes the API as a self-contained win-x64 application (no .NET install needed). The publish build runs the
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
cmd /c "dotnet publish `"$project`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o `"$target`" > `"$log`" 2>&1"
$exitCode = $LASTEXITCODE
# The first publish on a computer downloads the win-x64 runtime packs; a transient download failure is retried once.
if ($exitCode -ne 0 -and (Get-Content -LiteralPath $log -Raw) -match $networkFailure) {
    Write-Host "فشل تنزيل بعض الحزم؛ إعادة المحاولة مرة واحدة..." -ForegroundColor Yellow
    cmd /c "dotnet publish `"$project`" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o `"$target`" >> `"$log`" 2>&1"
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

foreach ($required in @("SmartSchoolTimetable.Api.exe", "wwwroot\index.html", "google-ortools-native.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $target $required))) {
        Stop-WithArabicError "المجلد الناتج ينقصه الملف $required. ملف السجل: $log"
    }
}

$utf8 = New-Object System.Text.UTF8Encoding $false
$utf8Bom = New-Object System.Text.UTF8Encoding $true

# The launcher: the server window stays open (closing it stops the program); the browser opens after a short wait.
$launcher = @"
@echo off
chcp 65001 >nul
cd /d "%~dp0"
title برنامج الجدول المدرسي
echo يعمل البرنامج الآن على هذا الحاسوب فقط. لا تغلق هذه النافذة أثناء العمل.
echo لإيقاف البرنامج أغلق هذه النافذة.
if not defined SST_PORT set "SST_PORT=5080"
if not defined SST_NO_BROWSER start "" cmd /c "timeout /t 4 /nobreak >nul & start http://127.0.0.1:%SST_PORT%/"
"%~dp0SmartSchoolTimetable.Api.exe" --LocalHost:Port=%SST_PORT% %*
if errorlevel 1 (
  echo.
  echo تعذّر تشغيل البرنامج أو توقف بخطأ.
  echo إذا كان البرنامج يعمل في نافذة أخرى فاستخدمها، أو أغلقها ثم شغّل هذا الملف من جديد.
  pause
)
"@
[IO.File]::WriteAllText((Join-Path $target "تشغيل البرنامج.bat"), $launcher.Replace("`r`n", "`n").Replace("`n", "`r`n"), $utf8)

$readme = @"
برنامج الجدول المدرسي — تعليمات التشغيل
=========================================

التشغيل
- انقر نقراً مزدوجاً على «تشغيل البرنامج.bat».
- تفتح نافذة سوداء (هي الخادم المحلي)، ثم يفتح المتصفح على العنوان http://127.0.0.1:5080/ بعد ثوانٍ.
- لا تغلق النافذة السوداء أثناء العمل. إغلاقها يوقف البرنامج.
- يعمل البرنامج على هذا الحاسوب فقط، ولا يحتاج إلى الإنترنت.

أول تشغيل
- تظهر شاشة إنشاء حساب المالك: اختر اسم مستخدم وكلمة مرور قوية.
- يظهر رمز الاسترداد مرة واحدة: احفظه في مكان آمن (يلزم إذا نُسيت كلمة المرور).
- ثم يبدأ معالج الإعداد: المدرسة، السنة الدراسية، الدوام، الصفوف والشعب، المواد والمنهج، المعلمون، الأنصبة.

أين تُحفظ البيانات
- قاعدة البيانات في مجلد المستخدم: %LOCALAPPDATA%\SmartSchoolTimetable\timetable.db
- خذ نسخة احتياطية بانتظام من: الإعدادات ← عام ← «النسخ الاحتياطي والاستعادة».
- عند الاستعادة يحفظ البرنامج نسخة تلقائية من البيانات الحالية في المجلد backups بجوار قاعدة البيانات.

إذا لم يفتح المتصفح
- افتح المتصفح يدوياً واكتب العنوان: http://127.0.0.1:5080/
- إذا ظهرت رسالة أن المنفذ مستخدم فالبرنامج يعمل في نافذة أخرى: استخدمها أو أغلقها ثم أعد التشغيل.

الدليل الكامل: USER_GUIDE_AR.md في مجلد docs من حزمة المصدر.
"@
[IO.File]::WriteAllText((Join-Path $target "اقرأني.txt"), $readme.Replace("`r`n", "`n").Replace("`n", "`r`n"), $utf8Bom)

$size = (Get-ChildItem -LiteralPath $target -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Done: {0} ({1:N0} MB)" -f $target, ($size / 1MB))
