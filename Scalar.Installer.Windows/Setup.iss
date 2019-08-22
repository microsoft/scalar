; This script requires Inno Setup Compiler 5.5.9 or later to compile
; The Inno Setup Compiler (and IDE) can be found at http://www.jrsoftware.org/isinfo.php

; General documentation on how to use InnoSetup scripts: http://www.jrsoftware.org/ishelp/index.php

#define VCRuntimeDir PackagesDir + "\GVFS.VCRuntime.0.2.0-build\lib\x64"
#define ScalarDir BuildOutputDir + "\Scalar.Windows\bin\" + PlatformAndConfiguration
#define ScalarCommonDir BuildOutputDir + "\Scalar.Common\bin\" + PlatformAndConfiguration + "\netstandard2.0"
#define ServiceDir BuildOutputDir + "\Scalar.Service.Windows\bin\" + PlatformAndConfiguration
#define ServiceUIDir BuildOutputDir + "\Scalar.Service.UI\bin\" + PlatformAndConfiguration
#define ScalarMountDir BuildOutputDir + "\Scalar.Mount.Windows\bin\" + PlatformAndConfiguration
#define ReadObjectDir BuildOutputDir + "\Scalar.ReadObjectHook.Windows\bin\" + PlatformAndConfiguration
#define ScalarUpgraderDir BuildOutputDir + "\Scalar.Upgrader\bin\" + PlatformAndConfiguration + "\net461"

#define MyAppName "Scalar"
#define MyAppInstallerVersion GetFileVersion(ScalarDir + "\Scalar.exe")
#define MyAppPublisher "Microsoft Corporation"
#define MyAppPublisherURL "http://www.microsoft.com"
#define MyAppURL "https://github.com/microsoft/Scalar"
#define MyAppExeName "Scalar.exe"
#define EnvironmentKey "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"
#define FileSystemKey "SYSTEM\CurrentControlSet\Control\FileSystem"

[Setup]
AppId={{82F731CB-1CFC-406D-8D84-8467BF6040C7}
AppName={#MyAppName}
AppVersion={#MyAppInstallerVersion}
VersionInfoVersion={#MyAppInstallerVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright � Microsoft 2019
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
SetupIconFile="{#ScalarDir}\Images\scalar.ico"
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

; Scalar.Common Files
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarCommonDir}\git2.dll"

; Scalar.Mount Files
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarMountDir}\Scalar.Mount.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarMountDir}\Scalar.Mount.exe"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarMountDir}\Scalar.Mount.exe.config"

; Scalar.Upgrader Files
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarUpgraderDir}\Scalar.Upgrader.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarUpgraderDir}\Scalar.Upgrader.exe"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarUpgraderDir}\Scalar.Upgrader.exe.config"

; Scalar.ReadObjectHook files
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ReadObjectDir}\Scalar.ReadObjectHook.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ReadObjectDir}\Scalar.ReadObjectHook.exe"

; Cpp Dependencies
DestDir: "{app}"; Flags: ignoreversion; Source:"{#VCRuntimeDir}\msvcp140.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#VCRuntimeDir}\msvcp140_1.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#VCRuntimeDir}\msvcp140_2.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#VCRuntimeDir}\vcruntime140.dll"

; Scalar PDB's
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.Common.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.Platform.Windows.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.pdb"

; Scalar.Service.UI Files
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceUIDir}\Scalar.Service.UI.exe" 
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceUIDir}\Scalar.Service.UI.exe.config" 
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceUIDir}\Scalar.Service.UI.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceUIDir}\scalar.ico"

; Scalar Files
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\CommandLine.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.Common.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.Platform.Windows.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Newtonsoft.Json.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.exe.config"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Images\scalar.ico"  
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\Scalar.exe" 

; NuGet support DLLs
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Commands.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Common.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Configuration.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Frameworks.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Packaging.Core.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Packaging.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Protocol.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\NuGet.Versioning.dll"

; .NET Standard Files
; See https://github.com/dotnet/standard/issues/415 for a discussion on why this are copied
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\netstandard.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\System.Net.Http.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\System.ValueTuple.dll"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ScalarDir}\System.IO.Compression.dll"

; Scalar.Service Files and PDB's
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceDir}\Scalar.Service.pdb"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceDir}\Scalar.Service.exe.config"
DestDir: "{app}"; Flags: ignoreversion; Source:"{#ServiceDir}\Scalar.Service.exe"; AfterInstall: InstallScalarService

[UninstallDelete]
; Deletes the entire installation directory, including files and subdirectories
Type: filesandordirs; Name: "{app}";
Type: filesandordirs; Name: "{commonappdata}\Scalar\Scalar.Upgrade";

[Registry]
Root: HKLM; Subkey: "{#EnvironmentKey}"; \
    ValueType: expandsz; ValueName: "PATH"; ValueData: "{olddata};{app}"; \
    Check: NeedsAddPath(ExpandConstant('{app}'))

Root: HKLM; Subkey: "{#FileSystemKey}"; \
    ValueType: dword; ValueName: "NtfsEnableDetailedCleanupResults"; ValueData: "1"; \
    Check: IsWindows10VersionPriorToCreatorsUpdate

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

function IsWindows10VersionPriorToCreatorsUpdate(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major = 10) and (Version.Minor = 0) and (Version.Build < 15063);
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

function DeleteFileIfItExists(FilePath: string) : Boolean;
begin
  Result := False;
  if FileExists(FilePath) then
    begin
      Log('DeleteFileIfItExists: Removing ' + FilePath);
      if DeleteFile(FilePath) then
        begin
          if not FileExists(FilePath) then
            begin
              Result := True;
            end
          else
            begin
              Log('DeleteFileIfItExists: File still exists after deleting: ' + FilePath);
            end;
        end
      else
        begin
          Log('DeleteFileIfItExists: Failed to delete ' + FilePath);
        end;
    end
  else
    begin
      Log('DeleteFileIfItExists: File does not exist: ' + FilePath);
      Result := True;
    end;
end;

function IsScalarRunning(): Boolean;
var
  ResultCode: integer;
begin
  if Exec('powershell.exe', '-NoProfile "Get-Process scalar,scalar.mount | foreach {exit 10}"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
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

procedure UnmountRepos();
var
  ResultCode: integer;
begin
  Exec('scalar.exe', 'service --unmount-all', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure MountRepos();
var
  StatusText: string;
  MountOutput: ansiString;
  ResultCode: integer;
  MsgBoxText: string;
begin
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Mounting Repos.';
  WizardForm.ProgressGauge.Style := npbstMarquee;

  ExecWithResult(ExpandConstant('{app}') + '\scalar.exe', 'service --mount-all', '', SW_HIDE, ewWaitUntilTerminated, ResultCode, MountOutput);
  WizardForm.StatusLabel.Caption := StatusText;
  WizardForm.ProgressGauge.Style := npbstNormal;

  if (ResultCode <> 0) then
    begin
      MsgBoxText := 'Mounting one or more repos failed:' + #13#10 + MountOutput;
      SuppressibleMsgBox(MsgBoxText, mbConfirmation, MB_OK, IDOK);
      ExitCode := 17;
    end;
end;

function ConfirmUnmountAll(): Boolean;
var
  MsgBoxResult: integer;
  Repos: ansiString;
  ResultCode: integer;
  MsgBoxText: string;
begin
  Result := False;
  if ExecWithResult('scalar.exe', 'service --list-mounted', '', SW_HIDE, ewWaitUntilTerminated, ResultCode, Repos) then
    begin
      if Repos = '' then
        begin
          Result := False;
        end
      else
        begin
          if ResultCode = 0 then
            begin
              MsgBoxText := 'The following repos are currently mounted:' + #13#10 + Repos + #13#10 + 'Setup needs to unmount all repos before it can proceed, and those repos will be unavailable while setup is running. Do you want to continue?';
              MsgBoxResult := SuppressibleMsgBox(MsgBoxText, mbConfirmation, MB_OKCANCEL, IDOK);
              if (MsgBoxResult = IDOK) then
                begin
                  Result := True;
                end
              else
                begin
                  Abort();
                end;
            end;
        end;
    end;
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
  UnmountRepos();
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
        if ExpandConstant('{param:REMOUNTREPOS|true}') = 'true' then
          begin
            MountRepos();
          end
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
        UninstallService('Scalar.Service', False);
        RemovePath(ExpandConstant('{app}'));
      end;
    end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  Result := '';
  if ConfirmUnmountAll() then
    begin
      if ExpandConstant('{param:REMOUNTREPOS|true}') = 'true' then
        begin
          UnmountRepos();
        end
    end;
  if not EnsureScalarNotRunning() then
    begin
      Abort();
    end;
  StopService('Scalar.Service');
end;
