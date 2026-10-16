# البناء والتسليم والتصفير

كل الأوامر من PowerShell، من جذر المستودع. المتطلبات على حاسوب التطوير: .NET SDK 9 وNode.js (للواجهة) وPython مع PyMuPDF (لدليل المستخدم فقط). لا يحتاج حاسوب المدرسة إلى شيء منها.

## 1) بناء مجلد التشغيل

```powershell
powershell -ExecutionPolicy Bypass -File tools\Publish-Release.ps1
```

ينشر البرنامج (الخادم والـLauncher) كمجلد مستقل لويندوز 64-بت داخل `artifacts\release\SmartSchoolTimetable-<التاريخ-الوقت>`، وينسخ إليه `USER_GUIDE_AR.pdf` و`LICENSE.txt`. يفشل السكربت إن وجد في المجلد قاعدة بيانات أو سجلات أو اختبارات أو مساراً لمستخدم. لا يكتب فوق مجلد موجود ولا يحذف شيئاً.

## 2) بناء المثبّت

```powershell
powershell -ExecutionPolicy Bypass -File tools\Build-Installer.ps1 -Version 1.0.0
```

- يستدعي النشر من الخطوة 1 بنفسه (أو أضف `-SourceDir <مجلد منشور>` لاستعمال مجلد جاهز).
- يستعمل مترجم Inno Setup الموجود في `artifacts\tools\innosetup` (يُنزَّل مرة واحدة من NuGet ولا يُثبَّت على الحاسوب).
- يرفض الكتابة فوق مثبّت موجود بالاسم نفسه. أضف `-Stamp` ليُضاف التاريخ والوقت إلى اسم الملف.

## 3) أين تجد الناتج

| الناتج | المكان |
|---|---|
| مجلد التشغيل | `artifacts\release\SmartSchoolTimetable-<التاريخ-الوقت>\` (وبجانبه ملف `.publish.log`) |
| المثبّت | `artifacts\installer\SmartSchoolTimetable-Setup-1.0.0.exe` (أو `-1.0.0-<التاريخ-الوقت>.exe` مع `-Stamp`) |
| بصمة المثبّت | الملف نفسه مع اللاحقة `.sha256` |
| دليل المستخدم | `docs\USER_GUIDE_AR.pdf` (يُعاد بناؤه بـ `tools\Build-UserGuide.ps1`) |

المجلدات `artifacts\` غير مخزّنة في git.

## 4) التحقق من SHA-256

```powershell
Get-FileHash .\artifacts\installer\SmartSchoolTimetable-Setup-1.0.0.exe -Algorithm SHA256
Get-Content .\artifacts\installer\SmartSchoolTimetable-Setup-1.0.0.exe.sha256
```

يجب أن تتطابق القيمتان (الحروف الكبيرة والصغيرة سواء). على حاسوب المدرسة نفّذ الأمر الأول على الملف المنسوخ وقارنه بالقيمة المكتوبة في ملاحظات الإصدار.

## 5) ماذا تُسلّم لمدرسة جديدة

1. `SmartSchoolTimetable-Setup-1.0.0.exe` (المثبّت).
2. `USER_GUIDE_AR.pdf` (دليل المستخدم؛ ينسخه المثبّت أيضاً ويضع له اختصاراً في قائمة «ابدأ»).
3. (اختياري) قيمة SHA-256 ليتحقق المستلم من الملف.

لا حاجة إلى الإنترنت ولا إلى تثبيت .NET. إن ظهرت نافذة SmartScreen فالخطوة موضحة في الفصل الأول من الدليل.

## 6) تصفير بيانات البرنامج على حاسوب (بدء من الصفر)

التصفير هنا **نقل لا حذف**: البيانات القديمة تبقى في مجلد أرشيف.

1. أغلق البرنامج كلياً: انقر أيقونته قرب ساعة ويندوز بزر الفأرة الأيمن ← «إيقاف».
2. انقل مجلد `%LOCALAPPDATA%\SmartSchoolTimetable` (فيه `timetable.db` ومجلد `backups`) إلى مجلد أرشيف باسم يحمل التاريخ:

```powershell
$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$archive = Join-Path $env:USERPROFILE "Documents\SmartSchoolTimetable\Archive\pre-v1.0-$stamp"
New-Item -ItemType Directory -Path $archive | Out-Null
Move-Item -LiteralPath "$env:LOCALAPPDATA\SmartSchoolTimetable\*" -Destination $archive
```

3. تحقق من أن عدد الملفات وأحجامها في الأرشيف مطابقة لما كان في المجلد الأصلي، ثم افتح البرنامج: تظهر شاشة «إعداد حساب المالك» كأول تشغيل.

نسخك الاحتياطية في المجلد الذي اخترته من «الإعدادات ← النسخ الاحتياطي» لا تتأثر.
