; Inno Setup script of «الجدول الذكي» (M5, ADR 0052). Built by tools/Build-Installer.ps1:
;   ISCC /DSourceDir=<published folder> /DAppVersion=1.1.0 /DLicenseFile=<license with BOM> /DOutputDir=<folder> installer\SmartSchoolTimetable.iss
; Rules: 64-bit Windows 10 (1809 or newer) only; installs to Program Files; shortcuts on the desktop and in the Start menu; the program's data
; (database, backups) lives in the user's profile and is NEVER touched by an upgrade or by the uninstaller.

#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef SourceDir
  #error SourceDir (the published folder) is required.
#endif
#ifndef OutputBase
  #define OutputBase "SmartSchoolTimetable-Setup-" + AppVersion
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
#ifndef LicenseFile
  #define LicenseFile "..\LICENSE"
#endif

[Setup]
AppId={{6F1D2A40-7B35-4C8E-9A52-3E0C4B7D18A9}
AppName=الجدول الذكي
AppVersion={#AppVersion}
AppVerName=الجدول الذكي {#AppVersion}
AppPublisher=جعفر وفقان
VersionInfoVersion={#AppVersion}
VersionInfoProductName=الجدول الذكي
VersionInfoDescription=الجدول الذكي - برنامج الجداول المدرسية
VersionInfoCopyright=جميع الحقوق محفوظة © 2026 جعفر وفقان
DefaultDirName={autopf}\SmartSchoolTimetable
DefaultGroupName=الجدول الذكي
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBase}
SetupIconFile=..\src\SmartSchoolTimetable.Launcher\app.ico
UninstallDisplayIcon={app}\SmartSchoolTimetable.exe
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
LicenseFile={#LicenseFile}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; 64-bit Windows 10 only (1809 = 10.0.17763); a 32-bit Windows is refused with an Arabic message in InitializeSetup.
MinVersion=10.0.17763
ArchitecturesInstallIn64BitMode=x64os
PrivilegesRequired=admin
; Normal installs are for all users (admin). /CURRENTUSER on the command line installs for the current user only (used by tests and PCs without an administrator).
PrivilegesRequiredOverridesAllowed=commandline
; A running copy must be closed first (the launcher holds this mutex); an upgrade then replaces the files in place.
AppMutex=SmartSchoolTimetable.Launcher
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UninstallDisplayName=الجدول الذكي
ShowLanguageDialog=no

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[CustomMessages]
arabic.Needs64Bit=هذا البرنامج يحتاج ويندوز 64-بت.%nلا يمكن تثبيته على ويندوز 32-بت.
arabic.LowMemory=ذاكرة هذا الحاسوب أقل من 4 جيجابايت.%nيعمل البرنامج لكنه سيكون أبطأ عند توليد الجداول الكبيرة. هل تريد المتابعة؟
arabic.TaskDesktop=إنشاء اختصار على سطح المكتب
arabic.TaskStartup=تشغيل البرنامج تلقائياً عند بدء ويندوز (يعمل في الخلفية دون فتح نافذة)
arabic.RunNow=تشغيل الجدول الذكي الآن
arabic.Guide=دليل المستخدم
arabic.UninstallDone=تم إلغاء تثبيت البرنامج.%n%nبياناتك ونسخك الاحتياطية لم تُحذف؛ هي محفوظة في مجلد بيانات المستخدم (%LOCALAPPDATA%\SmartSchoolTimetable) وفي مجلد النسخ الاحتياطية الذي اخترته.

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:TaskStartup}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\الجدول الذكي"; Filename: "{app}\SmartSchoolTimetable.exe"
Name: "{group}\{cm:Guide}"; Filename: "{app}\USER_GUIDE_AR.pdf"; Check: GuideExists
Name: "{autodesktop}\الجدول الذكي"; Filename: "{app}\SmartSchoolTimetable.exe"; Tasks: desktopicon

[Registry]
; Off by default. The program starts in the tray (--tray) and opens its window only when the user asks.
Root: HKA; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SmartSchoolTimetable"; \
  ValueData: """{app}\SmartSchoolTimetable.exe"" --tray"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\SmartSchoolTimetable.exe"; Description: "{cm:RunNow}"; Flags: nowait postinstall skipifsilent

; Deliberately NO [UninstallDelete] for data: the database and the backups are the school's and are never removed by this installer.

[Code]
type
  TMemoryStatusEx = record
    Length: Cardinal;
    MemoryLoad: Cardinal;
    TotalPhys: Int64;
    AvailPhys: Int64;
    TotalPageFile: Int64;
    AvailPageFile: Int64;
    TotalVirtual: Int64;
    AvailVirtual: Int64;
    AvailExtendedVirtual: Int64;
  end;

function GlobalMemoryStatusEx(var Buffer: TMemoryStatusEx): Boolean;
  external 'GlobalMemoryStatusEx@kernel32.dll stdcall';

function GuideExists: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\USER_GUIDE_AR.pdf'));
end;

function InitializeSetup: Boolean;
var
  Status: TMemoryStatusEx;
begin
  Result := True;
  if not IsWin64 then
  begin
    MsgBox(CustomMessage('Needs64Bit'), mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;
  // A 4 GB computer reports about 3.5 to 3.9 GB of usable memory: warn only below 3.4 GB (so a real 4 GB machine is not warned).
  Status.Length := SizeOf(Status);
  if GlobalMemoryStatusEx(Status) then
    if Status.TotalPhys < Int64(3400) * 1024 * 1024 then
      if not WizardSilent then
        Result := MsgBox(CustomMessage('LowMemory'), mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    MsgBox(CustomMessage('UninstallDone'), mbInformation, MB_OK);
end;
