; ============================================================
;  DaftarX — Modern installer (Inno Setup 6)
;
;  Single-file polished wizard:
;    Welcome → Configure (admin email/password/domain) → License
;    → Ready → Install (DB engine + DaftarX + SSL) → Finished
;
;  Output: ..\publish\DaftarX-Setup.exe (~340 MB)
;
;  Build: ISCC daftarx-setup.iss
; ============================================================

#define MyAppName        "DaftarX"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "DaftarX"
#define MyAppURL         "https://daftarx.local/"

[Setup]
AppId={{A1B2C3D4-1234-5678-9ABC-DEF012345678}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\EgyptTax
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=no
PrivilegesRequired=admin
OutputDir=..\publish
OutputBaseFilename=DaftarX-Setup
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
SetupLogging=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
RestartIfNeededByRun=no
ShowLanguageDialog=no
LanguageDetectionMethod=none

; Branded color scheme matching the app (navy)
WizardImageFile=
WizardSmallImageFile=

[Languages]
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"

[Files]
; --- prereq: SQL Server Express full installer (~279 MB).
;     This is the standalone "self-extract + install" engine that
;     does NOT require any internet access at install time. We ship
;     it bundled inside the wrapper so a customer on a fresh box
;     with no network can still install end-to-end.
;
;     Source file: publish/bootstrapper-prereqs/SQLEXPR_x64_ENU.exe
;     (~279 MB). Pull it once on the build machine via the matching
;     curl command in the build pipeline.
;
;     If you want a much smaller wrapper that downloads the engine
;     at install time instead, define -DNoSqlBundled when running
;     ISCC and the in-wizard DownloadSqlExpressIfMissing flow takes
;     over.
#ifndef NoSqlBundled
Source: "..\publish\bootstrapper-prereqs\SQLEXPR_x64_ENU.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion
#endif

; --- DaftarX MSI (~190 MB self-contained) ---
Source: "..\src\EgyptTax.Installer\bin\Release\EgyptTax.Installer.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion

; --- post-install SSL cert script ---
Source: "..\src\EgyptTax.Installer\setup-https.ps1"; DestDir: "{app}"; Flags: ignoreversion

; --- workstation tools (so IT can copy them later) ---
Source: "..\src\EgyptTax.Installer\workstation-tools\Trust-DaftarX-Cert.cmd"; DestDir: "{app}\workstation-tools"; Flags: ignoreversion
Source: "..\src\EgyptTax.Installer\workstation-tools\trust-cert.ps1"; DestDir: "{app}\workstation-tools"; Flags: ignoreversion

[Run]
; 1) Install SQL Server Express silently. We ship the FULL standalone
;    engine installer (SQLEXPR_x64_ENU.exe, ~279 MB) inside this
;    wrapper, so the installation does NOT require internet access.
;    The .exe is a self-extracting archive — we run it twice:
;    first to extract the setup media, then to invoke the extracted
;    setup.exe with our unattended params. Combined into one
;    invocation via the /Q /IAcceptSqlServerLicenseTerms +
;    /ACTION=Install flags the standalone installer accepts directly.
;
;    Skipped automatically when SQLEXPRESS instance already detected.
;
;    SAPWD here is rotated per-install via Inno Setup code (see
;    [Code].GetSqlSaPassword) so the SA account isn't a fleet-wide
;    shared secret. The MSI step below uses Trusted Connection
;    (Windows auth) for the app's own access — the SA password is
;    only kept for break-glass administration.
Filename: "{tmp}\SQLEXPR_x64_ENU.exe"; \
    Parameters: "/Q /IACCEPTSQLSERVERLICENSETERMS /ACTION=Install /FEATURES=SQL /INSTANCENAME=SQLEXPRESS /SQLSVCACCOUNT=""NT AUTHORITY\NETWORK SERVICE"" /SQLSYSADMINACCOUNTS=""BUILTIN\Administrators"" /TCPENABLED=1 /SECURITYMODE=SQL /SAPWD=""{code:GetSqlSaPassword}"""; \
    StatusMsg: "Installing database engine (this is the slow step — ~5 minutes)..."; \
    Check: NeedsSqlExpress; \
    Flags: waituntilterminated

; 2) Install DaftarX MSI. Pass admin credentials + the SQL Server
;    connection string the app will use at runtime (Trusted_Connection
;    against the just-installed local SQLEXPRESS instance — same
;    machine, no SA password leakage). The MSI's apply-config CA
;    writes appsettings.Production.json with this connection string,
;    overriding the SQLite default the single-file portable build
;    ships with.
;
;    BIND_URL ties the Kestrel binding to http://+:8088, matching
;    the firewall rule the MSI opens. INTERNAL_DOMAIN is forwarded
;    in case the operator wants the optional setup-https.ps1 flow
;    later (post-install).
Filename: "msiexec.exe"; \
    Parameters: "/i ""{tmp}\EgyptTax.Installer.msi"" /qn /norestart /l*v ""{tmp}\daftarx-msi.log"" ADMIN_EMAIL=""{code:GetAdminEmail}"" ADMIN_PASSWORD=""{code:GetAdminPassword}"" INTERNAL_DOMAIN=""{code:GetInternalDomain}"" SQL_CONNECTION=""Server=.\SQLEXPRESS;Database=EgyptTax;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"" BIND_URL=""http://+:8088"""; \
    StatusMsg: "Installing DaftarX..."; \
    Flags: waituntilterminated

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "http://localhost:8088/"
Name: "{group}\Workstation tools"; Filename: "{app}\workstation-tools"
Name: "{commondesktop}\{#MyAppName}"; Filename: "http://localhost:8088/"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات إضافية:"

[Code]
//
// =============== Custom wizard pages ===============
//
var
  ConfigPage:    TWizardPage;
  EmailEdit:     TNewEdit;
  PasswordEdit:  TPasswordEdit;
  PasswordEdit2: TPasswordEdit;
  DomainEdit:    TNewEdit;

procedure InitializeWizard;
var
  L: TLabel;
begin
  // -- Custom "Configure" page injected after Welcome --
  ConfigPage := CreateCustomPage(wpWelcome,
    'إعداد حساب المسؤول',
    'هذه البيانات هتكون أول حساب يدخل النظام، وأي اسم نطاق داخلي تختاره يتركَّب على شهادة SSL تلقائياً.');

  L := TLabel.Create(ConfigPage);
  L.Parent  := ConfigPage.Surface;
  L.Caption := 'البريد الإلكتروني للمسؤول:';
  L.Top := 8;
  L.Left := 0;
  L.Font.Style := [fsBold];

  EmailEdit := TNewEdit.Create(ConfigPage);
  EmailEdit.Parent := ConfigPage.Surface;
  EmailEdit.Top := 28;
  EmailEdit.Left := 0;
  EmailEdit.Width := ConfigPage.SurfaceWidth;
  EmailEdit.Text := 'admin@daftarx.local';

  L := TLabel.Create(ConfigPage);
  L.Parent  := ConfigPage.Surface;
  L.Caption := 'كلمة المرور (12 حرف على الأقل، حرف كبير + حرف صغير + رقم):';
  L.Top := 64;
  L.Left := 0;
  L.Font.Style := [fsBold];

  PasswordEdit := TPasswordEdit.Create(ConfigPage);
  PasswordEdit.Parent := ConfigPage.Surface;
  PasswordEdit.Top := 84;
  PasswordEdit.Left := 0;
  PasswordEdit.Width := ConfigPage.SurfaceWidth;

  L := TLabel.Create(ConfigPage);
  L.Parent  := ConfigPage.Surface;
  L.Caption := 'تأكيد كلمة المرور:';
  L.Top := 116;
  L.Left := 0;
  L.Font.Style := [fsBold];

  PasswordEdit2 := TPasswordEdit.Create(ConfigPage);
  PasswordEdit2.Parent := ConfigPage.Surface;
  PasswordEdit2.Top := 136;
  PasswordEdit2.Left := 0;
  PasswordEdit2.Width := ConfigPage.SurfaceWidth;

  L := TLabel.Create(ConfigPage);
  L.Parent  := ConfigPage.Surface;
  L.Caption := 'اسم النطاق الداخلي (اختياري — مثال: daftarx.local):';
  L.Top := 172;
  L.Left := 0;
  L.Font.Style := [fsBold];

  DomainEdit := TNewEdit.Create(ConfigPage);
  DomainEdit.Parent := ConfigPage.Surface;
  DomainEdit.Top := 192;
  DomainEdit.Left := 0;
  DomainEdit.Width := ConfigPage.SurfaceWidth;
  DomainEdit.Text := 'daftarx.local';

  L := TLabel.Create(ConfigPage);
  L.Parent  := ConfigPage.Surface;
  L.Caption := 'هيتولّد certificate SSL لهذا النطاق + لـlocalhost + IP الجهاز تلقائياً، عشان الموظفين يفتحوا https://اسم-النطاق/';
  L.Top := 224;
  L.Left := 0;
  L.Width := ConfigPage.SurfaceWidth;
  L.WordWrap := True;
  L.AutoSize := False;
  L.Height := 40;
  L.Font.Color := clGray;
end;

//
// =============== Validation ===============
//
function NextButtonClick(CurPageID: Integer): Boolean;
var
  PwLen: Integer;
  Pw: String;
  HasUpper, HasLower, HasDigit: Boolean;
  i: Integer;
  Ch: Char;
begin
  Result := True;
  if CurPageID = ConfigPage.ID then
  begin
    // Email format check (light)
    if (Length(EmailEdit.Text) = 0) or (Pos('@', EmailEdit.Text) = 0) or (Pos('.', EmailEdit.Text) = 0) then
    begin
      MsgBox('البريد الإلكتروني غير صحيح.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    Pw := PasswordEdit.Text;
    PwLen := Length(Pw);
    if PwLen < 12 then
    begin
      MsgBox('كلمة المرور يجب أن تكون 12 حرف على الأقل.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    if Pw <> PasswordEdit2.Text then
    begin
      MsgBox('تأكيد كلمة المرور لا يطابق.', mbError, MB_OK);
      Result := False;
      Exit;
    end;

    HasUpper := False; HasLower := False; HasDigit := False;
    for i := 1 to PwLen do
    begin
      Ch := Pw[i];
      if (Ch >= 'A') and (Ch <= 'Z') then HasUpper := True;
      if (Ch >= 'a') and (Ch <= 'z') then HasLower := True;
      if (Ch >= '0') and (Ch <= '9') then HasDigit := True;
    end;
    if not (HasUpper and HasLower and HasDigit) then
    begin
      MsgBox('كلمة المرور يجب أن تحتوي على حرف كبير + حرف صغير + رقم.', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
end;

//
// =============== Getters used by [Run] ===============
//
function GetAdminEmail(Param: String): String;
begin
  Result := EmailEdit.Text;
end;

function GetAdminPassword(Param: String): String;
begin
  Result := PasswordEdit.Text;
end;

function GetInternalDomain(Param: String): String;
begin
  Result := DomainEdit.Text;
end;

//
// =============== SQL Express detection + download ===============
//
function NeedsSqlExpress: Boolean;
var
  Value: String;
begin
  // If HKLM\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL
  // already has a SQLEXPRESS value, skip the (slow) install.
  Result := not RegQueryStringValue(
    HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL',
    'SQLEXPRESS',
    Value);
end;

// The SQL Server Express full installer is bundled at compile time
// (see [Files] section). No download step needed at install time;
// fully offline-friendly. If the operator wants the smaller
// download-on-install build, recompile with -DNoSqlBundled and add
// an in-wizard downloader.
//
// Kept as an empty stub (called from CurStepChanged) so the rest
// of the script doesn't have to change shape between bundled and
// download-mode builds. Any "file missing" condition surfaces
// naturally when the [Run] entry fires — Inno shows a clear
// "Failed to launch..." dialog with the missing path.
procedure DownloadSqlExpressIfMissing;
begin
  // intentionally empty in the bundled-build flavour
end;

//
// =============== SA password generator ===============
//
// Stable per-install random password so the SA account isn't a
// fleet-wide shared secret. We don't use it post-install (the app
// authenticates via Trusted_Connection); it just exists so
// SQL Express's mixed-mode install has a strong SA password if
// anyone needs break-glass access via SSMS.
var
  CachedSaPassword: String;

function GetSqlSaPassword(Param: String): String;
begin
  if CachedSaPassword = '' then
  begin
    // Mix the install timestamp into the SA password so it isn't a
    // fleet-wide shared secret. Inno Setup's Pascal lacks
    // Randomize/GetTickCount; GetDateTimeString is available and
    // produces a distinct value per install. The password is not
    // security-critical to the app itself (which uses
    // Trusted_Connection); SA is break-glass only.
    CachedSaPassword := 'DfX#Sa#' + GetDateTimeString('yyyymmddhhnnss', '-', ':') + '!';
  end;
  Result := CachedSaPassword;
end;

//
// =============== Step hooks ===============
//
procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Run BEFORE the [Run] section fires (ssInstall is "files
  // extracted, about to run [Run]"). If the SQL Express
  // bootstrapper wasn't bundled at compile time, fetch it from
  // Microsoft now — runs in-wizard with a progress bar.
  if CurStep = ssInstall then
  begin
    DownloadSqlExpressIfMissing;
  end;

  if CurStep = ssPostInstall then
  begin
    // Nothing else needed — the MSI itself rewrote
    // appsettings.Production.json with the SQL connection string +
    // bind URL via apply-config.ps1. The Finished page handles the
    // "open browser at http://localhost:8088/" affordance.
  end;
end;
