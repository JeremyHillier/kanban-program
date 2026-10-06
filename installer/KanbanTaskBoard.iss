#ifndef Channel
  #define Channel "Production"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#if Channel == "Test"
  #define MyAppName "Kanban Task Board (Test)"
  #define MyAppId "18423CD5-03AE-4CED-8914-EFD56BF5CF28"
  #define DataFolderName "KanbanApp.Test"
  #define RunValueSuffix "-Test"
#else
  #define MyAppName "Kanban Task Board"
  #define MyAppId "4172C30A-F96E-4741-B3A1-B16770195903"
  #define DataFolderName "KanbanApp"
  #define RunValueSuffix ""
#endif

#define MyAppPublisher "Jeremy Hillier Consulting Inc"
#define MyAppExeName "KanbanApp.exe"

[Setup]
AppId={{{#MyAppId}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userpf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
; No AppMutex: Setup handles a running copy itself (PrepareToInstall below) - it offers to close the app,
; which refuses while a task is being edited. CloseApplications stays as the fallback for anything else.
CloseApplications=yes
RestartApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
; Signed by installer\build-installers.ps1 when installer\signing.json exists (see installer\Signing.ps1): the
; build defines Signed and the "azuresign" tool, and Inno Setup signs this setup and its uninstaller.
#ifdef Signed
SignTool=azuresign
SignedUninstaller=yes
#endif
OutputDir=Output
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\KanbanApp\Assets\app.ico
; The licence agreement the customer accepts before installing. The same file is inside the exe
; (About > Licence Agreement), so there is one text to maintain.
LicenseFile=..\src\KanbanApp\Legal\EULA.txt
WizardImageFile=WizardImage.bmp
WizardSmallImageFile=WizardSmallImage.bmp

[Files]
Source: "..\publish\{#Channel}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; The release notes, rendered from CHANGELOG.md by render-whats-new.ps1 during the build. Carried
; inside the setup and read by [Code] below; never installed.
Source: "Output\WhatsNew.txt"; Flags: dontcopy

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

; "Start when Windows starts" (Settings) writes this value; Setup creates nothing here, but uninstalling removes it.
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "KanbanTaskBoard{#RunValueSuffix}"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--seed-data-folder ""{code:GetDataFolder}"""; Flags: runhidden nowait skipifsilent; StatusMsg: "Setting up data storage..."

[Code]
var
  DataDirPage: TInputDirWizardPage;
  PreviousVersion: String;
  IsUpdate: Boolean;

function GetInstalledVersion(): String;
var
  UninstallKey: String;
begin
  Result := '';
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{#MyAppId}}_is1';
  // Check both hives: a per-user install (the normal case here) registers under HKCU, but fall
  // back to HKLM in case a previous version was ever installed elevated.
  if RegQueryStringValue(HKCU, UninstallKey, 'DisplayVersion', Result) then exit;
  RegQueryStringValue(HKLM, UninstallKey, 'DisplayVersion', Result);
end;

{ "0.127.1" against "0.120.2", part by part: 1 when the first is newer, -1 when older, 0 when
  the same. A part that is not a number counts as 0, so an odd DisplayVersion still compares. }
function CompareVersions(const A, B: String): Integer;
var
  PartA, PartB: Integer;
  RestA, RestB: String;
  DotA, DotB: Integer;
begin
  Result := 0;
  RestA := A;
  RestB := B;
  while (RestA <> '') or (RestB <> '') do
  begin
    DotA := Pos('.', RestA);
    if DotA > 0 then begin PartA := StrToIntDef(Copy(RestA, 1, DotA - 1), 0); Delete(RestA, 1, DotA); end
    else begin PartA := StrToIntDef(RestA, 0); RestA := ''; end;
    DotB := Pos('.', RestB);
    if DotB > 0 then begin PartB := StrToIntDef(Copy(RestB, 1, DotB - 1), 0); Delete(RestB, 1, DotB); end
    else begin PartB := StrToIntDef(RestB, 0); RestB := ''; end;
    if PartA > PartB then begin Result := 1; exit; end;
    if PartA < PartB then begin Result := -1; exit; end;
  end;
end;

{ Every version newer than the one installed, as WhatsNew.txt lays them out: a "#version" marker
  line starts each version, and what follows it is shown as written. The list is newest first, so
  it stops at the first version that is not newer: the 1.0.0-1.2.0 entries at the very end predate
  the move back to 0.x numbering and would otherwise count as newer than everything. Empty when
  there is nothing newer to tell, or the file could not be read. }
function WhatsNewSince(const InstalledVersion: String): String;
var
  Lines: TArrayOfString;
  I: Integer;
begin
  Result := '';
  ExtractTemporaryFile('WhatsNew.txt');
  if not LoadStringsFromFile(ExpandConstant('{tmp}\WhatsNew.txt'), Lines) then exit;
  for I := 0 to GetArrayLength(Lines) - 1 do
  begin
    if Copy(Lines[I], 1, 1) = '#' then
    begin
      if CompareVersions(Copy(Lines[I], 2, Length(Lines[I]) - 1), InstalledVersion) <= 0 then break;
    end
    else
      Result := Result + Lines[I] + #13#10;
  end;
  Result := Trim(Result);
end;

procedure InitializeWizard;
var
  Notes: String;
begin
  PreviousVersion := GetInstalledVersion();
  IsUpdate := PreviousVersion <> '';

  { What changed between the version on this machine and this one, on its own page after Welcome.
    Only on an update: a first install has nothing to compare against, and the app's own What's New
    screen opens after an update anyway. }
  if IsUpdate then
  begin
    Notes := WhatsNewSince(PreviousVersion);
    if Notes <> '' then
      CreateOutputMsgMemoPage(wpWelcome, 'What''s New',
        'Changes since version ' + PreviousVersion,
        'This update brings {#MyAppName} to version {#MyAppVersion}. Here is what has changed:',
        Notes);
  end;

  if IsUpdate then
    WizardForm.WelcomeLabel2.Caption :=
      'Setup will update {#MyAppName} from version ' + PreviousVersion + ' to version {#MyAppVersion}.' + #13#10#13#10 +
      'It is recommended that you close all other applications before continuing.'
  else
    WizardForm.WelcomeLabel2.Caption :=
      'Setup will install {#MyAppName} version {#MyAppVersion} on your computer.' + #13#10#13#10 +
      'It is recommended that you close all other applications before continuing.';

  DataDirPage := CreateInputDirPage(wpSelectDir,
    'Select Task Data Location', 'Where should {#MyAppName} store your task data?',
    'Setup will store your task database, and any screenshots you attach to tasks, in the folder below.' + #13#10 +
    'You can change this later from within the app (Settings > Data Storage).',
    False, '');
  DataDirPage.Add('');
  DataDirPage.Values[0] := ExpandConstant('{localappdata}\{#DataFolderName}');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if (DataDirPage <> nil) and (PageID = DataDirPage.ID) then
    Result := FileExists(ExpandConstant('{localappdata}\{#DataFolderName}\config.json'));
end;

function GetDataFolder(Param: string): string;
begin
  Result := DataDirPage.Values[0];
end;


// ----- A running copy of the app -----
// Checked once the user has clicked Install. The app is asked to close through a window message it
// registers under the same name (MainWindow.SetupClose.cs); it answers 1 when it is closing, 2 when
// a task is being edited and it will not. WM_CLOSE would not do: a modal task window would swallow it.
const
  MutexName = 'KanbanTaskBoard-{#Channel}';
  CloseMessageName = 'KanbanTaskBoard.CloseForSetup';
  AppClosing = 1;
  AppRefused = 2;

function RegisterWindowMessage(lpString: String): UINT;
  external 'RegisterWindowMessageW@user32.dll stdcall';

function AppHasExited(): Boolean;
var
  Tries: Integer;
begin
  // Up to 20 seconds: the app backs the task file up as it closes.
  Tries := 0;
  while CheckForMutexes(MutexName) and (Tries < 80) do
  begin
    Sleep(250);
    Tries := Tries + 1;
  end;
  Result := not CheckForMutexes(MutexName);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  MainWindow: HWND;
  Reply: LongInt;
  CloseMessage: UINT;
begin
  Result := '';
  CloseMessage := RegisterWindowMessage(CloseMessageName);

  while CheckForMutexes(MutexName) do
  begin
    if MsgBox('{#MyAppName} is running.' + #13#10#13#10 +
              'Setup can close it for you and then continue. Your tasks are saved as you go, so nothing is lost.' + #13#10#13#10 +
              'Close {#MyAppName} and continue?', mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := 'Setup cannot update {#MyAppName} while it is running. Close it, then run Setup again.';
      exit;
    end;

    MainWindow := FindWindowByWindowName('{#MyAppName}');
    Reply := 0;
    if MainWindow <> 0 then
      Reply := SendMessage(MainWindow, CloseMessage, 0, 0);

    if Reply = AppRefused then
    begin
      MsgBox('A task is open in {#MyAppName}.' + #13#10#13#10 +
             'Save or cancel it, then click OK to try again.', mbInformation, MB_OK);
      continue;
    end;

    if (Reply <> AppClosing) or not AppHasExited() then
      MsgBox('{#MyAppName} did not close.' + #13#10#13#10 +
             'Close it yourself, then click OK to try again.', mbInformation, MB_OK);
  end;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox('{#MyAppName} has been removed.' + #13#10#13#10 +
      'Your task data was left in place at:' + #13#10 +
      ExpandConstant('{localappdata}\{#DataFolderName}') + #13#10#13#10 +
      'Delete that folder yourself if you no longer need it.',
      mbInformation, MB_OK);
  end;
end;
