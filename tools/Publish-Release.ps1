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
    throw "The output folder already exists: $target. Nothing was written."
}

$project = Join-Path $repository "src\SmartSchoolTimetable.Api\SmartSchoolTimetable.Api.csproj"
Write-Host "Publishing to $target"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $target
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

foreach ($required in @("SmartSchoolTimetable.Api.exe", "wwwroot\index.html", "google-ortools-native.dll")) {
    if (-not (Test-Path -LiteralPath (Join-Path $target $required))) { throw "The published folder is missing $required." }
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
start "" cmd /c "timeout /t 4 /nobreak >nul & start http://127.0.0.1:5080/"
SmartSchoolTimetable.Api.exe
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
