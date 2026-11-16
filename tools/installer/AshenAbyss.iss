; 《燼淵 Ashen Abyss》 web installer (Inno Setup 6.7+). Build with build_installer.ps1.
;
; The setup is small: it downloads the game's 7-Zip split parts from the GitHub release (or takes them from a local
; folder), checks every part against the release manifest (size + SHA-256), extracts them with the bundled 7-Zip
; code into a staging folder and then swaps the program files into place. Saves and settings in
; %APPDATA%\AshenAbyss are never touched. All command-line switches are documented in README.md.

#ifndef AppVer
  #define AppVer "0.1.0"
#endif
#ifndef OutDir
  #define OutDir "Y:\release_out\installer"
#endif
#define AppIdGuid "6B3E2F4A-9C1D-4E8B-A7F2-3D5C8E1B0A94"
#define ExeName "AshenAbyss.exe"
#define Repo "https://github.com/blhsing/ashen-abyss"

[Setup]
AppId={{{#AppIdGuid}}
AppName=燼淵 Ashen Abyss
AppVersion={#AppVer}
AppVerName=燼淵 Ashen Abyss {#AppVer}
AppPublisher=blhsing
AppPublisherURL={#Repo}
AppSupportURL={#Repo}/issues
AppUpdatesURL={#Repo}/releases
VersionInfoVersion={#AppVer}
VersionInfoProductName=燼淵 Ashen Abyss
VersionInfoDescription=燼淵 Ashen Abyss 安裝程式 (Setup)
DefaultDirName={userpf}\AshenAbyss
DisableProgramGroupPage=yes
DisableWelcomePage=no
DirExistsWarning=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
ArchiveExtraction=full
OutputDir={#OutDir}
OutputBaseFilename=AshenAbyss-{#AppVer}-Setup
SetupIconFile=Y:\game\assets\icons\AshenAbyss.ico
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName=燼淵 Ashen Abyss
WizardStyle=modern dark
WizardSizePercent=115
WizardImageFile=art\wizard_164x314.bmp,art\wizard_205x393.bmp,art\wizard_246x471.bmp,art\wizard_287x550.bmp,art\wizard_328x628.bmp,art\wizard_369x707.bmp,art\wizard_410x785.bmp
WizardSmallImageFile=art\small_55.bmp,art\small_69.bmp,art\small_83.bmp,art\small_96.bmp,art\small_110.bmp,art\small_124.bmp,art\small_138.bmp
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
SetupMutex=AshenAbyssSetupMutex
CloseApplications=no
ShowLanguageDialog=no
TimeStampsInUTC=yes

[Languages]
Name: "zhtw"; MessagesFile: "lang\ChineseTraditional.isl"

[Messages]
WelcomeLabel1=歡迎安裝《燼淵 Ashen Abyss》
WelcomeLabel2=本安裝程式會從 GitHub 下載 [name/ver] 的遊戲檔案，逐一驗證完整性（SHA-256）後安裝到你的電腦。%n%n• 預設安裝在你的使用者資料夾，不需要系統管理員權限。%n• 下載可使用系統（瀏覽器）的 Proxy 設定，或自訂 Proxy；中斷後可以接續下載。%n• 如果電腦上已經安裝過本遊戲，可以直接就地升級；存檔與設定不會受到影響。%n%n建議先關閉遊戲再繼續。
ConfirmUninstall=確定要移除 %1 嗎？%n%n你的存檔與設定（%APPDATA%\AshenAbyss）會保留，不會被刪除。
FinishedLabel=《燼淵 Ashen Abyss》已安裝完成。%n%n可以從「開始」功能表的捷徑啟動遊戲。存檔與設定位於 %APPDATA%\AshenAbyss。
FinishedLabelNoIcons=《燼淵 Ashen Abyss》已安裝完成。

[Tasks]
Name: "startmenuicon"; Description: "在「開始」功能表建立捷徑"; GroupDescription: "捷徑："
Name: "desktopicon"; Description: "在桌面建立捷徑"; GroupDescription: "捷徑："; Flags: unchecked

[Files]
Source: "licenses\7-Zip-License.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{code:GetShortcutName}"; Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\AshenAbyss.ico"; Comment: "燼淵 Ashen Abyss"; Tasks: startmenuicon
Name: "{userdesktop}\{code:GetShortcutName}"; Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\AshenAbyss.ico"; Comment: "燼淵 Ashen Abyss"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; Description: "立即啟動《燼淵 Ashen Abyss》"; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
; the program files are placed by code (extracted archive), so the uninstaller removes them explicitly; the list
; written at install time ({app}\.installed_items) is handled in CurUninstallStepChanged. Saves are elsewhere.
Type: filesandordirs; Name: "{app}\{#ExeName}"
Type: filesandordirs; Name: "{app}\AshenAbyss.pck"
Type: filesandordirs; Name: "{app}\AshenAbyss.ico"
Type: filesandordirs; Name: "{app}\assets"
Type: filesandordirs; Name: "{app}\data_AshenAbyss_windows_x86_64"
Type: filesandordirs; Name: "{app}\.setup-new"
Type: filesandordirs; Name: "{app}\.setup-old"
Type: filesandordirs; Name: "{app}\_download"
Type: files; Name: "{app}\.installed_items"
Type: dirifempty; Name: "{app}\licenses"
Type: dirifempty; Name: "{app}"

[Code]
const
  APP_VERSION = '{#AppVer}';
  REPO_DL = '{#Repo}/releases/download/';
  DEFAULT_SHORTCUT = '燼淵 Ashen Abyss';
  EXE_NAME = '{#ExeName}';
  UNINST_KEY = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#AppIdGuid}' + '}_is1';
  INET_KEY = 'Software\Microsoft\Windows\CurrentVersion\Internet Settings';
  STAGING = '.setup-new';
  OLDDIR = '.setup-old';
  ITEMS_FILE = '.installed_items';
  CHUNK = 262144;

  { exit codes (besides Inno Setup's own 0-8) }
  EC_CANCEL = 2;
  EC_BADARGS = 10;
  EC_MANIFEST = 11;
  EC_DOWNLOAD = 12;
  EC_PROXYAUTH = 13;
  EC_CHECKSUM = 14;
  EC_DISK = 15;
  EC_SOURCE = 16;
  EC_EXTRACT = 17;
  EC_INUSE = 18;
  EC_FILEOP = 19;
  EC_DET_INSTALLER = 40;
  EC_DET_SHORTCUT = 41;
  EC_DET_FOLDER = 42;

  { proxy modes }
  PM_SYSTEM = 0;
  PM_NONE = 1;
  PM_MANUAL = 2;

  { WinINet }
  INTERNET_OPEN_TYPE_PRECONFIG = 0;
  INTERNET_OPEN_TYPE_DIRECT = 1;
  INTERNET_OPEN_TYPE_PROXY = 3;
  INTERNET_SERVICE_HTTP = 3;
  INTERNET_FLAG_RELOAD = $80000000;
  INTERNET_FLAG_NO_CACHE_WRITE = $04000000;
  INTERNET_FLAG_SECURE = $00800000;
  INTERNET_FLAG_KEEP_CONNECTION = $00400000;
  INTERNET_FLAG_NO_UI = $00000200;
  INTERNET_FLAG_PRAGMA_NOCACHE = $00000100;
  INTERNET_OPTION_CONNECT_TIMEOUT = 2;
  INTERNET_OPTION_SEND_TIMEOUT = 5;
  INTERNET_OPTION_RECEIVE_TIMEOUT = 6;
  INTERNET_OPTION_PROXY_USERNAME = 43;
  INTERNET_OPTION_PROXY_PASSWORD = 44;
  HTTP_QUERY_STATUS_CODE = 19;
  HTTP_QUERY_FLAG_NUMBER = $20000000;

var
  { options (command line, then wizard) }
  OptSource, OptManifest, OptBaseUrl, OptVersion, OptProxy, OptProxyUser, OptProxyPass: String;
  OptDownloadDir, OptShortcutName, OptDetectLnk, OptDetectDir: String;
  OptProxyMode, OptRetries: Integer;
  OptKeepParts, OptNoUpgrade, OptDetectOnly, DirGiven: Boolean;
  ProxyHostPort: String;
  { detection }
  DetKind: Integer;             { 0 none, 1 installer registry key, 2 Start-menu shortcut, 3 default script folder }
  DetDir, DetVersion: String;
  { manifest }
  ManLoaded, ManVerified: Boolean;
  ManVersion, ManArchive, ManExe, ManSourceDesc: String;
  ManInstallSize, ManDownloadSize: Int64;
  PartName, PartSha: array of String;
  PartSize: array of Int64;
  TopLevel: array of String;
  { run state }
  FailCode: Integer;
  FailMsg: String;
  LastHttpFatal: Boolean;
  PartsDir: String;             { where the parts are read from for extraction }
  DownloadedHere: Boolean;      { the parts were downloaded by this run (delete afterwards unless kept) }
  AppDirCreated: Boolean;
  Busy, AbortRequested: Boolean;
  UiTick, SpeedTick: DWORD;
  SpeedBytes, SpeedBps: Int64;
  LastLogPct: Integer;
  RunNeed, RunGot: Int64;       { bytes still to download at the start of this run / received so far }
  { UI }
  UpgradePage: TInputOptionWizardPage;
  SourcePage, ProxyPage: TWizardPage;
  ProgPage: TOutputProgressWizardPage;
  AbortBtn: TNewButton;
  RbDownload, RbLocal, RbSystem, RbDirect, RbManual: TNewRadioButton;
  EdLocal, EdProxy, EdUser: TNewEdit;
  EdPass: TPasswordEdit;
  BtnBrowse, BtnTest: TNewButton;
  CbKeep: TNewCheckBox;
  LblTest, LblSysProxy: TNewStaticText;

{ ---------------------------------------------------------------- Win32 imports }
function InternetOpen(lpszAgent: String; dwAccessType: DWORD; lpszProxy, lpszProxyBypass: String; dwFlags: DWORD): Cardinal;
  external 'InternetOpenW@wininet.dll stdcall';
function InternetConnect(hInternet: Cardinal; lpszServerName: String; nServerPort: DWORD; lpszUserName, lpszPassword: String;
  dwService, dwFlags, dwContext: DWORD): Cardinal;
  external 'InternetConnectW@wininet.dll stdcall';
function HttpOpenRequest(hConnect: Cardinal; lpszVerb, lpszObjectName, lpszVersion, lpszReferrer: String;
  lplpszAcceptTypes: Cardinal; dwFlags, dwContext: DWORD): Cardinal;
  external 'HttpOpenRequestW@wininet.dll stdcall';
function HttpSendRequest(hRequest: Cardinal; lpszHeaders: String; dwHeadersLength: DWORD; lpOptional: Cardinal; dwOptionalLength: DWORD): BOOL;
  external 'HttpSendRequestW@wininet.dll stdcall';
function HttpQueryInfoDW(hRequest: Cardinal; dwInfoLevel: DWORD; var lpBuffer: DWORD; var lpdwBufferLength: DWORD; var lpdwIndex: DWORD): BOOL;
  external 'HttpQueryInfoW@wininet.dll stdcall';
function InternetReadFile(hFile: Cardinal; lpBuffer: AnsiString; dwNumberOfBytesToRead: DWORD; var lpdwNumberOfBytesRead: DWORD): BOOL;
  external 'InternetReadFile@wininet.dll stdcall';
function InternetCloseHandle(hInternet: Cardinal): BOOL;
  external 'InternetCloseHandle@wininet.dll stdcall';
function InternetSetOptionStr(hInternet: Cardinal; dwOption: DWORD; lpBuffer: String; dwBufferLength: DWORD): BOOL;
  external 'InternetSetOptionW@wininet.dll stdcall';
function InternetSetOptionDW(hInternet: Cardinal; dwOption: DWORD; var lpBuffer: DWORD; dwBufferLength: DWORD): BOOL;
  external 'InternetSetOptionW@wininet.dll stdcall';
function BCryptOpenAlgorithmProvider(var phAlgorithm: Cardinal; pszAlgId: String; pszImplementation: Cardinal; dwFlags: Cardinal): Cardinal;
  external 'BCryptOpenAlgorithmProvider@bcrypt.dll stdcall';
function BCryptCreateHash(hAlgorithm: Cardinal; var phHash: Cardinal; pbHashObject, cbHashObject, pbSecret, cbSecret, dwFlags: Cardinal): Cardinal;
  external 'BCryptCreateHash@bcrypt.dll stdcall';
function BCryptHashData(hHash: Cardinal; pbInput: AnsiString; cbInput: Cardinal; dwFlags: Cardinal): Cardinal;
  external 'BCryptHashData@bcrypt.dll stdcall';
function BCryptFinishHash(hHash: Cardinal; pbOutput: AnsiString; cbOutput: Cardinal; dwFlags: Cardinal): Cardinal;
  external 'BCryptFinishHash@bcrypt.dll stdcall';
function BCryptDestroyHash(hHash: Cardinal): Cardinal;
  external 'BCryptDestroyHash@bcrypt.dll stdcall';
function BCryptCloseAlgorithmProvider(hAlgorithm: Cardinal; dwFlags: Cardinal): Cardinal;
  external 'BCryptCloseAlgorithmProvider@bcrypt.dll stdcall';
function WinTickCount: DWORD;
  external 'GetTickCount@kernel32.dll stdcall';
procedure WinExitProcess(uExitCode: Cardinal);
  external 'ExitProcess@kernel32.dll stdcall';

{ ---------------------------------------------------------------- small helpers }
function Pad2(N: Integer): String;
begin
  Result := IntToStr(N);
  if N < 10 then Result := '0' + Result;
end;

function FmtBytes(B: Int64): String;
var X: Int64; W, F: Integer;
begin
  if B >= 1073741824 then begin
    X := (B * 100) div 1073741824; W := X div 100; F := X mod 100;
    Result := IntToStr(W) + '.' + Pad2(F) + ' GB';
  end else begin
    X := (B * 10) div 1048576; W := X div 10; F := X mod 10;
    Result := IntToStr(W) + '.' + IntToStr(F) + ' MB';
  end;
end;

function FmtSecs(S: Int64): String;
var M, Sec: Integer;
begin
  M := S div 60; Sec := S mod 60;
  if M >= 60 then Result := IntToStr(M div 60) + ' 小時 ' + IntToStr(M mod 60) + ' 分'
  else if M > 0 then Result := IntToStr(M) + ' 分 ' + IntToStr(Sec) + ' 秒'
  else Result := IntToStr(Sec) + ' 秒';
end;

function AddSlash(const S: String): String;
begin
  Result := S;
  if (Result <> '') and (Result[Length(Result)] <> '/') then Result := Result + '/';
end;

function SafeItemName(const N: String): Boolean;
begin
  Result := (N <> '') and (Pos('\', N) = 0) and (Pos('/', N) = 0) and (Pos(':', N) = 0) and (Pos('..', N) = 0)
    and (N[1] <> '.') and (N[1] <> ' ');
end;

function IsHex64(const S: String): Boolean;
var i: Integer;
begin
  Result := Length(S) = 64;
  if Result then
    for i := 1 to 64 do
      if Pos(S[i], '0123456789abcdefABCDEF') = 0 then begin Result := False; Exit; end;
end;

function FileSize64Of(const FileName: String): Int64;
var F: TFileStream;
begin
  Result := -1;
  if not FileExists(FileName) then Exit;
  try
    F := TFileStream.Create(FileName, fmOpenRead or fmShareDenyNone);
    try Result := F.Size; finally F.Free; end;
  except
    Result := -1;
  end;
end;

function PercentDecode(const S: String): String;
var i: Integer; H: String;
begin
  Result := ''; i := 1;
  while i <= Length(S) do begin
    if (S[i] = '%') and (i + 2 <= Length(S)) then begin
      H := '$' + Copy(S, i + 1, 2);
      Result := Result + Chr(StrToIntDef(H, 63)); i := i + 3;
    end else begin
      Result := Result + S[i]; i := i + 1;
    end;
  end;
end;

procedure SetFail(Code: Integer; const Msg: String);
begin
  FailCode := Code; FailMsg := Msg;
  Log(Format('ERROR %d: %s', [Code, Msg]));
end;

function ShortcutNameValue: String;
begin
  Result := OptShortcutName;
  if Result = '' then Result := DEFAULT_SHORTCUT;
end;

function GetShortcutName(Param: String): String;
begin
  Result := ShortcutNameValue;
end;

function AppDir: String;
begin
  Result := RemoveBackslashUnlessRoot(WizardDirValue);
end;

function DownloadDirValue: String;
begin
  if OptDownloadDir <> '' then Result := RemoveBackslashUnlessRoot(OptDownloadDir)
  else Result := AppDir + '\_download';
end;

{ ---------------------------------------------------------------- command line }
function FindParam(const Name: String; var Value: String): Boolean;
var i: Integer; P, U, N: String;
begin
  Result := False; Value := ''; N := '/' + Uppercase(Name);
  for i := 1 to ParamCount do begin
    P := ParamStr(i); U := Uppercase(P);
    if U = N then begin Result := True; Value := ''; end
    else if Pos(N + '=', U) = 1 then begin Result := True; Value := Copy(P, Length(N) + 2, MaxInt); end;
  end;
end;

function HasParam(const Name: String): Boolean;
var V: String;
begin
  Result := FindParam(Name, V);
end;

{ parses "[http://][user:pass@]host:port[/]" into ProxyHostPort (+ credentials); returns '' or an error text }
function ParseProxy(const Spec: String; var HostPort, User, Pass: String): String;
var S, Auth: String; i, Port: Integer;
begin
  Result := ''; S := Trim(Spec);
  if Pos('http://', Lowercase(S)) = 1 then S := Copy(S, 8, MaxInt)
  else if Pos('://', S) > 0 then begin Result := 'Proxy 只支援 http://（HTTP Proxy；HTTPS 流量會以 CONNECT 通道經過它）：' + Spec; Exit; end;
  while (S <> '') and (S[Length(S)] = '/') do S := Copy(S, 1, Length(S) - 1);
  i := Pos('@', S);
  if i > 0 then begin
    Auth := Copy(S, 1, i - 1); S := Copy(S, i + 1, MaxInt);
    i := Pos(':', Auth);
    if i > 0 then begin User := PercentDecode(Copy(Auth, 1, i - 1)); Pass := PercentDecode(Copy(Auth, i + 1, MaxInt)); end
    else User := PercentDecode(Auth);
  end;
  i := Pos(':', S);
  if (S = '') or (i <= 1) then begin Result := 'Proxy 必須寫成 http://主機:連接埠，例如 http://proxy.example.com:8080：' + Spec; Exit; end;
  Port := StrToIntDef(Copy(S, i + 1, MaxInt), -1);
  if (Port < 1) or (Port > 65535) or (Pos('/', S) > 0) then begin Result := 'Proxy 連接埠無效：' + Spec; Exit; end;
  HostPort := S;
end;

function ValidTag(const T: String): Boolean;
var i: Integer;
begin
  Result := T <> '';
  for i := 1 to Length(T) do
    if Pos(T[i], 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-') = 0 then Result := False;
end;

function ParseCommandLine: String;
var V, Tag, U, P: String; R: Integer;
begin
  Result := '';
  OptVersion := APP_VERSION; Tag := 'v' + APP_VERSION;
  if FindParam('TAG', V) then begin
    if not ValidTag(V) then begin Result := '/TAG= 的值無效：' + V; Exit; end;
    Tag := V;
    if (Length(V) > 1) and ((V[1] = 'v') or (V[1] = 'V')) then OptVersion := Copy(V, 2, MaxInt) else OptVersion := V;
  end;
  if FindParam('VERSION', V) then begin
    if not ValidTag(V) then begin Result := '/VERSION= 的值無效：' + V; Exit; end;
    OptVersion := V;
  end;
  OptBaseUrl := REPO_DL + Tag + '/';
  if FindParam('BASEURL', V) then begin
    if (Pos('http://', Lowercase(V)) <> 1) and (Pos('https://', Lowercase(V)) <> 1) then begin Result := '/BASEURL= 必須是 http:// 或 https:// 網址：' + V; Exit; end;
    OptBaseUrl := AddSlash(V);
  end;
  if FindParam('MANIFEST', V) then OptManifest := V;
  if FindParam('SOURCE', V) then begin
    if V = '' then begin Result := '/SOURCE= 需要一個資料夾路徑'; Exit; end;
    OptSource := RemoveBackslashUnlessRoot(ExpandFileName(V));
  end;
  if FindParam('DOWNLOADDIR', V) then begin
    if V = '' then begin Result := '/DOWNLOADDIR= 需要一個資料夾路徑'; Exit; end;
    OptDownloadDir := ExpandFileName(V);
  end;
  OptKeepParts := HasParam('KEEPPARTS');
  OptNoUpgrade := HasParam('NOUPGRADE');
  OptDetectOnly := HasParam('DETECTONLY');
  DirGiven := FindParam('DIR', V);
  OptRetries := 5;
  if FindParam('RETRIES', V) then begin
    R := StrToIntDef(V, -1);
    if (R < 0) or (R > 50) then begin Result := '/RETRIES= 必須是 0 到 50 的整數：' + V; Exit; end;
    OptRetries := R;
  end;
  if FindParam('SHORTCUTNAME', V) then begin
    if (V = '') or (Pos('\', V) > 0) or (Pos('/', V) > 0) or (Pos(':', V) > 0) then begin Result := '/SHORTCUTNAME= 的值無效：' + V; Exit; end;
    OptShortcutName := V;
  end;
  if FindParam('DETECTLNK', V) then OptDetectLnk := V;
  if FindParam('DETECTDIR', V) then OptDetectDir := V;

  OptProxyMode := PM_SYSTEM;
  if HasParam('NOPROXY') then OptProxyMode := PM_NONE;
  if FindParam('PROXY', V) then begin
    if OptProxyMode = PM_NONE then begin Result := '/PROXY= 與 /NOPROXY 不能同時使用'; Exit; end;
    if (Lowercase(V) = 'system') or (V = '') then OptProxyMode := PM_SYSTEM
    else if Lowercase(V) = 'none' then OptProxyMode := PM_NONE
    else begin
      U := ''; P := '';
      Result := ParseProxy(V, ProxyHostPort, U, P);
      if Result <> '' then Exit;
      OptProxy := V; OptProxyMode := PM_MANUAL; OptProxyUser := U; OptProxyPass := P;
    end;
  end;
  if FindParam('PROXYUSER', V) then OptProxyUser := V;
  if FindParam('PROXYPASS', V) then OptProxyPass := V;
end;

{ ---------------------------------------------------------------- system proxy (read only, for display/log) }
function SystemProxyDescription: String;
var En: Cardinal; Srv, Pac: String; B: AnsiString; Flags: Integer;
begin
  Result := '';
  if not RegQueryDWordValue(HKCU, INET_KEY, 'ProxyEnable', En) then En := 0;
  if not RegQueryStringValue(HKCU, INET_KEY, 'ProxyServer', Srv) then Srv := '';
  if not RegQueryStringValue(HKCU, INET_KEY, 'AutoConfigURL', Pac) then Pac := '';
  Flags := -1;
  if RegQueryBinaryValue(HKCU, INET_KEY + '\Connections', 'DefaultConnectionSettings', B) and (Length(B) >= 9) then
    Flags := Ord(B[9]);
  if (Flags >= 0) and ((Flags and 8) <> 0) then Result := Result + '自動偵測設定 (WPAD)；';
  if (Pac <> '') and ((Flags < 0) or ((Flags and 4) <> 0)) then Result := Result + '自動設定指令碼 ' + Pac + '；';
  if (En <> 0) and (Srv <> '') then Result := Result + 'Proxy 伺服器 ' + Srv + '；';
  if Result = '' then Result := '直接連線（未設定 Proxy）'
  else Result := Copy(Result, 1, Length(Result) - 1);
  Log(Format('System proxy (WinINet / Internet Options, HKCU): ProxyEnable=%d ProxyServer="%s" AutoConfigURL="%s" ConnectionFlags=%d', [En, Srv, Pac, Flags]));
end;

function ProxyModeText: String;
begin
  case OptProxyMode of
    PM_NONE: Result := '不使用 Proxy（直接連線）';
    PM_MANUAL: begin
      Result := '自訂 Proxy http://' + ProxyHostPort;
      if OptProxyUser <> '' then Result := Result + '（帳號 ' + OptProxyUser + '）';
    end;
  else
    Result := '系統 Proxy 設定：' + SystemProxyDescription;
  end;
end;

{ ---------------------------------------------------------------- progress UI }
function UiVisible: Boolean;
begin
  { the wizard (with this progress page) is visible in normal and /SILENT mode; /VERYSILENT shows no window }
  Result := (ProgPage <> nil) and ((not WizardSilent) or WizardForm.Visible);
end;

procedure Ui(const M1, M2: String; APos, AMax: Int64; Force: Boolean);
var T: DWORD; Pm, Pct: Integer;
begin
  T := WinTickCount;
  if (not Force) and (T - UiTick < 200) then Exit;
  UiTick := T;
  if AMax > 0 then Pm := (APos * 1000) div AMax else Pm := 0;
  if Pm > 1000 then Pm := 1000;
  if UiVisible then begin
    ProgPage.SetText(M1, M2);
    ProgPage.SetProgress(Pm, 1000);
  end;
  Pct := Pm div 100;
  if Force or (Pct <> LastLogPct) then begin
    LastLogPct := Pct;
    Log('  ' + M1 + ' | ' + M2);
  end;
end;

procedure AbortBtnClick(Sender: TObject);
begin
  if MsgBox('要中止嗎？已下載的部分會保留，下次執行安裝程式可以接續下載。', mbConfirmation, MB_YESNO) = IDYES then
    AbortRequested := True;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if Busy then begin
    Cancel := False; Confirm := False;
    AbortBtnClick(nil);
  end;
end;

{ ---------------------------------------------------------------- HTTP (WinINet: honours the system / browser proxy incl. PAC + WPAD) }
function WinInetErrText(Code: Integer): String;
begin
  case Code of
    12002: Result := '連線逾時';
    12007: Result := '找不到伺服器名稱（DNS）';
    12029: Result := '無法連線到伺服器或 Proxy';
    12030: Result := '連線被中斷';
    12031: Result := '連線被重設';
    12152: Result := '伺服器回應無效';
    12175, 12037, 12038, 12044, 12045, 12057, 12169: Result := 'HTTPS 憑證錯誤或無法檢查撤銷狀態';
    12180: Result := '自動 Proxy 設定指令碼無法使用';
    12166: Result := '自動 Proxy 設定指令碼發生錯誤';
  else
    Result := 'WinINet 錯誤';
  end;
  Result := Result + Format(' (%d)', [Code]);
end;

function CrackUrl(const Url: String; var Secure: Boolean; var Host: String; var Port: Integer; var Path: String): Boolean;
var S: String; i: Integer;
begin
  Result := False; S := Url;
  if Pos('https://', Lowercase(S)) = 1 then begin Secure := True; S := Copy(S, 9, MaxInt); Port := 443; end
  else if Pos('http://', Lowercase(S)) = 1 then begin Secure := False; S := Copy(S, 8, MaxInt); Port := 80; end
  else Exit;
  i := Pos('/', S);
  if i = 0 then begin Host := S; Path := '/'; end
  else begin Host := Copy(S, 1, i - 1); Path := Copy(S, i, MaxInt); end;
  i := Pos(':', Host);
  if i > 0 then begin
    Port := StrToIntDef(Copy(Host, i + 1, MaxInt), -1); Host := Copy(Host, 1, i - 1);
    if (Port < 1) or (Port > 65535) then Exit;
  end;
  Result := Host <> '';
end;

function OpenInternet: Cardinal;
var Ua: String; T: DWORD;
begin
  Ua := 'AshenAbyss-Setup/' + APP_VERSION + ' (Windows; WinINet)';
  case OptProxyMode of
    PM_NONE: Result := InternetOpen(Ua, INTERNET_OPEN_TYPE_DIRECT, '', '', 0);
    PM_MANUAL: Result := InternetOpen(Ua, INTERNET_OPEN_TYPE_PROXY, ProxyHostPort, '<local>', 0);
  else
    Result := InternetOpen(Ua, INTERNET_OPEN_TYPE_PRECONFIG, '', '', 0);
  end;
  if Result <> 0 then begin
    T := 30000; InternetSetOptionDW(Result, INTERNET_OPTION_CONNECT_TIMEOUT, T, 4);
    T := 60000; InternetSetOptionDW(Result, INTERNET_OPTION_SEND_TIMEOUT, T, 4);
    T := 60000; InternetSetOptionDW(Result, INTERNET_OPTION_RECEIVE_TIMEOUT, T, 4);
  end;
end;

procedure DrainRequest(hReq: Cardinal);
var Buf: AnsiString; Got: DWORD;
begin
  SetLength(Buf, 8192);
  repeat
    Got := 0;
    if not InternetReadFile(hReq, Buf, 8192, Got) then Break;
  until Got = 0;
end;

{ Downloads Url to DestFile. ExpectedSize > 0: resumes an existing (partial) DestFile with a Range request.
  Returns 0 on success, EC_CANCEL, EC_PROXYAUTH or EC_DOWNLOAD (LastHttpFatal = not worth retrying). }
function HttpDownload(const Url, DestFile: String; ExpectedSize: Int64; const Caption: String; BaseDone, Overall: Int64; var Err: String): Integer;
var hInet, hConn, hReq: Cardinal; Secure: Boolean; Host, Path, Hdr, M2: String; Port, Tries: Integer;
    Flags, Status, Len, Idx, Got, T0, T: DWORD; Offset, Done, Rem: Int64; F: TFileStream; Buf: AnsiString; Opened: Boolean;
begin
  Result := EC_DOWNLOAD; LastHttpFatal := False; Err := '';
  if not CrackUrl(Url, Secure, Host, Port, Path) then begin Err := '網址無效：' + Url; LastHttpFatal := True; Exit; end;
  Offset := 0;
  if ExpectedSize > 0 then begin
    Offset := FileSize64Of(DestFile);
    if Offset < 0 then Offset := 0;
    if Offset > ExpectedSize then begin DeleteFile(DestFile); Offset := 0; end;
    if Offset = ExpectedSize then begin Result := 0; Exit; end;
  end else
    DeleteFile(DestFile);
  if Offset > 0 then Log(Format('  resuming %s at byte %s', [ExtractFileName(DestFile), IntToStr(Offset)]));
  hConn := 0; hReq := 0; Opened := False;
  hInet := OpenInternet;
  if hInet = 0 then begin Err := '無法初始化網路連線：' + WinInetErrText(DLLGetLastError); Exit; end;
  try
    hConn := InternetConnect(hInet, Host, Port, '', '', INTERNET_SERVICE_HTTP, 0, 0);
    if hConn = 0 then begin Err := WinInetErrText(DLLGetLastError); Exit; end;
    Flags := INTERNET_FLAG_RELOAD or INTERNET_FLAG_NO_CACHE_WRITE or INTERNET_FLAG_KEEP_CONNECTION or INTERNET_FLAG_NO_UI or INTERNET_FLAG_PRAGMA_NOCACHE;
    if Secure then Flags := Flags or INTERNET_FLAG_SECURE;
    hReq := HttpOpenRequest(hConn, 'GET', Path, '', '', 0, Flags, 0);
    if hReq = 0 then begin Err := WinInetErrText(DLLGetLastError); Exit; end;
    if OptProxyUser <> '' then begin
      InternetSetOptionStr(hReq, INTERNET_OPTION_PROXY_USERNAME, OptProxyUser, Length(OptProxyUser));
      InternetSetOptionStr(hReq, INTERNET_OPTION_PROXY_PASSWORD, OptProxyPass, Length(OptProxyPass));
    end;
    Hdr := 'Accept: */*'#13#10;
    if Offset > 0 then Hdr := Hdr + 'Range: bytes=' + IntToStr(Offset) + '-'#13#10;
    Tries := 0;
    repeat
      if not HttpSendRequest(hReq, Hdr, Length(Hdr), 0, 0) then begin Err := WinInetErrText(DLLGetLastError); Exit; end;
      Status := 0; Len := 4; Idx := 0;
      HttpQueryInfoDW(hReq, HTTP_QUERY_STATUS_CODE or HTTP_QUERY_FLAG_NUMBER, Status, Len, Idx);
      if (Status = 407) and (OptProxyUser <> '') and (Tries < 2) then begin
        DrainRequest(hReq); Tries := Tries + 1;
      end else
        Break;
    until False;
    Log(Format('  GET %s -> HTTP %d', [Url, Status]));
    if (Status = 200) and (Offset > 0) then begin
      Log('  server ignored the Range request; downloading from the start'); Offset := 0;
    end else if Status = 206 then begin
    end else if Status = 200 then begin
    end else if Status = 416 then begin
      DeleteFile(DestFile); Err := 'HTTP 416（接續位置無效，將重新下載）'; Exit;
    end else if Status = 407 then begin
      Result := EC_PROXYAUTH; LastHttpFatal := True;
      if OptProxyUser = '' then Err := 'Proxy 需要驗證 (HTTP 407)：請提供 Proxy 帳號與密碼'
      else Err := 'Proxy 驗證失敗 (HTTP 407)：帳號或密碼不正確';
      Exit;
    end else begin
      Err := Format('伺服器回應 HTTP %d', [Status]);
      if (Status = 404) or (Status = 410) then Err := Err + '（找不到檔案：' + Url + '）';
      LastHttpFatal := (Status >= 400) and (Status < 500) and (Status <> 408) and (Status <> 429);
      Exit;
    end;

    if Offset = 0 then F := TFileStream.Create(DestFile, fmCreate)
    else begin
      F := TFileStream.Create(DestFile, fmOpenReadWrite or fmShareDenyWrite);
      F.Seek(Offset, 0);
    end;
    Opened := True;
    try
      Done := Offset; SetLength(Buf, CHUNK); T0 := WinTickCount;
      SpeedTick := T0; SpeedBytes := BaseDone + Done;
      repeat
        if AbortRequested then begin Result := EC_CANCEL; Err := '使用者已中止下載'; Exit; end;
        Got := 0;
        if not InternetReadFile(hReq, Buf, CHUNK, Got) then begin Err := '下載中斷：' + WinInetErrText(DLLGetLastError); Exit; end;
        if Got = 0 then Break;
        F.WriteBuffer(Buf, Got);
        Done := Done + Got;
        RunGot := RunGot + Got;
        T := WinTickCount;
        if T - SpeedTick >= 1000 then begin
          SpeedBps := ((BaseDone + Done - SpeedBytes) * 1000) div (T - SpeedTick);
          SpeedTick := T; SpeedBytes := BaseDone + Done;
        end;
        M2 := FmtBytes(BaseDone + Done) + ' / ' + FmtBytes(Overall);
        if SpeedBps > 0 then begin
          Rem := RunNeed - RunGot;
          if Rem < 0 then Rem := 0;
          Rem := Rem div SpeedBps;
          M2 := M2 + '　·　' + FmtBytes(SpeedBps) + '/s　·　剩餘約 ' + FmtSecs(Rem);
        end;
        Ui(Caption, M2, BaseDone + Done, Overall, False);
      until False;
      if (ExpectedSize > 0) and (Done <> ExpectedSize) then begin
        Err := Format('下載不完整（%s / %s）', [IntToStr(Done), IntToStr(ExpectedSize)]); Exit;
      end;
      T := WinTickCount - T0;
      if T = 0 then T := 1;
      Log(Format('  received %s bytes in %d.%d s (%s/s)', [IntToStr(Done - Offset), T div 1000, (T mod 1000) div 100,
        FmtBytes(((Done - Offset) * 1000) div T)]));
      Result := 0;
    finally
      if Opened then F.Free;
    end;
  finally
    if hReq <> 0 then InternetCloseHandle(hReq);
    if hConn <> 0 then InternetCloseHandle(hConn);
    InternetCloseHandle(hInet);
  end;
end;

{ ---------------------------------------------------------------- SHA-256 (Windows CNG, chunked so the UI stays alive) }
function Sha256File(const FileName, Caption: String; var Hex: String): Integer;
var hAlg, hHash: Cardinal; F: TFileStream; Buf, Dig: AnsiString; N: Longint; Done, Total: Int64; i: Integer;
begin
  Result := -1; Hex := ''; hAlg := 0; hHash := 0;
  if BCryptOpenAlgorithmProvider(hAlg, 'SHA256', 0, 0) <> 0 then Exit;
  try
    if BCryptCreateHash(hAlg, hHash, 0, 0, 0, 0, 0) <> 0 then Exit;
    try
      F := TFileStream.Create(FileName, fmOpenRead or fmShareDenyWrite);
      try
        Total := F.Size; Done := 0;
        repeat
          if AbortRequested then begin Result := EC_CANCEL; Exit; end;
          SetLength(Buf, 1048576);
          N := F.Read(Buf, 1048576);
          if N <= 0 then Break;
          if BCryptHashData(hHash, Buf, N, 0) <> 0 then Exit;
          Done := Done + N;
          Ui(Caption, FmtBytes(Done) + ' / ' + FmtBytes(Total), Done, Total, False);
        until False;
      finally
        F.Free;
      end;
      SetLength(Dig, 32);
      if BCryptFinishHash(hHash, Dig, 32, 0) <> 0 then Exit;
      for i := 1 to 32 do Hex := Hex + Lowercase(Format('%.2x', [Ord(Dig[i])]));
      Result := 0;
    finally
      BCryptDestroyHash(hHash);
    end;
  finally
    BCryptCloseAlgorithmProvider(hAlg, 0);
  end;
end;

{ ---------------------------------------------------------------- manifest }
function JsonValuePos(const J, Key: String): Integer;
var K: String; i: Integer;
begin
  Result := 0; K := '"' + Key + '"'; i := Pos(K, J);
  if i = 0 then Exit;
  i := i + Length(K);
  while (i <= Length(J)) and ((J[i] = ' ') or (J[i] = #9) or (J[i] = #13) or (J[i] = #10) or (J[i] = ':')) do i := i + 1;
  Result := i;
end;

function JsonStr(const J, Key: String): String;
var i: Integer;
begin
  Result := ''; i := JsonValuePos(J, Key);
  if (i = 0) or (i > Length(J)) or (J[i] <> '"') then Exit;
  i := i + 1;
  while (i <= Length(J)) and (J[i] <> '"') do begin
    if (J[i] = '\') and (i < Length(J)) then i := i + 1;
    Result := Result + J[i]; i := i + 1;
  end;
end;

function JsonInt(const J, Key: String): Int64;
var i: Integer; S: String;
begin
  Result := -1; i := JsonValuePos(J, Key); S := '';
  if i = 0 then Exit;
  while (i <= Length(J)) and (Pos(J[i], '0123456789') > 0) do begin S := S + J[i]; i := i + 1; end;
  if S <> '' then Result := StrToInt64Def(S, -1);
end;

function JsonArray(const J, Key: String): String;
var i, k: Integer;
begin
  Result := ''; i := JsonValuePos(J, Key);
  if (i = 0) or (i > Length(J)) or (J[i] <> '[') then Exit;
  Result := Copy(J, i + 1, MaxInt);
  k := Pos(']', Result);
  if k > 0 then Result := Copy(Result, 1, k - 1) else Result := '';
end;

procedure SetDefaultTopLevel;
begin
  SetArrayLength(TopLevel, 5);
  TopLevel[0] := 'AshenAbyss.exe'; TopLevel[1] := 'AshenAbyss.ico'; TopLevel[2] := 'AshenAbyss.pck';
  TopLevel[3] := 'assets'; TopLevel[4] := 'data_AshenAbyss_windows_x86_64';
end;

function ParseManifest(const J: String): String;
var A, Obj, N, Sha: String; i, k, Cnt: Integer; Sz: Int64; HasExe: Boolean;
begin
  Result := '';
  ManVersion := JsonStr(J, 'version');
  if ManVersion = '' then begin Result := '清單缺少 version'; Exit; end;
  A := JsonArray(J, 'parts'); Cnt := 0;
  SetArrayLength(PartName, 0); SetArrayLength(PartSize, 0); SetArrayLength(PartSha, 0);
  ManDownloadSize := 0;
  repeat
    i := Pos('{', A);
    if i = 0 then Break;
    A := Copy(A, i + 1, MaxInt); k := Pos('}', A);
    if k = 0 then begin Result := '清單格式錯誤（parts）'; Exit; end;
    Obj := Copy(A, 1, k - 1); A := Copy(A, k + 1, MaxInt);
    N := JsonStr(Obj, 'name'); Sz := JsonInt(Obj, 'size'); Sha := Lowercase(JsonStr(Obj, 'sha256'));
    if not SafeItemName(N) then begin Result := '清單中的檔名無效：' + N; Exit; end;
    if Sz <= 0 then begin Result := '清單中的檔案大小無效：' + N; Exit; end;
    if not IsHex64(Sha) then begin Result := '清單中的 SHA-256 無效：' + N; Exit; end;
    SetArrayLength(PartName, Cnt + 1); SetArrayLength(PartSize, Cnt + 1); SetArrayLength(PartSha, Cnt + 1);
    PartName[Cnt] := N; PartSize[Cnt] := Sz; PartSha[Cnt] := Sha;
    ManDownloadSize := ManDownloadSize + Sz;
    Cnt := Cnt + 1;
  until False;
  if Cnt = 0 then begin Result := '清單沒有任何分割檔 (parts)'; Exit; end;
  ManArchive := JsonStr(J, 'archive');
  if ManArchive = '' then ManArchive := PartName[0];
  if not SafeItemName(ManArchive) then begin Result := '清單中的 archive 無效'; Exit; end;
  ManExe := JsonStr(J, 'exe');
  if not SafeItemName(ManExe) then ManExe := EXE_NAME;
  ManInstallSize := JsonInt(J, 'install_size');
  if ManInstallSize <= 0 then ManInstallSize := (ManDownloadSize * 12) div 10;
  A := JsonArray(J, 'top_level'); Cnt := 0; HasExe := False;
  SetArrayLength(TopLevel, 0);
  repeat
    i := Pos('"', A);
    if i = 0 then Break;
    A := Copy(A, i + 1, MaxInt); k := Pos('"', A);
    if k = 0 then Break;
    N := Copy(A, 1, k - 1); A := Copy(A, k + 1, MaxInt);
    if not SafeItemName(N) then begin Result := '清單中的 top_level 項目無效：' + N; Exit; end;
    SetArrayLength(TopLevel, Cnt + 1); TopLevel[Cnt] := N; Cnt := Cnt + 1;
    if CompareText(N, ManExe) = 0 then HasExe := True;
  until False;
  if (Cnt = 0) or not HasExe then SetDefaultTopLevel;
  ManVerified := True;
end;

function ManifestName: String;
begin
  Result := 'AshenAbyss-' + OptVersion + '-manifest.json';
end;

{ Fallback for /SOURCE without any manifest: use the parts found in the folder (no checksum, logged as a warning). }
function ManifestFromFolder(const Dir: String): String;
var Prefix, N: String; i: Integer; Sz: Int64;
begin
  Result := '';
  Prefix := 'AshenAbyss-' + OptVersion + '-win64.7z.';
  SetArrayLength(PartName, 0); SetArrayLength(PartSize, 0); SetArrayLength(PartSha, 0);
  ManDownloadSize := 0; i := 0;
  repeat
    N := Prefix + Format('%.3d', [i + 1]);
    Sz := FileSize64Of(Dir + '\' + N);
    if Sz <= 0 then Break;
    SetArrayLength(PartName, i + 1); SetArrayLength(PartSize, i + 1); SetArrayLength(PartSha, i + 1);
    PartName[i] := N; PartSize[i] := Sz; PartSha[i] := '';
    ManDownloadSize := ManDownloadSize + Sz; i := i + 1;
  until False;
  if i = 0 then begin Result := '在 ' + Dir + ' 找不到 ' + Prefix + '001'; Exit; end;
  ManVersion := OptVersion; ManArchive := PartName[0]; ManExe := EXE_NAME;
  ManInstallSize := (ManDownloadSize * 12) div 10;
  SetDefaultTopLevel;
  ManVerified := False;
  Log('WARNING: no manifest found; using the parts present in ' + Dir + ' WITHOUT SHA-256 verification');
end;

function LoadManifest(var Err: String): Boolean;
var Src, Tmp: String; J: AnsiString; R: Integer;
begin
  Result := ManLoaded; Err := '';
  if ManLoaded then Exit;
  Src := '';
  if OptManifest <> '' then Src := OptManifest
  else if (OptSource <> '') and FileExists(OptSource + '\' + ManifestName) then Src := OptSource + '\' + ManifestName
  else Src := OptBaseUrl + ManifestName;
  Log('Manifest: ' + Src);
  if (Pos('http://', Lowercase(Src)) = 1) or (Pos('https://', Lowercase(Src)) = 1) then begin
    Tmp := ExpandConstant('{tmp}\manifest.json');
    R := HttpDownload(Src, Tmp, -1, '正在取得發行清單…', 0, 1, Err);
    if R <> 0 then begin
      if (OptSource <> '') and (OptManifest = '') then begin
        Log('Manifest download failed (' + Err + '); falling back to the parts in the source folder');
        Err := ManifestFromFolder(OptSource);
        Result := Err = '';
        if Result then begin ManLoaded := True; ManSourceDesc := '（無清單，未驗證 SHA-256）'; end;
        Exit;
      end;
      Err := '無法取得發行清單 ' + Src + '：' + Err;
      if R = EC_PROXYAUTH then FailCode := EC_PROXYAUTH;
      Exit;
    end;
  end else
    Tmp := Src;
  if not LoadStringFromFile(Tmp, J) then begin Err := '無法讀取發行清單：' + Tmp; Exit; end;
  Err := ParseManifest(String(J));
  if Err <> '' then begin Err := '發行清單 ' + Src + ' 無效：' + Err; Exit; end;
  ManLoaded := True; ManSourceDesc := Src;
  Log(Format('Manifest OK: version %s, %d part(s), download %s bytes, install %s bytes', [ManVersion, GetArrayLength(PartName),
    IntToStr(ManDownloadSize), IntToStr(ManInstallSize)]));
  Result := True;
end;

{ ---------------------------------------------------------------- detection of an existing installation }
function ExeIn(const Dir: String): Boolean;
begin
  Result := (Dir <> '') and FileExists(AddBackslash(Dir) + EXE_NAME);
end;

{ WScript.Shell cannot open a .lnk whose name is not ASCII (the shortcut is "燼淵 Ashen Abyss.lnk"), so a copy with
  an ASCII name in the setup's temporary folder is read instead. Read only: the shortcut itself is never changed. }
function ShortcutTarget(const Lnk: String): String;
var Shell, L: Variant; Copy1: String;
begin
  Result := '';
  if not FileExists(Lnk) then Exit;
  Copy1 := ExpandConstant('{tmp}\probe.lnk');
  try
    if not FileCopy(Lnk, Copy1, False) then begin Log('Could not copy shortcut ' + Lnk); Exit; end;
    Shell := CreateOleObject('WScript.Shell');
    L := Shell.CreateShortcut(Copy1);
    Result := L.TargetPath;
  except
    Log('Could not read shortcut ' + Lnk + ': ' + GetExceptionMessage);
  end;
  DeleteFile(Copy1);
end;

procedure DetectInstall;
var D, Lnk, T: String;
begin
  DetKind := 0; DetDir := ''; DetVersion := '';
  if RegQueryStringValue(HKCU, UNINST_KEY, 'InstallLocation', D) then begin
    D := RemoveBackslashUnlessRoot(D);
    if ExeIn(D) then begin
      DetKind := 1; DetDir := D;
      if not RegQueryStringValue(HKCU, UNINST_KEY, 'DisplayVersion', DetVersion) then DetVersion := '';
    end;
  end;
  if DetKind = 0 then begin
    if OptDetectLnk <> '' then Lnk := OptDetectLnk
    else Lnk := ExpandConstant('{userprograms}\') + DEFAULT_SHORTCUT + '.lnk';
    T := ShortcutTarget(Lnk);
    if (T <> '') and (CompareText(ExtractFileName(T), EXE_NAME) = 0) and FileExists(T) then begin
      DetKind := 2; DetDir := RemoveBackslashUnlessRoot(ExtractFileDir(T));
    end;
    Log('Detect: shortcut ' + Lnk + ' -> "' + T + '"');
  end;
  if DetKind = 0 then begin
    if OptDetectDir <> '' then D := OptDetectDir
    else D := ExpandConstant('{%USERPROFILE}\Games\AshenAbyss');
    D := RemoveBackslashUnlessRoot(D);
    if ExeIn(D) then begin DetKind := 3; DetDir := D; end;
  end;
  case DetKind of
    1: Log('Detect: installer-made installation ' + DetDir + ' version ' + DetVersion);
    2: Log('Detect: script-made installation via Start-menu shortcut: ' + DetDir);
    3: Log('Detect: script-made installation in the default folder: ' + DetDir);
  else
    Log('Detect: no existing installation found');
  end;
end;

function IsUpgrade: Boolean;
begin
  Result := (DetKind <> 0) and (CompareText(RemoveBackslashUnlessRoot(DetDir), AppDir) = 0);
end;

{ ---------------------------------------------------------------- file helpers }
function FileLocked(const FileName: String): Boolean;
var F: TFileStream;
begin
  Result := False;
  if not FileExists(FileName) then Exit;
  try
    F := TFileStream.Create(FileName, fmOpenReadWrite or fmShareExclusive);
    F.Free;
  except
    Result := True;
  end;
end;

function GameRunningIn(const Dir: String): Boolean;
begin
  Result := FileLocked(AddBackslash(Dir) + EXE_NAME) or FileLocked(AddBackslash(Dir) + 'AshenAbyss.pck');
end;

function ExistingDir(const Dir: String): String;
var P: String;
begin
  Result := Dir;
  while (Result <> '') and not DirExists(Result) do begin
    P := ExtractFileDir(Result);
    if (P = Result) or (P = '') then Break;
    Result := P;
  end;
end;

function FreeSpaceOf(const Dir: String): Int64;
var Free, Total: Int64;
begin
  Result := -1;
  if GetSpaceOnDisk64(ExistingDir(Dir), Free, Total) then Result := Free;
end;

function SameVolume(const A, B: String): Boolean;
begin
  Result := CompareText(ExtractFileDrive(ExpandFileName(A)), ExtractFileDrive(ExpandFileName(B))) = 0;
end;

function DirWritable(const Dir: String): Boolean;
var T: String;
begin
  Result := False;
  if not ForceDirectories(Dir) then Exit;
  T := AddBackslash(Dir) + '.aa_write_test';
  Result := SaveStringToFile(T, 'x', False);
  DeleteFile(T);
end;

function ItemPath(const Base, Name: String): String;
begin
  Result := AddBackslash(Base) + Name;
end;

function ItemExists(const P: String): Boolean;
begin
  Result := FileExists(P) or DirExists(P);
end;

procedure RemoveItem(const P: String);
begin
  if DirExists(P) then DelTree(P, True, True, True)
  else if FileExists(P) then begin
    DeleteFile(P);
  end;
end;

procedure CleanupStaging;
var A: String;
begin
  if not DirExists(WizardDirValue) then Exit;
  A := AppDir;
  if DirExists(ItemPath(A, STAGING)) then DelTree(ItemPath(A, STAGING), True, True, True);
end;

{ ---------------------------------------------------------------- the actual work (Preparing to Install) }
function PartUrl(const N: String): String;
begin
  Result := OptBaseUrl + N;
end;

function CheckDiskSpace(var Err: String): Boolean;
var i: Integer; NeedDl, NeedInst, FreeA, FreeD, Have: Int64; DlDir: String;
begin
  Result := True; Err := '';
  NeedInst := ManInstallSize + 64 * 1048576;
  NeedDl := 0; DlDir := DownloadDirValue;
  if OptSource = '' then
    for i := 0 to GetArrayLength(PartName) - 1 do begin
      if FileSize64Of(ItemPath(DlDir, PartName[i])) = PartSize[i] then Continue;
      Have := FileSize64Of(ItemPath(DlDir, PartName[i] + '.part'));
      if (Have < 0) or (Have > PartSize[i]) then Have := 0;
      NeedDl := NeedDl + PartSize[i] - Have;
    end;
  FreeA := FreeSpaceOf(AppDir);
  Log(Format('Disk: need %s bytes for the game in %s (free %s), %s bytes still to download into %s', [IntToStr(NeedInst), AppDir, IntToStr(FreeA), IntToStr(NeedDl), DlDir]));
  if FreeA < 0 then Exit;
  if (NeedDl > 0) and SameVolume(DlDir, AppDir) then begin
    if FreeA < NeedInst + NeedDl then begin
      Err := Format('磁碟空間不足：%s 需要 %s（下載 %s ＋ 安裝 %s），目前可用 %s。', [ExtractFileDrive(ExpandFileName(AppDir)), FmtBytes(NeedInst + NeedDl), FmtBytes(NeedDl), FmtBytes(NeedInst), FmtBytes(FreeA)]);
      Result := False;
    end;
  end else begin
    if FreeA < NeedInst then begin
      Err := Format('磁碟空間不足：安裝資料夾所在磁碟需要 %s，目前可用 %s。', [FmtBytes(NeedInst), FmtBytes(FreeA)]);
      Result := False; Exit;
    end;
    if NeedDl > 0 then begin
      FreeD := FreeSpaceOf(DlDir);
      if (FreeD >= 0) and (FreeD < NeedDl) then begin
        Err := Format('磁碟空間不足：下載資料夾所在磁碟需要 %s，目前可用 %s。', [FmtBytes(NeedDl), FmtBytes(FreeD)]);
        Result := False;
      end;
    end;
  end;
end;

{ download (or locate) and verify every part; sets PartsDir }
function AcquireParts(var Err: String): Integer;
var i, Attempt, R, Mism: Integer; Dl, Final, PartF, Hex, Cap: String; Base, Overall, Hav: Int64; T0, T: DWORD; Got: Int64;
begin
  Result := 0; Err := '';
  if OptSource <> '' then begin
    PartsDir := OptSource; DownloadedHere := False;
    Base := 0;
    for i := 0 to GetArrayLength(PartName) - 1 do begin
      Final := ItemPath(OptSource, PartName[i]);
      if FileSize64Of(Final) <> PartSize[i] then begin
        Result := EC_SOURCE;
        if FileExists(Final) then Err := Format('本機檔案大小不符：%s（應為 %s 位元組）', [Final, IntToStr(PartSize[i])])
        else Err := '本機資料夾缺少檔案：' + Final;
        Exit;
      end;
      if PartSha[i] <> '' then begin
        R := Sha256File(Final, Format('正在驗證 %d/%d：%s', [i + 1, GetArrayLength(PartName), PartName[i]]), Hex);
        if R = EC_CANCEL then begin Result := EC_CANCEL; Err := '使用者已中止'; Exit; end;
        if (R <> 0) or (Hex <> PartSha[i]) then begin
          Result := EC_CHECKSUM; Err := 'SHA-256 不符（檔案損毀或版本不同）：' + Final; Exit;
        end;
        Log('  SHA-256 OK: ' + PartName[i]);
      end;
    end;
    Exit;
  end;

  Dl := DownloadDirValue; PartsDir := Dl; DownloadedHere := True;
  if not DirWritable(Dl) then begin Result := EC_FILEOP; Err := '無法寫入下載資料夾：' + Dl; Exit; end;
  Overall := ManDownloadSize; Base := 0;
  T0 := WinTickCount; Got := 0;
  RunGot := 0; RunNeed := 0;
  for i := 0 to GetArrayLength(PartName) - 1 do begin
    if FileSize64Of(ItemPath(Dl, PartName[i])) = PartSize[i] then Continue;
    Hav := FileSize64Of(ItemPath(Dl, PartName[i] + '.part'));
    if (Hav < 0) or (Hav > PartSize[i]) then Hav := 0;
    RunNeed := RunNeed + PartSize[i] - Hav;
  end;
  for i := 0 to GetArrayLength(PartName) - 1 do begin
    Final := ItemPath(Dl, PartName[i]); PartF := Final + '.part';
    Cap := Format('正在下載 %d/%d：%s', [i + 1, GetArrayLength(PartName), PartName[i]]);
    Mism := 0;
    if FileSize64Of(Final) = PartSize[i] then begin
      R := Sha256File(Final, Format('正在驗證已下載的檔案 %d/%d：%s', [i + 1, GetArrayLength(PartName), PartName[i]]), Hex);
      if R = EC_CANCEL then begin Result := EC_CANCEL; Err := '使用者已中止'; Exit; end;
      if (R = 0) and (Hex = PartSha[i]) then begin
        Log('  already downloaded and verified: ' + PartName[i]);
        Base := Base + PartSize[i]; Continue;
      end;
      Log('  existing file failed verification, downloading again: ' + PartName[i]);
      DeleteFile(Final);
    end else if FileExists(Final) then
      DeleteFile(Final);
    Attempt := 0;
    repeat
      if Attempt > 0 then begin
        Log(Format('  retry %d/%d in %d s: %s', [Attempt, OptRetries, Attempt * 3, Err]));
        Ui(Cap, Format('連線問題，%d 秒後重試（%d/%d）：%s', [Attempt * 3, Attempt, OptRetries, Err]), Base, Overall, True);
        T := WinTickCount;
        while (WinTickCount - T < Attempt * 3000) and not AbortRequested do begin
          Sleep(100);
          Ui(Cap, Format('連線問題，稍後重試（%d/%d）：%s', [Attempt, OptRetries, Err]), Base, Overall, False);
        end;
      end;
      Got := Got - FileSize64Of(PartF);
      R := HttpDownload(PartUrl(PartName[i]), PartF, PartSize[i], Cap, Base, Overall, Err);
      Got := Got + FileSize64Of(PartF);
      if R = 0 then begin
        R := Sha256File(PartF, Format('正在驗證 SHA-256 %d/%d：%s', [i + 1, GetArrayLength(PartName), PartName[i]]), Hex);
        if R = EC_CANCEL then begin Result := EC_CANCEL; Err := '使用者已中止'; Exit; end;
        if (R = 0) and (Hex = PartSha[i]) then begin
          DeleteFile(Final);
          if not RenameFile(PartF, Final) then begin Result := EC_FILEOP; Err := '無法重新命名 ' + PartF; Exit; end;
          Log('  SHA-256 OK: ' + PartName[i] + ' ' + Hex);
          Break;
        end;
        Mism := Mism + 1;
        Log('  SHA-256 MISMATCH: ' + PartName[i] + ' got ' + Hex + ' expected ' + PartSha[i]);
        DeleteFile(PartF);
        Err := 'SHA-256 不符：' + PartName[i];
        if Mism >= 2 then begin Result := EC_CHECKSUM; Err := Err + '（重新下載後仍不符，發行檔案可能已更換或損毀）'; Exit; end;
      end else if (R = EC_CANCEL) or (R = EC_PROXYAUTH) then begin
        Result := R; Exit;
      end else if LastHttpFatal then begin
        Result := EC_DOWNLOAD; Exit;
      end;
      Attempt := Attempt + 1;
      if Attempt > OptRetries then begin Result := EC_DOWNLOAD; Err := '下載失敗（已重試 ' + IntToStr(OptRetries) + ' 次）：' + Err; Exit; end;
    until False;
    Base := Base + PartSize[i];
  end;
  T := WinTickCount - T0;
  if T = 0 then T := 1;
  if Got > 0 then
    Log(Format('Download summary: %s bytes in %d s, average %s/s', [IntToStr(Got), T div 1000, FmtBytes((Got * 1000) div T)]));
end;

function OnExtractProgress(const ArchiveName, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Ui('正在解壓縮遊戲檔案（' + FmtBytes(ManInstallSize) + '）', FileName, Progress, ProgressMax, False);
  Result := not AbortRequested;
end;

function ExtractToStaging(var Err: String): Integer;
var St: String;
begin
  Result := 0; Err := '';
  St := ItemPath(AppDir, STAGING);
  if DirExists(St) then DelTree(St, True, True, True);
  if not ForceDirectories(St) then begin Result := EC_FILEOP; Err := '無法建立暫存資料夾：' + St; Exit; end;
  Ui('正在解壓縮遊戲檔案（' + FmtBytes(ManInstallSize) + '）', '', 0, 1, True);
  try
    ExtractArchive(ItemPath(PartsDir, ManArchive), St, '', True, @OnExtractProgress);
  except
    if AbortRequested then begin Result := EC_CANCEL; Err := '使用者已中止'; end
    else begin Result := EC_EXTRACT; Err := '解壓縮失敗：' + GetExceptionMessage; end;
    DelTree(St, True, True, True);
    Exit;
  end;
  if not FileExists(ItemPath(St, ManExe)) then begin
    Result := EC_EXTRACT; Err := '解壓縮後找不到 ' + ManExe; DelTree(St, True, True, True); Exit;
  end;
  Log('Extracted into ' + St);
end;

function DoPrepare(var Err: String): Integer;
begin
  Result := 0;
  if not DirWritable(AppDir) then begin Result := EC_FILEOP; Err := '無法寫入安裝資料夾（可能需要系統管理員權限，請改選其他資料夾）：' + AppDir; Exit; end;
  if GameRunningIn(AppDir) then begin Result := EC_INUSE; Err := '遊戲正在執行中（' + AppDir + '），請先關閉遊戲再安裝。'; Exit; end;
  if not LoadManifest(Err) then begin
    if FailCode = EC_PROXYAUTH then Result := EC_PROXYAUTH else Result := EC_MANIFEST;
    FailCode := 0; Exit;
  end;
  if not CheckDiskSpace(Err) then begin Result := EC_DISK; Exit; end;
  Result := AcquireParts(Err);
  if Result <> 0 then Exit;
  Result := ExtractToStaging(Err);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Err: String; R: Integer;
begin
  Result := ''; FailCode := 0; FailMsg := ''; AbortRequested := False;
  if OptSource <> '' then Err := 'local folder ' + OptSource else Err := 'download ' + OptBaseUrl;
  if IsUpgrade then Log('Upgrade in place: ' + AppDir) else Log('New installation: ' + AppDir);
  Log('Source: ' + Err + '; network: ' + ProxyModeText);
  Err := '';
  AppDirCreated := not DirExists(AppDir);
  Busy := True;
  if UiVisible then begin
    ProgPage.SetText('正在準備…', '');
    ProgPage.SetProgress(0, 1000);
    ProgPage.Show;
    AbortBtn.Visible := True;
  end;
  try
    R := DoPrepare(Err);
  finally
    Busy := False;
    if UiVisible then begin AbortBtn.Visible := False; ProgPage.Hide; end;
  end;
  if R <> 0 then begin
    SetFail(R, Err);
    CleanupStaging;
    RemoveDir(DownloadDirValue);   { only if empty (partial downloads are kept for resuming) }
    if AppDirCreated and DirExists(AppDir) then RemoveDir(AppDir);
    if R = EC_CANCEL then Result := '已中止。已下載的部分保留在 ' + DownloadDirValue + '，下次執行安裝程式時會接續下載。'
    else Result := Err;
    if (R = EC_DOWNLOAD) or (R = EC_PROXYAUTH) or (R = EC_MANIFEST) then
      Result := Result + #13#10#13#10 + '網路設定：' + ProxyModeText + #13#10 + '可以按「上一步」變更 Proxy 設定後再試一次；已下載的部分會保留並接續下載。';
  end;
end;

{ ---------------------------------------------------------------- swap the new program files into place (Installing) }
procedure ReadPreviousItems(var L: TStringList);
var S: TArrayOfString; i: Integer;
begin
  if LoadStringsFromFile(ItemPath(AppDir, ITEMS_FILE), S) then
    for i := 0 to GetArrayLength(S) - 1 do
      if SafeItemName(Trim(S[i])) and (L.IndexOf(Trim(S[i])) < 0) then L.Add(Trim(S[i]));
end;

procedure SwapProgramFiles;
var A, St, Od, N: String; Old, Moved, NewItems: TStringList; i: Integer; Ok: Boolean;
begin
  A := AppDir; St := ItemPath(A, STAGING); Od := ItemPath(A, OLDDIR);
  if GameRunningIn(A) then begin
    SetFail(EC_INUSE, '遊戲正在執行中，請先關閉遊戲。');
    RaiseException(FailMsg);
  end;
  Old := TStringList.Create; Moved := TStringList.Create; NewItems := TStringList.Create;
  try
    Old.CaseSensitive := False;
    for i := 0 to GetArrayLength(TopLevel) - 1 do Old.Add(TopLevel[i]);
    ReadPreviousItems(Old);
    if DirExists(Od) then DelTree(Od, True, True, True);
    Ok := True;
    { 1. move the old program files aside }
    for i := 0 to Old.Count - 1 do begin
      N := Old[i];
      if ItemExists(ItemPath(A, N)) then begin
        if not ForceDirectories(Od) then begin Ok := False; Break; end;
        if RenameFile(ItemPath(A, N), ItemPath(Od, N)) then Moved.Add(N)
        else begin Log('Cannot move aside: ' + ItemPath(A, N)); Ok := False; Break; end;
      end;
    end;
    { 2. move the new ones in }
    if Ok then
      for i := 0 to GetArrayLength(TopLevel) - 1 do begin
        N := TopLevel[i];
        if not ItemExists(ItemPath(St, N)) then Continue;
        if RenameFile(ItemPath(St, N), ItemPath(A, N)) then NewItems.Add(N)
        else begin Log('Cannot move into place: ' + ItemPath(A, N)); Ok := False; Break; end;
      end;
    if not Ok then begin
      { roll back: take the new items out again, put the old ones back }
      for i := 0 to NewItems.Count - 1 do RemoveItem(ItemPath(A, NewItems[i]));
      for i := 0 to Moved.Count - 1 do RenameFile(ItemPath(Od, Moved[i]), ItemPath(A, Moved[i]));
      DelTree(St, True, True, True);
      SetFail(EC_FILEOP, '無法替換程式檔案（' + A + '）；原本的安裝已還原。');
      RaiseException(FailMsg);
    end;
    if Moved.Count > 0 then Log(Format('Upgrade: replaced %d item(s): %s', [Moved.Count, Moved.CommaText]));
    Log(Format('Installed %d item(s): %s', [NewItems.Count, NewItems.CommaText]));
    DelTree(Od, True, True, True);
    DelTree(St, True, True, True);
    DeleteFile(ItemPath(A, ITEMS_FILE));
    for i := 0 to NewItems.Count - 1 do SaveStringToFile(ItemPath(A, ITEMS_FILE), NewItems[i] + #13#10, True);
  finally
    Old.Free; Moved.Free; NewItems.Free;
  end;
end;

procedure DeleteDownloadedParts;
var i: Integer; Dl: String;
begin
  if not DownloadedHere then Exit;
  Dl := DownloadDirValue;
  if OptKeepParts then begin Log('Keeping the downloaded parts in ' + Dl); Exit; end;
  for i := 0 to GetArrayLength(PartName) - 1 do begin
    DeleteFile(ItemPath(Dl, PartName[i]));
    DeleteFile(ItemPath(Dl, PartName[i] + '.part'));
  end;
  RemoveDir(Dl);
  Log('Deleted the downloaded parts');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var KB: Cardinal;
begin
  if CurStep = ssInstall then begin
    WizardForm.StatusLabel.Caption := '正在替換程式檔案…';
    SwapProgramFiles;
  end else if CurStep = ssPostInstall then begin
    DeleteDownloadedParts;
  end else if CurStep = ssDone then begin
    KB := ManInstallSize div 1024;
    RegWriteDWordValue(HKCU, UNINST_KEY, 'EstimatedSize', KB);
    RegWriteStringValue(HKCU, UNINST_KEY, 'DisplayVersion', ManVersion);
    Log('AA-RESULT: 0 OK (' + AppDir + ', version ' + ManVersion + ')');
  end;
end;

{ ---------------------------------------------------------------- wizard pages }
procedure UpdateSourceControls;
begin
  EdLocal.Enabled := RbLocal.Checked;
  BtnBrowse.Enabled := RbLocal.Checked;
  CbKeep.Enabled := RbDownload.Checked;
end;

procedure SourceRadioClick(Sender: TObject);
begin
  UpdateSourceControls;
end;

procedure UpdateProxyControls;
begin
  EdProxy.Enabled := RbManual.Checked;
  EdUser.Enabled := not RbDirect.Checked;
  EdPass.Enabled := not RbDirect.Checked;
end;

procedure ProxyRadioClick(Sender: TObject);
begin
  UpdateProxyControls;
end;

procedure BrowseClick(Sender: TObject);
var D: String;
begin
  D := EdLocal.Text;
  if BrowseForFolder('選擇存放 AshenAbyss-…-win64.7z.001 等分割檔的資料夾：', D, False) then EdLocal.Text := D;
end;

function ApplyProxyPage(var Err: String): Boolean;
var U, P: String;
begin
  Result := True; Err := '';
  if RbDirect.Checked then OptProxyMode := PM_NONE
  else if RbManual.Checked then begin
    U := ''; P := '';
    Err := ParseProxy(EdProxy.Text, ProxyHostPort, U, P);
    if Err <> '' then begin Result := False; Exit; end;
    OptProxyMode := PM_MANUAL; OptProxy := EdProxy.Text;
    if (U <> '') and (EdUser.Text = '') then begin EdUser.Text := U; EdPass.Text := P; end;
  end else
    OptProxyMode := PM_SYSTEM;
  OptProxyUser := EdUser.Text; OptProxyPass := EdPass.Text;
end;

procedure TestClick(Sender: TObject);
var Err: String;
begin
  if not ApplyProxyPage(Err) then begin LblTest.Caption := Err; Exit; end;
  LblTest.Caption := '正在連線…';
  WizardForm.Refresh;
  ManLoaded := False;
  if LoadManifest(Err) then
    LblTest.Caption := Format('連線成功：版本 %s，需下載 %s（%d 個檔案），安裝後約 %s。', [ManVersion, FmtBytes(ManDownloadSize),
      GetArrayLength(PartName), FmtBytes(ManInstallSize)])
  else
    LblTest.Caption := '連線失敗：' + Err;
  FailCode := 0;
end;

function NewLabel(Page: TWizardPage; const Caption: String; Top, Height: Integer): TNewStaticText;
begin
  Result := TNewStaticText.Create(Page);
  Result.Parent := Page.Surface;
  Result.Caption := Caption;
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.Left := 0; Result.Top := Top; Result.Width := Page.SurfaceWidth; Result.Height := Height;
end;

function NewRadio(Page: TWizardPage; const Caption: String; Top: Integer): TNewRadioButton;
begin
  Result := TNewRadioButton.Create(Page);
  Result.Parent := Page.Surface;
  Result.Caption := Caption;
  Result.Left := 0; Result.Top := Top; Result.Width := Page.SurfaceWidth; Result.Height := ScaleY(18);
end;

procedure CreatePages;
var Y, W: Integer; L: TNewStaticText;
begin
  { upgrade }
  UpgradePage := CreateInputOptionPage(wpWelcome, '偵測到已安裝的《燼淵》', '要升級現有的安裝，還是安裝到其他資料夾？',
    '', True, False);
  UpgradePage.Add('升級現有的安裝（建議）：保留原本的資料夾，只替換程式檔案');
  UpgradePage.Add('安裝到其他資料夾（全新安裝）');
  { an explicit /DIR= always wins over the detected installation }
  UpgradePage.Values[0] := not OptNoUpgrade and not DirGiven;
  UpgradePage.Values[1] := OptNoUpgrade or DirGiven;

  { source }
  SourcePage := CreateCustomPage(wpSelectDir, '遊戲檔案來源', '選擇要從網路下載，或使用已經下載好的檔案。');
  W := SourcePage.SurfaceWidth;
  RbDownload := NewRadio(SourcePage, '從 GitHub 下載（建議）', 0);
  L := NewLabel(SourcePage, '來源：' + OptBaseUrl, ScaleY(20), ScaleY(30)); L.Left := ScaleX(18); L.Width := W - ScaleX(18);
  CbKeep := TNewCheckBox.Create(SourcePage);
  CbKeep.Parent := SourcePage.Surface; CbKeep.Left := ScaleX(18); CbKeep.Top := ScaleY(52); CbKeep.Width := W - ScaleX(18); CbKeep.Height := ScaleY(18);
  CbKeep.Caption := '安裝完成後保留下載的檔案（存放在安裝資料夾的 _download 子資料夾）';
  CbKeep.Checked := OptKeepParts;
  RbLocal := NewRadio(SourcePage, '從本機資料夾安裝（已下載的 .7z.001、.002… 分割檔，可離線安裝）', ScaleY(88));
  EdLocal := TNewEdit.Create(SourcePage);
  EdLocal.Parent := SourcePage.Surface; EdLocal.Left := ScaleX(18); EdLocal.Top := ScaleY(110);
  EdLocal.Width := W - ScaleX(18) - ScaleX(90); EdLocal.Text := OptSource;
  BtnBrowse := TNewButton.Create(SourcePage);
  BtnBrowse.Parent := SourcePage.Surface; BtnBrowse.Caption := '瀏覽(&R)…';
  BtnBrowse.Left := W - ScaleX(82); BtnBrowse.Top := ScaleY(108); BtnBrowse.Width := ScaleX(82); BtnBrowse.Height := ScaleY(25);
  BtnBrowse.OnClick := @BrowseClick;
  L := NewLabel(SourcePage, '安裝程式會依發行清單檢查每個檔案的大小與 SHA-256；下載中斷時，下次執行會從中斷處接續。存檔與設定（%APPDATA%\AshenAbyss）不會被變更。',
    ScaleY(148), ScaleY(48));
  RbDownload.Checked := OptSource = '';
  RbLocal.Checked := OptSource <> '';
  RbDownload.OnClick := @SourceRadioClick; RbLocal.OnClick := @SourceRadioClick;
  UpdateSourceControls;

  { proxy }
  ProxyPage := CreateCustomPage(SourcePage.ID, '網路連線（Proxy）', '下載時要如何連線到 GitHub？');
  W := ProxyPage.SurfaceWidth;
  RbSystem := NewRadio(ProxyPage, '使用系統的 Proxy 設定（建議，與瀏覽器／網際網路選項相同）', 0);
  LblSysProxy := NewLabel(ProxyPage, '目前的系統設定：' + SystemProxyDescription, ScaleY(19), ScaleY(30));
  LblSysProxy.Left := ScaleX(18); LblSysProxy.Width := W - ScaleX(18);
  RbDirect := NewRadio(ProxyPage, '不使用 Proxy（直接連線）', ScaleY(52));
  RbManual := NewRadio(ProxyPage, '使用下列 Proxy 伺服器：', ScaleY(76));
  EdProxy := TNewEdit.Create(ProxyPage);
  EdProxy.Parent := ProxyPage.Surface; EdProxy.Left := ScaleX(18); EdProxy.Top := ScaleY(98); EdProxy.Width := W - ScaleX(18);
  EdProxy.Text := OptProxy;
  if EdProxy.Text = '' then EdProxy.Text := 'http://';
  Y := ScaleY(126);
  L := NewLabel(ProxyPage, '例如 http://proxy.example.com:8080', Y, ScaleY(16)); L.Left := ScaleX(18); L.Width := W - ScaleX(18);
  Y := ScaleY(150);
  L := NewLabel(ProxyPage, 'Proxy 帳號（選填）：', Y + ScaleY(3), ScaleY(18)); L.Width := ScaleX(120);
  EdUser := TNewEdit.Create(ProxyPage);
  EdUser.Parent := ProxyPage.Surface; EdUser.Left := ScaleX(124); EdUser.Top := Y; EdUser.Width := ScaleX(150); EdUser.Text := OptProxyUser;
  L := NewLabel(ProxyPage, '密碼：', Y + ScaleY(3), ScaleY(18)); L.Left := ScaleX(284); L.Width := ScaleX(44);
  EdPass := TPasswordEdit.Create(ProxyPage);
  EdPass.Parent := ProxyPage.Surface; EdPass.Left := ScaleX(330); EdPass.Top := Y; EdPass.Width := W - ScaleX(330); EdPass.Text := OptProxyPass;
  BtnTest := TNewButton.Create(ProxyPage);
  BtnTest.Parent := ProxyPage.Surface; BtnTest.Caption := '測試連線(&T)';
  BtnTest.Left := 0; BtnTest.Top := ScaleY(184); BtnTest.Width := ScaleX(110); BtnTest.Height := ScaleY(25);
  BtnTest.OnClick := @TestClick;
  LblTest := NewLabel(ProxyPage, '', ScaleY(186), ScaleY(44)); LblTest.Left := ScaleX(120); LblTest.Width := W - ScaleX(120);
  RbSystem.Checked := OptProxyMode = PM_SYSTEM;
  RbDirect.Checked := OptProxyMode = PM_NONE;
  RbManual.Checked := OptProxyMode = PM_MANUAL;
  RbSystem.OnClick := @ProxyRadioClick; RbDirect.OnClick := @ProxyRadioClick; RbManual.OnClick := @ProxyRadioClick;
  UpdateProxyControls;

  { progress (shown during Preparing to Install) }
  ProgPage := CreateOutputProgressPage('正在下載並安裝遊戲檔案', '安裝程式正在下載、驗證並解壓縮《燼淵》的遊戲檔案，請稍候。');
  AbortBtn := TNewButton.Create(ProgPage);
  AbortBtn.Parent := ProgPage.Surface; AbortBtn.Caption := '中止(&A)';
  AbortBtn.Width := ScaleX(90); AbortBtn.Height := ScaleY(25);
  AbortBtn.Left := ProgPage.SurfaceWidth - AbortBtn.Width; AbortBtn.Top := ProgPage.SurfaceHeight - AbortBtn.Height - ScaleY(4);
  AbortBtn.OnClick := @AbortBtnClick; AbortBtn.Visible := False;
end;

procedure InitializeWizard;
var L: TNewStaticText;
begin
  CreatePages;
  if (DetKind <> 0) and not DirGiven and not OptNoUpgrade then
    WizardForm.DirEdit.Text := DetDir;
  WizardForm.DiskSpaceLabel.Caption := '安裝前會依發行清單檢查可用空間（需要遊戲大小加上下載檔案的空間；下載的檔案在安裝完成後會刪除）。';
  if DetKind <> 0 then begin
    UpgradePage.SubCaptionLabel.Caption := '這台電腦上已經安裝了《燼淵 Ashen Abyss》，請選擇安裝方式：';
    UpgradePage.CheckListBox.Height := ScaleY(56);
    L := TNewStaticText.Create(UpgradePage);
    L.Parent := UpgradePage.Surface;
    L.AutoSize := False; L.WordWrap := True;
    L.Left := 0; L.Top := UpgradePage.CheckListBox.Top + UpgradePage.CheckListBox.Height + ScaleY(12);
    L.Width := UpgradePage.SurfaceWidth; L.Height := ScaleY(110);
    case DetKind of
      1: L.Caption := '已安裝的版本：' + DetVersion + #13#10 + '位置：' + DetDir;
      2: L.Caption := '由安裝指令碼建立的安裝（經由「開始」功能表捷徑找到，版本不明）' + #13#10 + '位置：' + DetDir;
    else
      L.Caption := '由安裝指令碼建立的安裝（版本不明）' + #13#10 + '位置：' + DetDir;
    end;
    L.Caption := L.Caption + #13#10#13#10 + '升級會保留原本的資料夾，只替換遊戲程式檔案；存檔與設定（%APPDATA%\AshenAbyss）不會被變更。';
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = UpgradePage.ID then Result := DetKind = 0
  else if PageID = wpSelectDir then Result := (DetKind <> 0) and UpgradePage.Values[0]
  else if PageID = ProxyPage.ID then Result := RbLocal.Checked;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var Err, D: String;
begin
  Result := True;
  { Inno Setup also calls NextButtonClick in silent mode; there the command line alone decides (in particular the
    install folder: /DIR=, else the detected installation, else the default) }
  if WizardSilent then Exit;
  if CurPageID = UpgradePage.ID then begin
    if UpgradePage.Values[0] then WizardForm.DirEdit.Text := DetDir
    else if CompareText(WizardForm.DirEdit.Text, DetDir) = 0 then WizardForm.DirEdit.Text := ExpandConstant('{userpf}\AshenAbyss');
  end else if CurPageID = wpSelectDir then begin
    D := RemoveBackslashUnlessRoot(WizardForm.DirEdit.Text);
    if not DirWritable(D) then begin
      MsgBox('無法寫入這個資料夾（可能需要系統管理員權限）：' + #13#10 + D + #13#10#13#10 + '請選擇你的使用者資料夾內的位置，例如預設的 ' + ExpandConstant('{userpf}\AshenAbyss'), mbError, MB_OK);
      Result := False;
    end;
  end else if CurPageID = SourcePage.ID then begin
    OptKeepParts := CbKeep.Checked;
    if RbLocal.Checked then begin
      D := RemoveBackslashUnlessRoot(Trim(EdLocal.Text));
      if (D = '') or not DirExists(D) then begin MsgBox('請選擇存放分割檔的資料夾。', mbError, MB_OK); Result := False; Exit; end;
      if OptSource <> D then ManLoaded := False;
      OptSource := D;
      if not LoadManifest(Err) then begin MsgBox(Err, mbError, MB_OK); Result := False; FailCode := 0; Exit; end;
    end else begin
      if OptSource <> '' then ManLoaded := False;
      OptSource := '';
    end;
  end else if CurPageID = ProxyPage.ID then begin
    if not ApplyProxyPage(Err) then begin MsgBox(Err, mbError, MB_OK); Result := False; Exit; end;
    ManLoaded := False;
    WizardForm.NextButton.Enabled := False;
    LblTest.Caption := '正在取得發行清單…';
    WizardForm.Refresh;
    try
      if not LoadManifest(Err) then begin
        LblTest.Caption := '連線失敗：' + Err;
        MsgBox('無法連線到下載來源：' + #13#10 + Err + #13#10#13#10 + '請檢查網路與 Proxy 設定後再試一次。', mbError, MB_OK);
        Result := False; FailCode := 0;
      end else
        LblTest.Caption := Format('連線成功：版本 %s，需下載 %s。', [ManVersion, FmtBytes(ManDownloadSize)]);
    finally
      WizardForm.NextButton.Enabled := True;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var S, Free: String; F: Int64;
begin
  if IsUpgrade then S := '安裝方式：' + NewLine + Space + '就地升級現有的安裝（存檔與設定不受影響）'
  else S := '安裝方式：' + NewLine + Space + '全新安裝';
  S := S + NewLine + NewLine + '安裝資料夾：' + NewLine + Space + AppDir;
  if OptSource <> '' then S := S + NewLine + NewLine + '遊戲檔案來源：' + NewLine + Space + '本機資料夾 ' + OptSource
  else begin
    S := S + NewLine + NewLine + '遊戲檔案來源：' + NewLine + Space + '從 GitHub 下載 ' + OptBaseUrl;
    S := S + NewLine + Space + '網路：' + ProxyModeText;
    if OptKeepParts then S := S + NewLine + Space + '下載的檔案會保留在 ' + DownloadDirValue
    else S := S + NewLine + Space + '下載的檔案在安裝完成後會刪除';
  end;
  if ManLoaded then begin
    F := FreeSpaceOf(AppDir);
    if F >= 0 then Free := '，目前可用 ' + FmtBytes(F) else Free := '';
    S := S + NewLine + NewLine + '版本與大小：' + NewLine + Space + '《燼淵 Ashen Abyss》' + ManVersion;
    if OptSource = '' then S := S + NewLine + Space + Format('下載 %s（%d 個分割檔）', [FmtBytes(ManDownloadSize), GetArrayLength(PartName)]);
    S := S + NewLine + Space + '安裝後約 ' + FmtBytes(ManInstallSize) + Free;
  end;
  if MemoTasksInfo <> '' then S := S + NewLine + NewLine + MemoTasksInfo;
  Result := S;
end;

{ ---------------------------------------------------------------- setup / uninstall entry points }
function InitializeSetup: Boolean;
var Err: String;
begin
  Result := True; FailCode := 0;
  Log('Command line: ' + GetCmdTail);
  Err := ParseCommandLine;
  if Err <> '' then begin
    SetFail(EC_BADARGS, Err);
    if not WizardSilent then MsgBox('命令列參數錯誤：' + #13#10 + Err + #13#10#13#10 + '請參閱 README.md 的說明。', mbError, MB_OK);
    Result := False; Exit;
  end;
  DetectInstall;
  if OptDetectOnly then begin
    case DetKind of
      1: FailCode := EC_DET_INSTALLER;
      2: FailCode := EC_DET_SHORTCUT;
      3: FailCode := EC_DET_FOLDER;
    else FailCode := 0;
    end;
    FailMsg := 'detect only: ' + DetDir;
    Log(Format('AA-DETECT: kind=%d dir="%s" version="%s"', [DetKind, DetDir, DetVersion]));
    if FailCode = 0 then WinExitProcess(0);
    Result := False; Exit;
  end;
end;

procedure DeinitializeSetup;
var Dummy: Integer;
begin
  if Busy then AbortRequested := True;
  try
    CleanupStaging;
  except
  end;
  if FailCode <> 0 then begin
    Log(Format('AA-RESULT: %d %s', [FailCode, FailMsg]));
    { Inno Setup itself only knows exit codes 0-8: end the process with the specific code (documented in README.md).
      The temporary folder is removed first, as Setup would have done. }
    DelTree(ExpandConstant('{tmp}'), True, True, True);
    if DirExists(ExpandConstant('{tmp}')) then
      Exec(ExpandConstant('{cmd}'), '/c ping -n 4 127.0.0.1 >nul & rd /s /q "' + ExpandConstant('{tmp}') + '"', '', SW_HIDE, ewNoWait, Dummy);
    WinExitProcess(FailCode);
  end;
end;

{ uninstaller: refuses while the game runs; removes the program items listed at install time (never the saves) }
function InitializeUninstall: Boolean;
begin
  Result := True;
  if GameRunningIn(ExpandConstant('{app}')) then begin
    if not UninstallSilent then MsgBox('遊戲正在執行中，請先關閉《燼淵 Ashen Abyss》再移除。', mbError, MB_OK);
    Log('Uninstall refused: the game is running');
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var S: TArrayOfString; i: Integer; A, N: String;
begin
  if CurUninstallStep = usUninstall then begin
    A := ExpandConstant('{app}');
    if LoadStringsFromFile(AddBackslash(A) + ITEMS_FILE, S) then
      for i := 0 to GetArrayLength(S) - 1 do begin
        N := Trim(S[i]);
        if SafeItemName(N) then begin
          Log('Removing ' + AddBackslash(A) + N);
          if DirExists(AddBackslash(A) + N) then DelTree(AddBackslash(A) + N, True, True, True)
          else DeleteFile(AddBackslash(A) + N);
        end;
      end;
  end;
end;
