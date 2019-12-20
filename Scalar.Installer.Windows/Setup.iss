; This script requires Inno Setup Compiler 5.5.9 or later to compile
; The Inno Setup Compiler (and IDE) can be found at http://www.jrsoftware.org/isinfo.php

; General documentation on how to use InnoSetup scripts: http://www.jrsoftware.org/ishelp/index.php

#define MyAppName "Scalar"
#define MyAppPublisher "Microsoft Corporation"
#define MyAppPublisherURL "http://www.microsoft.com"
#define MyAppURL "https://github.com/microsoft/Scalar"
#define MyAppExeName "Scalar.exe"
#define ServiceUIName "Scalar"
#define EnvironmentKey "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"

[Setup]
AppId={{82F731CB-1CFC-406D-8D84-8467BF6040C7}
AppName={#MyAppName}
AppVersion={#ScalarVersion}
VersionInfoVersion={#ScalarVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright © Microsoft 2019
BackColor=clWhite
BackSolid=yes
DefaultDirName={pf}\{#MyAppName}
OutputBaseFilename=SetupScalar.{#ScalarVersion}
OutputDir=Setup
Compression=lzma2
InternalCompressLevel=ultra64
SolidCompression=yes
MinVersion=10.0.14374
DisableDirPage=yes
DisableReadyPage=yes
SetupIconFile="{#LayoutPath}\Images\scalar.ico"
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
WizardImageStretch=no
WindowResizable=no
CloseApplications=yes
ChangesEnvironment=yes
RestartIfNeededByRun=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl";

[Types]
Name: "full"; Description: "Full installation"; Flags: iscustom;

[Components]

[InstallDelete]
; Delete old dependencies from VS 2015 VC redistributables
Type: files; Name: "{app}\ucrtbase.dll"

[Files]
; Include all files (recursively) from the layout directory
DestDir: "{app}"; Flags: ignoreversion recursesubdirs; Source:"{#LayoutPath}\*";

[Icons]
Name: "{commonstartmenu}\{#ServiceUIName}"; Filename: "{app}\Scalar.Service.UI.exe"; AppUserModelID: "Scalar"

[UninstallDelete]
; Deletes the entire installation directory, including files and subdirectories
Type: filesandordirs; Name: "{app}";
Type: filesandordirs; Name: "{commonappdata}\Scalar\Scalar.Upgrade";

[Registry]
Root: HKLM; Subkey: "{#EnvironmentKey}"; \
    ValueType: expandsz; ValueName: "PATH"; ValueData: "{olddata};{app}"; \
    Check: NeedsAddPath(ExpandConstant('{app}'))

[Code]
var
  ExitCode: Integer;

function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    '{#EnvironmentKey}',
    'PATH', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  // look for the path with leading and trailing semicolon
  // Pos() returns 0 if not found
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

procedure RemovePath(Path: string);
var
  Paths: string;
  PathMatchIndex: Integer;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, '{#EnvironmentKey}', 'Path', Paths) then
    begin
      Log('PATH not found');
    end
  else
    begin
      Log(Format('PATH is [%s]', [Paths]));

      PathMatchIndex := Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';');
      if PathMatchIndex = 0 then
        begin
          Log(Format('Path [%s] not found in PATH', [Path]));
        end
      else
        begin
          Delete(Paths, PathMatchIndex - 1, Length(Path) + 1);
          Log(Format('Path [%s] removed from PATH => [%s]', [Path, Paths]));

          if RegWriteStringValue(HKEY_LOCAL_MACHINE, '{#EnvironmentKey}', 'Path', Paths) then
            begin
              Log('PATH written');
            end
          else
            begin
              Log('Error writing PATH');
            end;
        end;
    end;
end;

procedure StopService(ServiceName: string);
var
  ResultCode: integer;
begin
  Log('StopService: stopping: ' + ServiceName);
  // ErrorCode 1060 means service not installed, 1062 means service not started
  if not Exec(ExpandConstant('{sys}\SC.EXE'), 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode <> 1060) and (ResultCode <> 1062) then
    begin
      RaiseException('Fatal: Could not stop service: ' + ServiceName);
    end;
end;

procedure UninstallService(ServiceName: string; ShowProgress: boolean);
var
  ResultCode: integer;
begin
  if Exec(ExpandConstant('{sys}\SC.EXE'), 'query ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode <> 1060) then
    begin
      Log('UninstallService: uninstalling service: ' + ServiceName);
      if (ShowProgress) then
        begin
          WizardForm.StatusLabel.Caption := 'Uninstalling service: ' + ServiceName;
          WizardForm.ProgressGauge.Style := npbstMarquee;
        end;

      try
        StopService(ServiceName);

        if not Exec(ExpandConstant('{sys}\SC.EXE'), 'delete ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
          begin
            Log('UninstallService: Could not uninstall service: ' + ServiceName);
            RaiseException('Fatal: Could not uninstall service: ' + ServiceName);
          end;

        if (ShowProgress) then
          begin
            WizardForm.StatusLabel.Caption := 'Waiting for pending ' + ServiceName + ' deletion to complete. This may take a while.';
          end;

      finally
        if (ShowProgress) then
          begin
            WizardForm.ProgressGauge.Style := npbstNormal;
          end;
      end;

    end;
end;

procedure InstallScalarService();
var
  ResultCode: integer;
  StatusText: string;
  InstallSuccessful: Boolean;
begin
  InstallSuccessful := False;

  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Installing Scalar.Service.';
  WizardForm.ProgressGauge.Style := npbstMarquee;

  try
    if Exec(ExpandConstant('{sys}\SC.EXE'), ExpandConstant('create Scalar.Service binPath="{app}\Scalar.Service.exe" start=auto'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
      begin
        if Exec(ExpandConstant('{sys}\SC.EXE'), 'failure Scalar.Service reset= 30 actions= restart/10/restart/5000//1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
          begin
            if Exec(ExpandConstant('{sys}\SC.EXE'), 'start Scalar.Service', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
              begin
                InstallSuccessful := True;
              end;
          end;
      end;

  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;

  if InstallSuccessful = False then
    begin
      RaiseException('Fatal: An error occured while installing Scalar.Service.');
    end;
end;

procedure StartScalarServiceUI();
var
  ResultCode: integer;
begin
  if ExecAsOriginalUser(ExpandConstant('{app}\Scalar.Service.UI.exe'), '', '', SW_HIDE, ewNoWait, ResultCode) then
    begin
      Log('StartGVFSServiceUI: Successfully launched Scalar.Service.UI');
    end
  else
    begin
      Log('StartGVFSServiceUI: Failed to launch Scalar.Service.UI');
    end;
end;

procedure StopScalarServiceUI();
var
  ResultCode: integer;
begin
  if Exec('powershell.exe', '-NoProfile "Stop-Process -Name Scalar.Service.UI"', '', SW_HIDE, ewNoWait, ResultCode) then
    begin
      Log('StopGVFSServiceUI: Successfully stopped Scalar.Service.UI');
    end
  else
    begin
      RaiseException('Fatal: Could not stop process: Scalar.Service.UI');
    end;
end;

function IsScalarRunning(): Boolean;
var
  ResultCode: integer;
begin
  if Exec('powershell.exe', '-NoProfile "Get-Process scalar | foreach {exit 10}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if ResultCode = 10 then
        begin
          Result := True;
        end;
      if ResultCode = 1 then
        begin
          Result := False;
        end;
    end;
end;

function ExecWithResult(Filename, Params, WorkingDir: String; ShowCmd: Integer;
  Wait: TExecWait; var ResultCode: Integer; var ResultString: ansiString): Boolean;
var
  TempFilename: string;
  Command: string;
begin
  TempFilename := ExpandConstant('{tmp}\~execwithresult.txt');
  { Exec via cmd and redirect output to file. Must use special string-behavior to work. }
  Command := Format('"%s" /S /C ""%s" %s > "%s""', [ExpandConstant('{cmd}'), Filename, Params, TempFilename]);
  Result := Exec(ExpandConstant('{cmd}'), Command, WorkingDir, ShowCmd, Wait, ResultCode);
  if Result then
    begin
      LoadStringFromFile(TempFilename, ResultString);
    end;
  DeleteFile(TempFilename);
end;

procedure StopMaintenanceTasks();
var
  ResultCode: integer;
begin
  // TODO: #185 Instead of calling --help, use the correct action for stopping the
  // maintenance task
  Exec('scalar.exe', 'service --help', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function EnsureScalarNotRunning(): Boolean;
var
  MsgBoxResult: integer;
begin
  MsgBoxResult := IDRETRY;
  while (IsScalarRunning()) Do
    begin
      if(MsgBoxResult = IDRETRY) then
        begin
          MsgBoxResult := SuppressibleMsgBox('Scalar is currently running. Please close all instances of Scalar before continuing the installation.', mbError, MB_RETRYCANCEL, IDCANCEL);
        end;
      if(MsgBoxResult = IDCANCEL) then
        begin
          Result := False;
          Abort();
        end;
    end;

  Result := True;
end;

// Below are EVENT FUNCTIONS -> The main entry points of InnoSetup into the code region
// Documentation : http://www.jrsoftware.org/ishelp/index.php?topic=scriptevents

function InitializeUninstall(): Boolean;
begin
  StopMaintenanceTasks();
  Result := EnsureScalarNotRunning();
end;

// Called just after "install" phase, before "post install"
function NeedRestart(): Boolean;
begin
  Result := False;
end;

function UninstallNeedRestart(): Boolean;
begin
  Result := False;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:
      begin
        UninstallService('Scalar.Service', True);
      end;
    ssPostInstall:
      begin
        InstallScalarService();
      end;
    end;
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := ExitCode;
end;

procedure CurUninstallStepChanged(CurStep: TUninstallStep);
begin
  case CurStep of
    usUninstall:
      begin
        StopScalarServiceUI();
        UninstallService('Scalar.Service', False);
        RemovePath(ExpandConstant('{app}'));
      end;
    end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  Result := '';
  StopMaintenanceTasks();
  if not EnsureScalarNotRunning() then
    begin
      Abort();
    end;
  StopService('Scalar.Service');
  StopScalarServiceUI();
end;
