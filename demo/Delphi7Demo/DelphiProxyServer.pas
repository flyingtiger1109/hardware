unit DelphiProxyServer;

interface

uses
  Windows, SysUtils, Classes, WinSock, ExtCtrls,
  TerminalManager, TerminalClient, CallbackParser, FileSaver, PreviewManager, EncodingHelper,
  VlcPlayer, SyncObjs;

type
  TDelphiProxyServer = class;
  TLogCallback = procedure(const Msg: string) of object;
  TPreviewReadyCallback = procedure of object;

  TCallbackReceiverThread = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FListenSocket: TSocket;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer);
    procedure StopServer;
  end;

  TDelphiHttpServerThread = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FListenSocket: TSocket;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer);
    procedure StopServer;
  end;

  THttpClientWorker = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FWorkerIndex: Integer;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer; WorkerIndex: Integer);
  end;


  TProxyQueuedTask = class
  public
    Method: string;
    Path: string;
    BodyUtf8: string;
    Response: string;
    Generation: Integer;
    WaitForResponse: Boolean;
    CreatedTick: DWORD;
    DoneEvent: TEvent;
    StateLock: TCriticalSection;
    constructor Create(const AMethod, APath, ABodyUtf8: string; AGeneration: Integer; AWaitForResponse: Boolean);
    destructor Destroy; override;
  end;

  TProxyTaskWorker = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FQueue: TList;
    FLock: TCriticalSection;
    FEvent: TEvent;
    FMaxQueue: Integer;
    FWorkerName: string;
    function PopTask: TProxyQueuedTask;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer; const WorkerName: string; MaxQueue: Integer);
    destructor Destroy; override;
    function Enqueue(Task: TProxyQueuedTask): Boolean;
    procedure StopWorker;
  end;
  TAsyncSwitchThread = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FTerminalIndex: Integer;
    FSwitchGeneration: Integer;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer; TerminalIndex, SwitchGeneration: Integer);
  end;

  TAsyncStopPreviewThread = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FResType: TPreviewResourceType;
    FSessionType: TPreviewSessionType;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer; ResType: TPreviewResourceType; SessionType: TPreviewSessionType);
  end;

  TAsyncStartPreviewThread = class(TThread)
  private
    FOwner: TDelphiProxyServer;
    FResType: TPreviewResourceType;
    FSessionType: TPreviewSessionType;
    FHwnd: HWND;
    FTerminalBaseUrl: string;
    FRequestId: string;
    FCallbackUrl: string;
    FSwitchGeneration: Integer;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer; ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType; Hwnd: HWND; const TerminalBaseUrl,
      RequestId, CallbackUrl: string; SwitchGeneration: Integer);
  end;

  TVlcWarmupThread = class(TThread)
  private
    FOwner: TDelphiProxyServer;
  protected
    procedure Execute; override;
  public
    constructor Create(AOwner: TDelphiProxyServer);
  end;

  TDelphiProxyServer = class
  private
    FThread: TDelphiHttpServerThread;
    FCallbackThread: TCallbackReceiverThread;
    FTerminalManager: TTerminalManager;
    FTerminalClient: TTerminalClient;
    FCallbackParser: TCallbackParser;
    FFileSaver: TFileSaver;
    FPreviewManager: TPreviewManager;
    FRequestSaveDirs: TStringList;
    FRequestCallbacks: TStringList;
    FRequestGenerations: TStringList;
    FCompletedCallbacks: TStringList;
    FDelphiServerHost: string;
    FDelphiServerPort: Integer;
    FTerminalCallbackListenHost: string;
    FTerminalCallbackPublicHost: string;
    FTerminalCallbackPort: Integer;
    FTerminalCallbackPath: string;
    FDllCallbackHost: string;
    FDllCallbackPort: Integer;
    FDllCallbackBasePath: string;
    FLogProc: TLogCallback;
    FExternalPreviewReadyProc: TPreviewReadyCallback;
    FLanIp: string;
    FLocalActivePreviews: TPreviewResourceSet;
    FExternalActivePreviews: TPreviewResourceSet;
    FThirdPartyCameraHwnd: HWND;
    FThirdPartyFingerprintHwnd: HWND;
    FThirdPartyIrisHwnd: HWND;
    FSwitchingTerminal: Boolean;
    FSwitchGeneration: Integer;
    FLastSwitchTick: DWORD;
    FStateLock: TRTLCriticalSection;
    FHttpQueue: TList;
    FHttpQueueLock: TCriticalSection;
    FHttpQueueEvent: TEvent;
    FHttpStopping: Boolean;
    FHttpWorkers: array[0..7] of THttpClientWorker;
    FFaceCaptureWorker: TProxyTaskWorker;
    FFingerprintCaptureWorker: TProxyTaskWorker;
    FOcrWorker: TProxyTaskWorker;
    FNfcWorker: TProxyTaskWorker;
    procedure DoLog(const Msg: string);
    procedure NotifyExternalPreviewReady;
    function GenRequestId(const Prefix: string): string;
    function GetLocalLanIp: string;
    function GetCallbackBase: string;
    function GetDllCallbackUrl(const ResourcePath: string): string;
    function BuildTerminalProcessStartBody: string;
    procedure LoadRuntimeConfig;
    procedure AutoStartPreviews;
    procedure AutoStopPreviews;
    function ExecuteTerminalSwitch(Index, SwitchGeneration: Integer): Boolean;
    function StartTerminalSwitchAsync(Index: Integer): Boolean;
    function BeginTerminalSwitch(out SwitchGeneration: Integer): Boolean;
    procedure EndTerminalSwitch(SwitchGeneration: Integer);
    function IsTerminalSwitching: Boolean;
    function CurrentSwitchGeneration: Integer;
    function IsSwitchGenerationCurrent(SwitchGeneration: Integer): Boolean;
    function ShouldDropUnknownCallback(const RequestId: string): Boolean;
    procedure RememberRequestContext(const RequestId, SaveDir, CallbackUrl: string);
    procedure GetRequestContext(const RequestId: string; out SaveDir, CallbackUrl: string; out RequestGeneration: Integer);
    procedure ForgetRequestContext(const RequestId: string);
    procedure ClearRequestContexts;
    procedure PruneCompletedCallbacks;
    procedure MarkCompletedCallback(const RequestId: string);
    function IsCompletedCallback(const RequestId: string): Boolean;
    procedure StartHttpWorkers;
    procedure StopHttpWorkers;
    function EnqueueHttpClient(Client: TSocket): Boolean;
    function PopHttpClient: TSocket;
    procedure ProcessHttpClient(Client: TSocket);
    procedure StartBusinessWorkers;
    procedure StopBusinessWorkers;
    function QueueBusinessRequest(Worker: TProxyTaskWorker; const WorkerName, Method, Path, BodyUtf8: string; WaitMs: Integer): string;
    function HandleRequestDirect(const Method, Path, BodyUtf8: string): string;
    function HandleRequest(const Method, Path, BodyUtf8: string): string;
    function HandleTerminalCallback(const BodyUtf8: string): string;
    function MakeCallback(const RequestId, DllCallbackUrl, PayloadUtf8: string): Boolean;
  public
    constructor Create(ACameraPanel, AFingerprintPanel, AIrisPanel: TPanel);
    destructor Destroy; override;
    procedure Start;
    procedure Stop;
    procedure SetLogProc(ALogProc: TLogCallback);
    procedure SetExternalPreviewReadyProc(AProc: TPreviewReadyCallback);
    property TerminalManager: TTerminalManager read FTerminalManager;
    property PreviewManager: TPreviewManager read FPreviewManager;
    // Direct terminal operations (for Delphi UI buttons)
    function SwitchTerminalDirect(Index: Integer): Boolean;
    function StartProcessDirect(const SaveDir: string): Boolean;
    function EndProcessDirect: Boolean;
    function CaptureFaceDirect(const SaveDir: string; out SavePath: string): Boolean;
    function CaptureFingerprintDirect(const SaveDir: string; out SavePath: string): Boolean;
    function RequestOCRDirect(const SaveDir: string): string;
    function RequestNfcDirect(const SaveDir: string): string;
    function CaptureIrisDirect(const SaveDir: string): string;
    function StartCameraPreviewDirect: Boolean;
    function StopCameraPreviewDirect: Boolean;
    function StartFingerprintPreviewDirect: Boolean;
    function StopFingerprintPreviewDirect: Boolean;
    function StartIrisPreviewDirect: Boolean;
    function StopIrisPreviewDirect: Boolean;
  end;

implementation

const
  HTTP_QUEUE_LIMIT = 64;
  HTTP_SOCKET_TIMEOUT_MS = 2000;
  BUSINESS_WAIT_TIMEOUT_MS = 4500;

function PosExSimple(const SubStr, S: string; Offset: Integer): Integer;
var I: Integer;
begin Result := 0; if Offset < 1 then Offset := 1;
  for I := Offset to Length(S) - Length(SubStr) + 1 do
    if Copy(S, I, Length(SubStr)) = SubStr then begin Result := I; Exit; end;
end;

function JsonEscape(const S: string): string;
var I: Integer;
begin Result := '';
  for I := 1 to Length(S) do
    case S[I] of
      '\': Result := Result + '\\';
      '"': Result := Result + '\"';
      #13: Result := Result + '\r';
      #10: Result := Result + '\n';
      #9: Result := Result + '\t';
    else Result := Result + S[I];
    end;
end;

function JsonStr(const Name, Value: string): string;
begin Result := '"' + Name + '":"' + JsonEscape(Value) + '"'; end;

function JsonInt(const Name: string; Value: Int64): string;
begin Result := '"' + Name + '":' + IntToStr(Value); end;

function ResolveExactSaveFile(const FilePath: string): string;
var
  ParentDir: string;
begin
  Result := FilePath;
  if Result = '' then Exit;
  if ExtractFileDrive(Result) = '' then
    Result := ExpandFileName(ExtractFilePath(ParamStr(0)) + Result);
  ParentDir := ExtractFileDir(Result);
  if (ParentDir <> '') and not DirectoryExists(ParentDir) then
    ForceDirectories(ParentDir);
end;

function ExtractJsonString(const JsonUtf8, Name: string): string;
var Key: string; P, I: Integer; Escaped: Boolean;
begin Result := ''; Key := '"' + Name + '"'; P := Pos(Key, JsonUtf8);
  if P = 0 then Exit;
  P := PosExSimple(':', JsonUtf8, P + Length(Key)); if P = 0 then Exit; Inc(P);
  while (P <= Length(JsonUtf8)) and (JsonUtf8[P] in [' ', #9, #13, #10]) do Inc(P);
  if (P > Length(JsonUtf8)) or (JsonUtf8[P] <> '"') then Exit; Inc(P); Escaped := False;
  for I := P to Length(JsonUtf8) do begin
    if Escaped then begin
      case JsonUtf8[I] of 'n': Result := Result + #10; 'r': Result := Result + #13; 't': Result := Result + #9;
      else Result := Result + JsonUtf8[I]; end; Escaped := False;
    end else if JsonUtf8[I] = '\' then Escaped := True
    else if JsonUtf8[I] = '"' then Exit else Result := Result + JsonUtf8[I];
  end;
end;

function ExtractJsonInt(const JsonUtf8, Name: string): Int64;
var Text, Key: string; P: Integer;
begin Result := 0; Text := ExtractJsonString(JsonUtf8, Name);
  if Text <> '' then begin Result := StrToInt64Def(Text, 0); Exit; end;
  Key := '"' + Name + '"'; P := Pos(Key, JsonUtf8); if P = 0 then Exit;
  P := PosExSimple(':', JsonUtf8, P + Length(Key)); if P = 0 then Exit; Inc(P);
  while (P <= Length(JsonUtf8)) and (JsonUtf8[P] in [' ', #9, #13, #10]) do Inc(P);
  Text := '';
  while (P <= Length(JsonUtf8)) and (JsonUtf8[P] in ['0'..'9', '-']) do begin Text := Text + JsonUtf8[P]; Inc(P); end;
  Result := StrToInt64Def(Text, 0);
end;

function ExtractJsonObject(const JsonUtf8, Name: string): string;
var Key: string; P, I, Depth: Integer; InString, Escaped: Boolean;
begin
  Result := '';
  Key := '"' + Name + '"';
  P := Pos(Key, JsonUtf8);
  if P = 0 then Exit;
  P := PosExSimple(':', JsonUtf8, P + Length(Key));
  if P = 0 then Exit;
  Inc(P);
  while (P <= Length(JsonUtf8)) and (JsonUtf8[P] in [' ', #9, #13, #10]) do Inc(P);
  if (P > Length(JsonUtf8)) or (JsonUtf8[P] <> '{') then Exit;

  Depth := 0;
  InString := False;
  Escaped := False;
  for I := P to Length(JsonUtf8) do
  begin
    if InString then
    begin
      if Escaped then
        Escaped := False
      else if JsonUtf8[I] = '\' then
        Escaped := True
      else if JsonUtf8[I] = '"' then
        InString := False;
    end
    else
    begin
      if JsonUtf8[I] = '"' then
        InString := True
      else if JsonUtf8[I] = '{' then
        Inc(Depth)
      else if JsonUtf8[I] = '}' then
      begin
        Dec(Depth);
        if Depth = 0 then
        begin
          Result := Copy(JsonUtf8, P, I - P + 1);
          Exit;
        end;
      end;
    end;
  end;
end;

function HttpPostJson(const Url, BodyUtf8: string): Boolean;
var WSA: TWSAData; Sock: TSocket; Addr: TSockAddrIn; Host, Path, Req, HostPort: string; Port, SlashPos, ColonPos, TimeoutMs: Integer; U: string;
begin Result := False; U := Url; if Copy(U, 1, 7) = 'http://' then Delete(U, 1, 7);
  SlashPos := Pos('/', U); if SlashPos = 0 then Exit;
  HostPort := Copy(U, 1, SlashPos - 1); Path := Copy(U, SlashPos, MaxInt);
  ColonPos := Pos(':', HostPort);
  if ColonPos > 0 then begin Host := Copy(HostPort, 1, ColonPos - 1); Port := StrToIntDef(Copy(HostPort, ColonPos + 1, MaxInt), 80); end
  else begin Host := HostPort; Port := 80; end;
  if Host = '' then Exit; if WSAStartup($0202, WSA) <> 0 then Exit;
  try Sock := socket(AF_INET, SOCK_STREAM, IPPROTO_TCP); if Sock = INVALID_SOCKET then Exit;
    TimeoutMs := 3000;
    setsockopt(Sock, SOL_SOCKET, SO_RCVTIMEO, PChar(@TimeoutMs), SizeOf(TimeoutMs));
    setsockopt(Sock, SOL_SOCKET, SO_SNDTIMEO, PChar(@TimeoutMs), SizeOf(TimeoutMs));
    try FillChar(Addr, SizeOf(Addr), 0); Addr.sin_family := AF_INET; Addr.sin_port := htons(Port);
      Addr.sin_addr.S_addr := inet_addr(PChar(Host)); if Addr.sin_addr.S_addr = INADDR_NONE then Exit;
      if connect(Sock, Addr, SizeOf(Addr)) <> 0 then Exit;
      Req := 'POST ' + Path + ' HTTP/1.1'#13#10 + 'Host: ' + Host + ':' + IntToStr(Port) + #13#10 +
        'Content-Type: application/json; charset=utf-8'#13#10 + 'Content-Length: ' + IntToStr(Length(BodyUtf8)) + #13#10 +
        'Connection: close'#13#10#13#10 + BodyUtf8;
      Result := send(Sock, Req[1], Length(Req), 0) = Length(Req);
    finally closesocket(Sock); end;
  finally WSACleanup; end;
end;

function SafeResolveSaveDir(const SaveDir: string): string;
begin Result := SaveDir; if Result = '' then Result := ExtractFilePath(ParamStr(0)) + 'captures';
  if not DirectoryExists(Result) then ForceDirectories(Result); end;

// ============================================================
// GetLocalLanIp - find local IP matching terminal subnet
// ============================================================
function TDelphiProxyServer.GetLocalLanIp: string;
var
  TerminalIp, SubnetPrefix: string;
  HostEnt: PHostEnt;
  HostName: array[0..255] of Char;
  P: PChar;
  I: Integer;
begin
  Result := '127.0.0.1';
  if gethostname(HostName, SizeOf(HostName)) <> 0 then Exit;
  HostEnt := gethostbyname(HostName);
  if HostEnt = nil then Exit;

  // Get subnet prefix from current terminal IP
  TerminalIp := FTerminalManager.CurrentBaseUrl;
  // Extract IP from URL: "http://192.168.20.30:9098" -> "192.168.20"
  if Copy(TerminalIp, 1, 7) = 'http://' then Delete(TerminalIp, 1, 7);
  I := Pos(':', TerminalIp);
  if I > 0 then TerminalIp := Copy(TerminalIp, 1, I - 1);
  // Get first 3 octets
  I := Pos('.', TerminalIp);
  if I > 0 then I := Pos('.', Copy(TerminalIp, I+1, MaxInt));
  // Actually we need "192.168.20"
  SubnetPrefix := TerminalIp;
  // Remove last octet: "192.168.20.30" -> "192.168.20."
  I := Length(SubnetPrefix);
  while (I > 0) and (SubnetPrefix[I] <> '.') do Dec(I);
  if I > 0 then SubnetPrefix := Copy(SubnetPrefix, 1, I);

  // Find local IP matching the subnet
  P := HostEnt.h_addr_list^;
  I := 0;
  while P <> nil do
  begin
    Result := Format('%d.%d.%d.%d', [Byte(P[0]), Byte(P[1]), Byte(P[2]), Byte(P[3])]);
    if Pos(SubnetPrefix, Result) = 1 then
      Exit; // Found matching subnet
    Inc(I);
    if I >= 16 then Break;
    P := PChar(Pointer(Integer(HostEnt.h_addr_list) + I * SizeOf(Pointer)));
  end;

  // No matching subnet found, return first IP
  P := HostEnt.h_addr_list^;
  if P <> nil then
    Result := Format('%d.%d.%d.%d', [Byte(P[0]), Byte(P[1]), Byte(P[2]), Byte(P[3])]);
end;

function TDelphiProxyServer.GetCallbackBase: string;
var CallbackHost: string;
begin
  CallbackHost := FTerminalCallbackPublicHost;
  if CallbackHost = '' then CallbackHost := FLanIp;
  Result := 'http://' + CallbackHost + ':' + IntToStr(FTerminalCallbackPort) + FTerminalCallbackPath;
end;

function TDelphiProxyServer.GetDllCallbackUrl(const ResourcePath: string): string;
var PathText: string;
begin
  PathText := ResourcePath;
  if (PathText <> '') and (PathText[1] <> '/') then
    PathText := '/' + PathText;
  Result := 'http://' + FDllCallbackHost + ':' + IntToStr(FDllCallbackPort) +
    FDllCallbackBasePath + PathText;
end;

function TDelphiProxyServer.BuildTerminalProcessStartBody: string;
var CallbackBase, RequestId: string;
begin
  CallbackBase := GetCallbackBase;
  RequestId := GenRequestId('PROCESS');
  Result := '{' + JsonStr('request_id', RequestId) + ',"callbacks":{' +
    JsonStr('ocr_document', CallbackBase) + ',' +
    JsonStr('ocr_event_status', CallbackBase) + ',' +
    JsonStr('nfc_card', CallbackBase) + '}}';
end;

procedure TDelphiProxyServer.LoadRuntimeConfig;
var ConfigPath, ConfigText, Section, HostText, BasePathText: string;
  SL: TStringList; PortValue: Int64;
  NetworkCachingValue, LiveCachingValue: Integer;
begin
  FDelphiServerHost := '127.0.0.1';
  FDelphiServerPort := 8080;
  FTerminalCallbackListenHost := '0.0.0.0';
  FTerminalCallbackPublicHost := '';
  FTerminalCallbackPort := 8081;
  FTerminalCallbackPath := '/terminal-callback';
  FDllCallbackHost := '127.0.0.1';
  FDllCallbackPort := 39091;
  FDllCallbackBasePath := '/HZCYKJTHardWare/callback';

  ConfigPath := ExtractFilePath(ParamStr(0)) + 'HZCYKJTHardWare.json';
  if not FileExists(ConfigPath) then Exit;

  SL := TStringList.Create;
  try
    SL.LoadFromFile(ConfigPath);
    ConfigText := SL.Text;
  finally
    SL.Free;
  end;

  FTerminalManager.LoadFromConfig(ConfigText);

  Section := ExtractJsonObject(ConfigText, 'delphi_server');
  if Section <> '' then
  begin
    HostText := ExtractJsonString(Section, 'host');
    if HostText <> '' then
      FDelphiServerHost := HostText;

    PortValue := ExtractJsonInt(Section, 'port');
    if (PortValue > 0) and (PortValue <= 65535) then
      FDelphiServerPort := PortValue;
  end;

  Section := ExtractJsonObject(ConfigText, 'terminal_callback_server');
  if Section <> '' then
  begin
    HostText := ExtractJsonString(Section, 'listen_host');
    if HostText <> '' then
      FTerminalCallbackListenHost := HostText;

    FTerminalCallbackPublicHost := ExtractJsonString(Section, 'public_host');

    PortValue := ExtractJsonInt(Section, 'port');
    if (PortValue > 0) and (PortValue <= 65535) then
      FTerminalCallbackPort := PortValue;

    BasePathText := ExtractJsonString(Section, 'path');
    if BasePathText <> '' then
    begin
      if BasePathText[1] <> '/' then
        BasePathText := '/' + BasePathText;
      FTerminalCallbackPath := BasePathText;
    end;
  end;

  NetworkCachingValue := 150;
  LiveCachingValue := 150;
  Section := ExtractJsonObject(ConfigText, 'preview');
  if Section <> '' then
  begin
    if Pos('"rtsp_network_caching_ms"', Section) > 0 then
      NetworkCachingValue := ExtractJsonInt(Section, 'rtsp_network_caching_ms');
    if Pos('"rtsp_live_caching_ms"', Section) > 0 then
      LiveCachingValue := ExtractJsonInt(Section, 'rtsp_live_caching_ms');
  end;
  FPreviewManager.SetCachingMs(NetworkCachingValue, LiveCachingValue);

  Section := ExtractJsonObject(ConfigText, 'callback_server');
  if Section = '' then Exit;

  HostText := ExtractJsonString(Section, 'host');
  if HostText <> '' then
    FDllCallbackHost := HostText;

  PortValue := ExtractJsonInt(Section, 'port');
  if (PortValue > 0) and (PortValue <= 65535) then
    FDllCallbackPort := PortValue;

  BasePathText := ExtractJsonString(Section, 'base_path');
  if BasePathText <> '' then
  begin
    if BasePathText[1] <> '/' then
      BasePathText := '/' + BasePathText;
    while (Length(BasePathText) > 1) and (BasePathText[Length(BasePathText)] = '/') do
      Delete(BasePathText, Length(BasePathText), 1);
    FDllCallbackBasePath := BasePathText;
  end;
end;

// ============================================================
// TCallbackReceiverThread
// ============================================================
constructor TCallbackReceiverThread.Create(AOwner: TDelphiProxyServer);
begin inherited Create(True); FreeOnTerminate := False; FOwner := AOwner; FListenSocket := INVALID_SOCKET; end;

procedure TCallbackReceiverThread.StopServer;
begin Terminate; if FListenSocket <> INVALID_SOCKET then begin closesocket(FListenSocket); FListenSocket := INVALID_SOCKET; end; end;

procedure TCallbackReceiverThread.Execute;
var WSA: TWSAData; Addr: TSockAddrIn; Client: TSocket; Buf: array[0..16383] of Char;
  RecvLen, HeaderEnd, ContentLength, BodyLen, NeedLen, CLPos, LineEnd, TimeoutMs: Integer;
  Raw, Header, BodyUtf8, Response, ResponseBody, Chunk: string;
begin if WSAStartup($0202, WSA) <> 0 then Exit;
  try FListenSocket := socket(AF_INET, SOCK_STREAM, IPPROTO_TCP); if FListenSocket = INVALID_SOCKET then Exit;
    FillChar(Addr, SizeOf(Addr), 0); Addr.sin_family := AF_INET;
    Addr.sin_addr.S_addr := inet_addr(PChar(FOwner.FTerminalCallbackListenHost));
    Addr.sin_port := htons(FOwner.FTerminalCallbackPort);
    if bind(FListenSocket, Addr, SizeOf(Addr)) <> 0 then begin
      FOwner.DoLog('[错误] [终端回调] 回调接收服务启动失败：bind ' + FOwner.FTerminalCallbackListenHost + ':' +
        IntToStr(FOwner.FTerminalCallbackPort) + ' ，error=' + IntToStr(WSAGetLastError) + '，');
      Exit;
    end;
    if listen(FListenSocket, SOMAXCONN) <> 0 then begin
      FOwner.DoLog('[错误] [终端回调] 回调接收服务启动失败：listen ' + FOwner.FTerminalCallbackListenHost + ':' +
        IntToStr(FOwner.FTerminalCallbackPort) + ' ，error=' + IntToStr(WSAGetLastError) + '，');
      Exit;
    end;
    FOwner.DoLog('[信息] [终端回调] 回调接收服务程序已启动，listen=' + FOwner.FTerminalCallbackListenHost + ':' +
      IntToStr(FOwner.FTerminalCallbackPort));
    FOwner.DoLog('[信息] [终端回调] 终端回调地址为：' + FOwner.GetCallbackBase);
    while not Terminated do begin
      Client := accept(FListenSocket, nil, nil); if Client = INVALID_SOCKET then Continue;
      TimeoutMs := 5000;
      setsockopt(Client, SOL_SOCKET, SO_RCVTIMEO, PChar(@TimeoutMs), SizeOf(TimeoutMs));
      setsockopt(Client, SOL_SOCKET, SO_SNDTIMEO, PChar(@TimeoutMs), SizeOf(TimeoutMs));
      try Raw := '';
        repeat RecvLen := recv(Client, Buf, SizeOf(Buf), 0);
          if RecvLen > 0 then begin SetString(Chunk, PChar(@Buf[0]), RecvLen); Raw := Raw + Chunk; end;
          HeaderEnd := Pos(#13#10#13#10, Raw);
        until (RecvLen <= 0) or (HeaderEnd > 0);
        if HeaderEnd > 0 then begin
          Header := Copy(Raw, 1, HeaderEnd - 1); ContentLength := 0;
          CLPos := Pos('Content-Length:', Header); if CLPos = 0 then CLPos := Pos('content-length:', Header);
          if CLPos > 0 then begin CLPos := CLPos + Length('Content-Length:');
            while (CLPos <= Length(Header)) and (Header[CLPos] in [' ', #9]) do Inc(CLPos);
            LineEnd := PosExSimple(#13#10, Header, CLPos); if LineEnd = 0 then LineEnd := Length(Header) + 1;
            ContentLength := StrToIntDef(Trim(Copy(Header, CLPos, LineEnd - CLPos)), 0); end;
          BodyLen := Length(Raw) - (HeaderEnd + 3); NeedLen := ContentLength - BodyLen;
          while (NeedLen > 0) do begin RecvLen := recv(Client, Buf, SizeOf(Buf), 0);
            if RecvLen <= 0 then Break; SetString(Chunk, PChar(@Buf[0]), RecvLen); Raw := Raw + Chunk; Dec(NeedLen, RecvLen); end;
          BodyUtf8 := Copy(Raw, HeaderEnd + 4, ContentLength);
          FOwner.DoLog('[信息] [终端回调] 收到终端回调，body_size=' + IntToStr(Length(BodyUtf8)));
          ResponseBody := FOwner.HandleTerminalCallback(BodyUtf8); end
        else ResponseBody := '{"status":"rejected"}';
        Response := 'HTTP/1.1 202 Accepted'#13#10 + 'Content-Type: application/json; charset=utf-8'#13#10 +
          'Content-Length: ' + IntToStr(Length(ResponseBody)) + #13#10 + 'Connection: close'#13#10#13#10 + ResponseBody;
        send(Client, Response[1], Length(Response), 0);
      finally closesocket(Client); end;
    end;
  finally if FListenSocket <> INVALID_SOCKET then closesocket(FListenSocket); FListenSocket := INVALID_SOCKET; WSACleanup; end;
end;

// ============================================================
// TDelphiHttpServerThread (configured DLL communication endpoint)
// ============================================================
constructor TDelphiHttpServerThread.Create(AOwner: TDelphiProxyServer);
begin inherited Create(True); FreeOnTerminate := False; FOwner := AOwner; FListenSocket := INVALID_SOCKET; end;

procedure TDelphiHttpServerThread.StopServer;
begin Terminate; if FListenSocket <> INVALID_SOCKET then begin closesocket(FListenSocket); FListenSocket := INVALID_SOCKET; end; end;

procedure TDelphiHttpServerThread.Execute;
var
  WSA: TWSAData;
  Addr: TSockAddrIn;
  Client: TSocket;
  TimeoutMs: Integer;
  ResponseBody, Response: string;
begin
  if WSAStartup($0202, WSA) <> 0 then Exit;
  try
    FListenSocket := socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if FListenSocket = INVALID_SOCKET then Exit;
    FillChar(Addr, SizeOf(Addr), 0);
    Addr.sin_family := AF_INET;
    Addr.sin_addr.S_addr := inet_addr(PChar(FOwner.FDelphiServerHost));
    Addr.sin_port := htons(FOwner.FDelphiServerPort);
    if bind(FListenSocket, Addr, SizeOf(Addr)) <> 0 then begin
      FOwner.DoLog('[错误] [服务] DLL通信服务启动失败：bind ' + FOwner.FDelphiServerHost + ':' +
        IntToStr(FOwner.FDelphiServerPort) + ' ，error=' + IntToStr(WSAGetLastError) + '，');
      Exit;
    end;
    if listen(FListenSocket, SOMAXCONN) <> 0 then begin
      FOwner.DoLog('[错误] [服务] DLL通信服务启动失败：listen ' + FOwner.FDelphiServerHost + ':' +
        IntToStr(FOwner.FDelphiServerPort) + ' ，error=' + IntToStr(WSAGetLastError) + '，');
      Exit;
    end;
    FOwner.DoLog('[信息] [服务] DLL通信服务程序已启动，http://' + FOwner.FDelphiServerHost + ':' +
      IntToStr(FOwner.FDelphiServerPort));
    while not Terminated do begin
      Client := accept(FListenSocket, nil, nil);
      if Client = INVALID_SOCKET then begin
        if Terminated then Break;
        Continue;
      end;
      TimeoutMs := HTTP_SOCKET_TIMEOUT_MS;
      setsockopt(Client, SOL_SOCKET, SO_RCVTIMEO, PChar(@TimeoutMs), SizeOf(TimeoutMs));
      setsockopt(Client, SOL_SOCKET, SO_SNDTIMEO, PChar(@TimeoutMs), SizeOf(TimeoutMs));
      if not FOwner.EnqueueHttpClient(Client) then begin
        ResponseBody := '{"error":true,"code":"server_busy"}';
        Response := 'HTTP/1.1 503 Service Unavailable'#13#10 + 'Content-Type: application/json; charset=utf-8'#13#10 +
          'Content-Length: ' + IntToStr(Length(ResponseBody)) + #13#10 + 'Connection: close'#13#10#13#10 + ResponseBody;
        send(Client, Response[1], Length(Response), 0);
        closesocket(Client);
        FOwner.DoLog('[警告] [DLL请求] HTTP工作队列已满，已拒绝新请求，防止请求堆积。');
      end;
    end;
  finally
    if FListenSocket <> INVALID_SOCKET then closesocket(FListenSocket);
    FListenSocket := INVALID_SOCKET;
    WSACleanup;
  end;
end;

// ============================================================
// THttpClientWorker
// ============================================================
constructor THttpClientWorker.Create(AOwner: TDelphiProxyServer; WorkerIndex: Integer);
begin
  inherited Create(True);
  FreeOnTerminate := False;
  FOwner := AOwner;
  FWorkerIndex := WorkerIndex;
end;

procedure THttpClientWorker.Execute;
var
  Client: TSocket;
begin
  while not Terminated do begin
    Client := FOwner.PopHttpClient;
    if Client = INVALID_SOCKET then begin
      FOwner.FHttpQueueEvent.WaitFor(500);
      Continue;
    end;
    FOwner.ProcessHttpClient(Client);
  end;
end;

procedure TDelphiProxyServer.StartHttpWorkers;
var
  I: Integer;
begin
  FHttpQueueLock.Acquire;
  try
    FHttpStopping := False;
  finally
    FHttpQueueLock.Release;
  end;
  for I := Low(FHttpWorkers) to High(FHttpWorkers) do begin
    if FHttpWorkers[I] = nil then begin
      FHttpWorkers[I] := THttpClientWorker.Create(Self, I + 1);
      FHttpWorkers[I].Resume;
    end;
  end;
  DoLog('[信息] [DLL请求] HTTP固定工作线程已启动，线程数=' + IntToStr(High(FHttpWorkers) - Low(FHttpWorkers) + 1) +
    '，队列上限=' + IntToStr(HTTP_QUEUE_LIMIT));
end;

procedure TDelphiProxyServer.StopHttpWorkers;
var
  I: Integer;
  Client: TSocket;
begin
  FHttpQueueLock.Acquire;
  try
    FHttpStopping := True;
    while FHttpQueue.Count > 0 do begin
      Client := TSocket(FHttpQueue[0]);
      FHttpQueue.Delete(0);
      if Client <> INVALID_SOCKET then closesocket(Client);
    end;
  finally
    FHttpQueueLock.Release;
  end;
  for I := Low(FHttpWorkers) to High(FHttpWorkers) do begin
    if FHttpWorkers[I] <> nil then begin
      FHttpWorkers[I].Terminate;
      FHttpQueueEvent.SetEvent;
    end;
  end;
  for I := Low(FHttpWorkers) to High(FHttpWorkers) do begin
    if FHttpWorkers[I] <> nil then begin
      FHttpWorkers[I].WaitFor;
      FHttpWorkers[I].Free;
      FHttpWorkers[I] := nil;
    end;
  end;
end;

function TDelphiProxyServer.EnqueueHttpClient(Client: TSocket): Boolean;
begin
  Result := False;
  FHttpQueueLock.Acquire;
  try
    if FHttpStopping then Exit;
    if FHttpQueue.Count >= HTTP_QUEUE_LIMIT then Exit;
    FHttpQueue.Add(Pointer(Client));
    Result := True;
    FHttpQueueEvent.SetEvent;
  finally
    FHttpQueueLock.Release;
  end;
end;

function TDelphiProxyServer.PopHttpClient: TSocket;
begin
  Result := INVALID_SOCKET;
  FHttpQueueLock.Acquire;
  try
    if FHttpQueue.Count > 0 then begin
      Result := TSocket(FHttpQueue[0]);
      FHttpQueue.Delete(0);
    end;
  finally
    FHttpQueueLock.Release;
  end;
end;

procedure TDelphiProxyServer.ProcessHttpClient(Client: TSocket);
var
  Buf: array[0..8191] of Char;
  RecvLen, HeaderEnd, ContentLength, BodyLen, NeedLen: Integer;
  Raw, Header, Method, Path, BodyUtf8, ResponseBody, Response, Chunk: string;
  P1, P2, CLPos, LineEnd: Integer;
begin
  try
    try
      Raw := '';
      HeaderEnd := 0;
      repeat
        RecvLen := recv(Client, Buf, SizeOf(Buf), 0);
        if RecvLen > 0 then begin
          SetString(Chunk, PChar(@Buf[0]), RecvLen);
          Raw := Raw + Chunk;
        end;
        HeaderEnd := Pos(#13#10#13#10, Raw);
      until (RecvLen <= 0) or (HeaderEnd > 0);
      if HeaderEnd > 0 then begin
        Header := Copy(Raw, 1, HeaderEnd - 1);
        ContentLength := 0;
        CLPos := Pos('Content-Length:', Header);
        if CLPos = 0 then CLPos := Pos('content-length:', Header);
        if CLPos > 0 then begin
          CLPos := CLPos + Length('Content-Length:');
          while (CLPos <= Length(Header)) and (Header[CLPos] in [' ', #9]) do Inc(CLPos);
          LineEnd := PosExSimple(#13#10, Header, CLPos);
          if LineEnd = 0 then LineEnd := Length(Header) + 1;
          ContentLength := StrToIntDef(Trim(Copy(Header, CLPos, LineEnd - CLPos)), 0);
        end;
        BodyLen := Length(Raw) - (HeaderEnd + 3);
        NeedLen := ContentLength - BodyLen;
        while NeedLen > 0 do begin
          RecvLen := recv(Client, Buf, SizeOf(Buf), 0);
          if RecvLen <= 0 then Break;
          SetString(Chunk, PChar(@Buf[0]), RecvLen);
          Raw := Raw + Chunk;
          Dec(NeedLen, RecvLen);
        end;
        P1 := Pos(' ', Header);
        P2 := PosExSimple(' ', Header, P1 + 1);
        if (P1 <= 0) or (P2 <= P1) then begin
          ResponseBody := '{"error":true,"code":"bad_request"}';
        end else begin
          Method := Copy(Header, 1, P1 - 1);
          Path := Copy(Header, P1 + 1, P2 - P1 - 1);
          BodyUtf8 := Copy(Raw, HeaderEnd + 4, ContentLength);
          DoLog('[信息] [DLL请求] 收到DLL下发请求：' + Method + ' ' + Path);
          ResponseBody := HandleRequest(Method, Path, BodyUtf8);
        end;
      end else begin
        ResponseBody := '{"error":true,"code":"bad_request"}';
      end;
      Response := 'HTTP/1.1 200 OK'#13#10 + 'Content-Type: application/json; charset=utf-8'#13#10 +
        'Content-Length: ' + IntToStr(Length(ResponseBody)) + #13#10 + 'Connection: close'#13#10#13#10 + ResponseBody;
      send(Client, Response[1], Length(Response), 0);
    except
      on E: Exception do begin
        DoLog('[错误] [DLL请求] HTTP工作线程处理请求异常：' + E.Message);
        ResponseBody := '{"error":true,"code":"internal_error"}';
        Response := 'HTTP/1.1 500 Internal Server Error'#13#10 + 'Content-Type: application/json; charset=utf-8'#13#10 +
          'Content-Length: ' + IntToStr(Length(ResponseBody)) + #13#10 + 'Connection: close'#13#10#13#10 + ResponseBody;
        send(Client, Response[1], Length(Response), 0);
      end;
    end;
  finally
    closesocket(Client);
  end;
end;

// ============================================================
// TProxyTaskWorker
// ============================================================
constructor TProxyQueuedTask.Create(const AMethod, APath, ABodyUtf8: string; AGeneration: Integer; AWaitForResponse: Boolean);
begin
  inherited Create;
  Method := AMethod;
  Path := APath;
  BodyUtf8 := ABodyUtf8;
  Response := '';
  Generation := AGeneration;
  WaitForResponse := AWaitForResponse;
  CreatedTick := GetTickCount;
  StateLock := TCriticalSection.Create;
  DoneEvent := TEvent.Create(nil, True, False, '');
end;

destructor TProxyQueuedTask.Destroy;
begin
  DoneEvent.Free;
  StateLock.Free;
  inherited Destroy;
end;

constructor TProxyTaskWorker.Create(AOwner: TDelphiProxyServer; const WorkerName: string; MaxQueue: Integer);
begin
  inherited Create(True);
  FreeOnTerminate := False;
  FOwner := AOwner;
  FWorkerName := WorkerName;
  FMaxQueue := MaxQueue;
  FQueue := TList.Create;
  FLock := TCriticalSection.Create;
  FEvent := TEvent.Create(nil, False, False, '');
end;

destructor TProxyTaskWorker.Destroy;
begin
  StopWorker;
  FEvent.Free;
  FLock.Free;
  FQueue.Free;
  inherited Destroy;
end;

function TProxyTaskWorker.Enqueue(Task: TProxyQueuedTask): Boolean;
var
  OldTask: TProxyQueuedTask;
  FreeOldTask: Boolean;
  NotifyOldTask: Boolean;
begin
  Result := False;
  if Task = nil then Exit;
  FLock.Acquire;
  try
    if Terminated then Exit;
    while FQueue.Count >= FMaxQueue do begin
      OldTask := TProxyQueuedTask(FQueue[0]);
      FQueue.Delete(0);
      if OldTask <> nil then begin
        FreeOldTask := False;
        NotifyOldTask := False;
        OldTask.StateLock.Acquire;
        try
          OldTask.Response := '{"error":true,"code":"request_replaced"}';
          FOwner.DoLog('[警告] [业务队列] 新请求已替换等待中的旧请求：队列=' + FWorkerName + '，old_path=' + OldTask.Path + '，new_path=' + Task.Path);
          if OldTask.WaitForResponse then
            NotifyOldTask := True
          else
            FreeOldTask := True;
        finally
          OldTask.StateLock.Release;
        end;
        if NotifyOldTask then
          OldTask.DoneEvent.SetEvent
        else if FreeOldTask then
          OldTask.Free;
      end;
    end;
    FQueue.Add(Task);
    Result := True;
    FEvent.SetEvent;
  finally
    FLock.Release;
  end;
end;

function TProxyTaskWorker.PopTask: TProxyQueuedTask;
begin
  Result := nil;
  FLock.Acquire;
  try
    if FQueue.Count > 0 then begin
      Result := TProxyQueuedTask(FQueue[0]);
      FQueue.Delete(0);
    end;
  finally
    FLock.Release;
  end;
end;

procedure TProxyTaskWorker.StopWorker;
var
  Task: TProxyQueuedTask;
  FreeTask: Boolean;
  NotifyTask: Boolean;
begin
  Terminate;
  FEvent.SetEvent;
  FLock.Acquire;
  try
    while FQueue.Count > 0 do begin
      Task := TProxyQueuedTask(FQueue[0]);
      FQueue.Delete(0);
      if Task <> nil then begin
        FreeTask := False;
        NotifyTask := False;
        Task.StateLock.Acquire;
        try
          Task.Response := '{"error":true,"code":"service_stopping"}';
          if Task.WaitForResponse then
            NotifyTask := True
          else
            FreeTask := True;
        finally
          Task.StateLock.Release;
        end;
        if NotifyTask then
          Task.DoneEvent.SetEvent
        else if FreeTask then
          Task.Free;
      end;
    end;
  finally
    FLock.Release;
  end;
end;

procedure TProxyTaskWorker.Execute;
var
  Task: TProxyQueuedTask;
  FreeTask: Boolean;
  NotifyTask: Boolean;
begin
  while not Terminated do begin
    Task := PopTask;
    if Task = nil then begin
      FEvent.WaitFor(500);
      Continue;
    end;
    try
      if Task.Generation <> FOwner.CurrentSwitchGeneration then begin
        Task.Response := '{"error":true,"code":"stale_request"}';
        FOwner.DoLog('[警告] [业务队列] 丢弃旧终端请求：队列=' + FWorkerName + '，path=' + Task.Path);
      end else begin
        Task.Response := FOwner.HandleRequestDirect(Task.Method, Task.Path, Task.BodyUtf8);
      end;
    except
      on E: Exception do begin
        Task.Response := '{"error":true,"code":"worker_exception"}';
        FOwner.DoLog('[错误] [业务队列] 工作线程异常：队列=' + FWorkerName + '，错误=' + E.Message);
      end;
    end;
    FreeTask := False;
    NotifyTask := False;
    Task.StateLock.Acquire;
    try
      if Task.WaitForResponse then
        NotifyTask := True
      else
        FreeTask := True;
    finally
      Task.StateLock.Release;
    end;
    if NotifyTask then
      Task.DoneEvent.SetEvent
    else if FreeTask then
      Task.Free;
  end;
end;
// ============================================================
// TAsyncSwitchThread
// ============================================================
constructor TAsyncSwitchThread.Create(AOwner: TDelphiProxyServer; TerminalIndex, SwitchGeneration: Integer);
begin
  inherited Create(False);
  FreeOnTerminate := True;
  FOwner := AOwner;
  FTerminalIndex := TerminalIndex;
  FSwitchGeneration := SwitchGeneration;
end;

procedure TAsyncSwitchThread.Execute;
begin
  FOwner.ExecuteTerminalSwitch(FTerminalIndex, FSwitchGeneration);
end;

// ============================================================
// TAsyncStopPreviewThread
// ============================================================
constructor TAsyncStopPreviewThread.Create(AOwner: TDelphiProxyServer;
  ResType: TPreviewResourceType; SessionType: TPreviewSessionType);
begin
  inherited Create(False);
  FreeOnTerminate := True;
  FOwner := AOwner;
  FResType := ResType;
  FSessionType := SessionType;
end;

procedure TAsyncStopPreviewThread.Execute;
begin
  FOwner.FPreviewManager.StopPreview(FResType, FSessionType);
end;


// ============================================================
// TAsyncStartPreviewThread
// ============================================================
constructor TAsyncStartPreviewThread.Create(AOwner: TDelphiProxyServer;
  ResType: TPreviewResourceType; SessionType: TPreviewSessionType;
  Hwnd: HWND; const TerminalBaseUrl, RequestId, CallbackUrl: string;
  SwitchGeneration: Integer);
begin
  inherited Create(False);
  FreeOnTerminate := True;
  FOwner := AOwner;
  FResType := ResType;
  FSessionType := SessionType;
  FHwnd := Hwnd;
  FTerminalBaseUrl := TerminalBaseUrl;
  FRequestId := RequestId;
  FCallbackUrl := CallbackUrl;
  FSwitchGeneration := SwitchGeneration;
end;

procedure TAsyncStartPreviewThread.Execute;
var
  ResourceType: string;
  PayloadUtf8: string;
begin
  case FResType of
    prtCamera: ResourceType := 'face_image';
    prtFingerprint: ResourceType := 'fingerprint_image';
    prtIris: ResourceType := 'iris_image';
    else ResourceType := 'unknown';
  end;
  if not FOwner.IsSwitchGenerationCurrent(FSwitchGeneration) then Exit;
  if FOwner.FPreviewManager.StartPreview(FResType, FSessionType, FHwnd, FTerminalBaseUrl) then
  begin
    if not FOwner.IsSwitchGenerationCurrent(FSwitchGeneration) then
    begin
      FOwner.FPreviewManager.StopPreview(FResType, FSessionType);
      Exit;
    end;
    FOwner.DoLog('[信息] [异步预览] 已开始: resource=' + ResourceType +
      ', request_id=' + FRequestId);
    PayloadUtf8 := '{' +
      JsonStr('request_id', FRequestId) + ',' +
      JsonStr('resource_type', ResourceType) + ',' +
      JsonInt('render_hwnd', FOwner.FPreviewManager.GetRenderHwnd(FResType, FSessionType)) + ',' +
      JsonInt('delphi_host_hwnd', FOwner.FPreviewManager.GetDefaultHostHwnd(FResType)) + '}';
    if FOwner.MakeCallback(FRequestId, FCallbackUrl, PayloadUtf8) then
    begin
      FOwner.MarkCompletedCallback(FRequestId);
      FOwner.ForgetRequestContext(FRequestId);
      FOwner.DoLog('[信息] [异步预览] 外部预览已成功回调第三方，通知主窗口最小化到任务栏。');
      FOwner.NotifyExternalPreviewReady;
    end;
  end
  else
  begin
    if not FOwner.IsSwitchGenerationCurrent(FSwitchGeneration) then Exit;
    FOwner.DoLog('[错误] [异步预览] 失败: resource=' + ResourceType +
      ', request_id=' + FRequestId);
    Exclude(FOwner.FExternalActivePreviews, FResType);
    case FResType of
      prtCamera: FOwner.FThirdPartyCameraHwnd := 0;
      prtFingerprint: FOwner.FThirdPartyFingerprintHwnd := 0;
      prtIris: FOwner.FThirdPartyIrisHwnd := 0;
    end;
    PayloadUtf8 := '{' +
      JsonStr('request_id', FRequestId) + ',' +
      JsonStr('resource_type', ResourceType) + ',' +
      JsonInt('render_hwnd', FHwnd) + ',' +
      '"error":true,"code":"preview_failed"}';
    if FOwner.MakeCallback(FRequestId, FCallbackUrl, PayloadUtf8) then
    begin
      FOwner.MarkCompletedCallback(FRequestId);
      FOwner.ForgetRequestContext(FRequestId);
    end;
  end;
end;

// ============================================================
// TVlcWarmupThread
// ============================================================
constructor TVlcWarmupThread.Create(AOwner: TDelphiProxyServer);
begin
  inherited Create(False);
  FreeOnTerminate := True;
  FOwner := AOwner;
end;

procedure TVlcWarmupThread.Execute;
var
  Vlc: TVlcPlayer;
begin
  FOwner.DoLog('[信息] [VLC预热] 正在启动...');
  Vlc := TVlcPlayer.Create;
  try
    Vlc.Warmup;
    FOwner.DoLog('[信息] [VLC预热] 已完成, 耗时=' + IntToStr(Vlc.WarmupMs) + 'ms');
  finally
    Vlc.Free;
  end;
end;
// TDelphiProxyServer - Core
// ============================================================
constructor TDelphiProxyServer.Create(ACameraPanel, AFingerprintPanel, AIrisPanel: TPanel);
begin inherited Create;
  FTerminalManager := TTerminalManager.Create; FTerminalClient := TTerminalClient.Create;
  FCallbackParser := TCallbackParser.Create; FFileSaver := TFileSaver.Create;
  FPreviewManager := TPreviewManager.Create(ACameraPanel, AFingerprintPanel, AIrisPanel);
  FRequestSaveDirs := TStringList.Create; FRequestCallbacks := TStringList.Create;
  FRequestGenerations := TStringList.Create;
  FCompletedCallbacks := TStringList.Create;
  FHttpQueue := TList.Create;
  FHttpQueueLock := TCriticalSection.Create;
  FHttpQueueEvent := TEvent.Create(nil, False, False, '');
  FHttpStopping := True;
  FillChar(FHttpWorkers, SizeOf(FHttpWorkers), 0);
  FFaceCaptureWorker := nil;
  FFingerprintCaptureWorker := nil;
  FOcrWorker := nil;
  FNfcWorker := nil;
  InitializeCriticalSection(FStateLock);
  FSwitchingTerminal := False; FSwitchGeneration := 0; FLastSwitchTick := 0;
  FThread := nil; FCallbackThread := nil; FLogProc := nil; FExternalPreviewReadyProc := nil; FLanIp := '127.0.0.1';
  FLocalActivePreviews := [];
  FExternalActivePreviews := [];
  LoadRuntimeConfig;
end;

destructor TDelphiProxyServer.Destroy;
begin Stop; FRequestSaveDirs.Free; FRequestCallbacks.Free; FRequestGenerations.Free; FCompletedCallbacks.Free; FHttpQueueEvent.Free; FHttpQueueLock.Free; FHttpQueue.Free; DeleteCriticalSection(FStateLock); FPreviewManager.Free;
  FFileSaver.Free; FCallbackParser.Free; FTerminalClient.Free; FTerminalManager.Free; inherited Destroy; end;

procedure TDelphiProxyServer.SetLogProc(ALogProc: TLogCallback);
begin FLogProc := ALogProc; FPreviewManager.SetLogProc(ALogProc); end;

procedure TDelphiProxyServer.SetExternalPreviewReadyProc(AProc: TPreviewReadyCallback);
begin FExternalPreviewReadyProc := AProc; end;

procedure TDelphiProxyServer.NotifyExternalPreviewReady;
begin
  if Assigned(FExternalPreviewReadyProc) then
    FExternalPreviewReadyProc;
end;

procedure TDelphiProxyServer.DoLog(const Msg: string);
begin if Assigned(FLogProc) then FLogProc(Msg); end;

function TDelphiProxyServer.GenRequestId(const Prefix: string): string;
begin Result := Prefix + '_' + FormatDateTime('yyyymmddhhnnsszzz', Now); end;

procedure TDelphiProxyServer.Start;
begin if FThread <> nil then Exit;
  FLanIp := GetLocalLanIp;
  FThirdPartyCameraHwnd := 0; FThirdPartyFingerprintHwnd := 0; FThirdPartyIrisHwnd := 0;
  DoLog('[信息] [服务] 已检测本机局域网地址：' + FLanIp);
  DoLog('[信息] [终端状态] 当前终端：' + FTerminalManager.CurrentName + ' ' + FTerminalManager.CurrentBaseUrl);
  FCallbackThread := TCallbackReceiverThread.Create(Self); FCallbackThread.Resume;
  StartHttpWorkers;
  StartBusinessWorkers;
  DoLog('[信息] [服务] 正在启动DLL通信服务，http://' + FDelphiServerHost + ':' + IntToStr(FDelphiServerPort));
  FThread := TDelphiHttpServerThread.Create(Self); FThread.Resume;
  DoLog('[信息] [服务] DLL通信终端回调=' + GetCallbackBase);
  DoLog('[信息] [预览管理] 服务程序已启动，等待外部预览指令中...'); TVlcWarmupThread.Create(Self); end;

procedure TDelphiProxyServer.Stop;
begin if FThread <> nil then begin FThread.StopServer; FThread.WaitFor; FThread.Free; FThread := nil; end;
  StopHttpWorkers;
  StopBusinessWorkers;
  if FCallbackThread <> nil then begin FCallbackThread.StopServer; FCallbackThread.WaitFor; FCallbackThread.Free; FCallbackThread := nil; end;
  FPreviewManager.StopPreview(prtCamera, pstLocal); FPreviewManager.StopPreview(prtFingerprint, pstLocal); FPreviewManager.StopPreview(prtIris, pstLocal);
  FPreviewManager.StopPreview(prtCamera, pstExternal); FPreviewManager.StopPreview(prtFingerprint, pstExternal); FPreviewManager.StopPreview(prtIris, pstExternal);
  FLocalActivePreviews := []; FExternalActivePreviews := []; DoLog('[信息] [服务] 服务程序已停止。'); end;

function TDelphiProxyServer.MakeCallback(const RequestId, DllCallbackUrl, PayloadUtf8: string): Boolean;
begin
  Result := False;
  if DllCallbackUrl = '' then Exit;
  DoLog('[信息] [DLL回调] 正在向DLL回传异步结果，url=' + DllCallbackUrl +
    '，body_size=' + IntToStr(Length(PayloadUtf8)));
  Result := HttpPostJson(DllCallbackUrl, PayloadUtf8);
  if Result then
    DoLog('[信息] [DLL回调] DLL回调成功，request_id=' + RequestId)
  else
    DoLog('[错误] [DLL回调] DLL回调失败，request_id=' + RequestId + '，url=' + DllCallbackUrl);
end;

function TDelphiProxyServer.BeginTerminalSwitch(out SwitchGeneration: Integer): Boolean;
begin
  Result := False;
  SwitchGeneration := 0;
  EnterCriticalSection(FStateLock);
  try
    if FSwitchingTerminal then begin
      SwitchGeneration := FSwitchGeneration;
      Exit;
    end;
    Inc(FSwitchGeneration);
    FSwitchingTerminal := True;
    FLastSwitchTick := GetTickCount;
    SwitchGeneration := FSwitchGeneration;
    Result := True;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.EndTerminalSwitch(SwitchGeneration: Integer);
begin
  EnterCriticalSection(FStateLock);
  try
    if FSwitchGeneration = SwitchGeneration then begin
      FSwitchingTerminal := False;
      FLastSwitchTick := GetTickCount;
    end;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

function TDelphiProxyServer.IsTerminalSwitching: Boolean;
begin
  EnterCriticalSection(FStateLock);
  try
    Result := FSwitchingTerminal;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

function TDelphiProxyServer.CurrentSwitchGeneration: Integer;
begin
  EnterCriticalSection(FStateLock);
  try
    Result := FSwitchGeneration;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

function TDelphiProxyServer.IsSwitchGenerationCurrent(SwitchGeneration: Integer): Boolean;
begin
  EnterCriticalSection(FStateLock);
  try
    Result := FSwitchGeneration = SwitchGeneration;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

function TDelphiProxyServer.ShouldDropUnknownCallback(const RequestId: string): Boolean;
begin
  Result := False;
  if RequestId = '' then Exit;
  EnterCriticalSection(FStateLock);
  try
    Result := (FRequestGenerations.IndexOfName(RequestId) < 0) and
      (FLastSwitchTick <> 0) and (Integer(GetTickCount - FLastSwitchTick) < 30000);
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.RememberRequestContext(const RequestId, SaveDir, CallbackUrl: string);
var ResolvedSaveDir: string;
begin
  if RequestId = '' then Exit;
  ResolvedSaveDir := SafeResolveSaveDir(SaveDir);
  EnterCriticalSection(FStateLock);
  try
    FRequestSaveDirs.Values[RequestId] := ResolvedSaveDir;
    FRequestCallbacks.Values[RequestId] := CallbackUrl;
    FRequestGenerations.Values[RequestId] := IntToStr(FSwitchGeneration);
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.GetRequestContext(const RequestId: string; out SaveDir, CallbackUrl: string; out RequestGeneration: Integer);
begin
  SaveDir := '';
  CallbackUrl := '';
  RequestGeneration := -1;
  if RequestId = '' then Exit;
  EnterCriticalSection(FStateLock);
  try
    SaveDir := FRequestSaveDirs.Values[RequestId];
    CallbackUrl := FRequestCallbacks.Values[RequestId];
    RequestGeneration := StrToIntDef(FRequestGenerations.Values[RequestId], -1);
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.ForgetRequestContext(const RequestId: string);
var I: Integer;
begin
  if RequestId = '' then Exit;
  EnterCriticalSection(FStateLock);
  try
    I := FRequestSaveDirs.IndexOfName(RequestId); if I >= 0 then FRequestSaveDirs.Delete(I);
    I := FRequestCallbacks.IndexOfName(RequestId); if I >= 0 then FRequestCallbacks.Delete(I);
    I := FRequestGenerations.IndexOfName(RequestId); if I >= 0 then FRequestGenerations.Delete(I);
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.ClearRequestContexts;
begin
  EnterCriticalSection(FStateLock);
  try
    FRequestSaveDirs.Clear;
    FRequestCallbacks.Clear;
    FRequestGenerations.Clear;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.PruneCompletedCallbacks;
var I: Integer; TickNow, TickSaved: DWORD;
begin
  TickNow := GetTickCount;
  EnterCriticalSection(FStateLock);
  try
    for I := FCompletedCallbacks.Count - 1 downto 0 do
    begin
      TickSaved := DWORD(StrToInt64Def(FCompletedCallbacks.ValueFromIndex[I], 0));
      if (Integer(TickNow - TickSaved) > 600000) or (FCompletedCallbacks.Count > 1024) then
        FCompletedCallbacks.Delete(I);
    end;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

procedure TDelphiProxyServer.MarkCompletedCallback(const RequestId: string);
begin
  if RequestId = '' then Exit;
  EnterCriticalSection(FStateLock);
  try
    FCompletedCallbacks.Values[RequestId] := IntToStr(GetTickCount);
  finally
    LeaveCriticalSection(FStateLock);
  end;
  PruneCompletedCallbacks;
end;

function TDelphiProxyServer.IsCompletedCallback(const RequestId: string): Boolean;
begin
  Result := False;
  if RequestId = '' then Exit;
  PruneCompletedCallbacks;
  EnterCriticalSection(FStateLock);
  try
    Result := FCompletedCallbacks.IndexOfName(RequestId) >= 0;
  finally
    LeaveCriticalSection(FStateLock);
  end;
end;

// ============================================================
// AUTO PREVIEWS
// ============================================================
procedure TDelphiProxyServer.AutoStopPreviews;
var
  SessionType: TPreviewSessionType;
  ResType: TPreviewResourceType;
  ActivePreviews: TPreviewResourceSet;
  TotalTick, ItemTick: DWORD;
begin
  TotalTick := GetTickCount;
  DoLog('[信息] [终端切换] 切换前正在停止活动预览...');
  for SessionType := pstLocal to pstExternal do
  begin
    if SessionType = pstLocal then
    begin
      ActivePreviews := FLocalActivePreviews;
    end
    else
    begin
      ActivePreviews := FExternalActivePreviews;
    end;
    for ResType := High(TPreviewResourceType) downto Low(TPreviewResourceType) do
      if ResType in ActivePreviews then
      begin
        ItemTick := GetTickCount;
        FPreviewManager.StopPreview(ResType, SessionType);
        DoLog(Format('[性能] 停止预览 resource=%d session=%d 耗时=%d毫秒',
          [Ord(ResType), Ord(SessionType), Integer(GetTickCount - ItemTick)]));
      end;
  end;
  DoLog(Format('[性能] 停止全部预览 耗时=%d毫秒', [Integer(GetTickCount - TotalTick)]));
end;
procedure TDelphiProxyServer.AutoStartPreviews;
var
  SessionType: TPreviewSessionType;
  ResType: TPreviewResourceType;
  ActivePreviews: TPreviewResourceSet;
  TargetHwnd: HWND;
  TotalTick, ItemTick: DWORD;
begin
  TotalTick := GetTickCount;
  DoLog(Format('[信息] [终端切换] 正在终端%d上恢复活动预览', [FTerminalManager.CurrentIndex]));
  for SessionType := pstLocal to pstExternal do
  begin
    if SessionType = pstLocal then
    begin
      ActivePreviews := FLocalActivePreviews;
    end
    else
    begin
      ActivePreviews := FExternalActivePreviews;
    end;
    for ResType := Low(TPreviewResourceType) to High(TPreviewResourceType) do
      if ResType in ActivePreviews then
      begin
        TargetHwnd := 0;
        if SessionType = pstExternal then
          case ResType of
            prtCamera: TargetHwnd := FThirdPartyCameraHwnd;
            prtFingerprint: TargetHwnd := FThirdPartyFingerprintHwnd;
            prtIris: TargetHwnd := FThirdPartyIrisHwnd;
          end;
        ItemTick := GetTickCount;
        FPreviewManager.StartPreview(ResType, SessionType, TargetHwnd,
          FTerminalManager.CurrentBaseUrl);
        DoLog(Format('[性能] 启动预览 resource=%d session=%d 耗时=%d毫秒',
          [Ord(ResType), Ord(SessionType), Integer(GetTickCount - ItemTick)]));
      end;
  end;
  DoLog(Format('[性能] 启动全部预览 耗时=%d毫秒', [Integer(GetTickCount - TotalTick)]));
end;
// ============================================================
// DIRECT METHODS
// ============================================================
function TDelphiProxyServer.ExecuteTerminalSwitch(Index, SwitchGeneration: Integer): Boolean;
var
  TotalTick, PhaseTick: DWORD;
begin
  Result := False;
  TotalTick := GetTickCount;
  try
    ClearRequestContexts;
    PhaseTick := GetTickCount;
    AutoStopPreviews;
    DoLog(Format('[PERF] terminal switch stop previews cost=%dms', [Integer(GetTickCount - PhaseTick)]));
    PhaseTick := GetTickCount;
    FTerminalManager.SwitchTo(Index);
    DoLog(Format('[PERF] terminal manager switch cost=%dms', [Integer(GetTickCount - PhaseTick)]));
    PhaseTick := GetTickCount;
    AutoStartPreviews;
    DoLog(Format('[PERF] terminal switch restart previews cost=%dms', [Integer(GetTickCount - PhaseTick)]));
    DoLog(Format('[PERF] terminal switch total cost=%dms', [Integer(GetTickCount - TotalTick)]));
    Result := True;
  except
    on E: Exception do
      DoLog('[错误] [终端切换] 后台切换异常：' + E.Message);
  end;
  EndTerminalSwitch(SwitchGeneration);
end;

function TDelphiProxyServer.StartTerminalSwitchAsync(Index: Integer): Boolean;
var
  SwitchGeneration: Integer;
begin
  Result := False;
  if (Index < 1) or (Index > 2) then Exit;
  if FTerminalManager.IsSameTerminal(Index) then
  begin
    Result := True;
    Exit;
  end;
  if not BeginTerminalSwitch(SwitchGeneration) then Exit;
  try
    TAsyncSwitchThread.Create(Self, Index, SwitchGeneration);
    Result := True;
    DoLog(Format('[信息] [终端切换] 已受理后台切换请求：terminal=%d，generation=%d', [Index, SwitchGeneration]));
  except
    on E: Exception do
    begin
      EndTerminalSwitch(SwitchGeneration);
      DoLog('[错误] [终端切换] 创建后台切换线程失败：' + E.Message);
    end;
  end;
end;

function TDelphiProxyServer.SwitchTerminalDirect(Index: Integer): Boolean;
var
  SwitchGeneration: Integer;
begin
  Result := False;
  if (Index < 1) or (Index > 2) then Exit;
  if FTerminalManager.IsSameTerminal(Index) then begin
    Result := True;
    Exit;
  end;
  if not BeginTerminalSwitch(SwitchGeneration) then Exit;
  Result := ExecuteTerminalSwitch(Index, SwitchGeneration);
end;
function TDelphiProxyServer.StartProcessDirect(const SaveDir: string): Boolean;
var BaseUrl, BodyUtf8, ResponseUtf8, ResolvedSaveDir: string;
begin
  Result := False;
  ResolvedSaveDir := SafeResolveSaveDir(SaveDir);
  BaseUrl := FTerminalManager.CurrentBaseUrl;
  BodyUtf8 := BuildTerminalProcessStartBody;
  FTerminalManager.ProcessSaveDir := ResolvedSaveDir;
  DoLog('[信息] [流程] 正在向终端开始流程，url=' + BaseUrl + '/process/start?，save_dir=' + ResolvedSaveDir);
  DoLog('[信息] [流程] 终端回调地址=' + GetCallbackBase);
  if not FTerminalClient.PostJson(BaseUrl, '/process/start', BodyUtf8, ResponseUtf8) then
  begin
    DoLog('[错误] [终端通信] 开始流程指令发送到终端失败。');
    Exit;
  end;
  FTerminalManager.ProcessActive := True;
  DoLog('[信息] [流程] 终端流程已开始，save_dir=' + FTerminalManager.ProcessSaveDir);
  Result := True;
end;

function TDelphiProxyServer.EndProcessDirect: Boolean;
begin FTerminalManager.ProcessActive := False; FTerminalManager.ProcessSaveDir := '';
  ClearRequestContexts; DoLog('[信息] [流程] 流程已结束。'); Result := True; end;

function TDelphiProxyServer.CaptureFaceDirect(const SaveDir: string; out SavePath: string): Boolean;
var BaseUrl, ReqId, ResponseUtf8: string; FaceResult: TImageCallbackResult;
begin Result := False; SavePath := ''; BaseUrl := FTerminalManager.CurrentBaseUrl; ReqId := GenRequestId('FACE');
  DoLog('[信息] [终端通信] 正在向终端请求人脸抓拍，request_id=' + ReqId + '，url=' + BaseUrl + '/resources/face-image/sync-request');
  if not FTerminalClient.PostJson(BaseUrl, '/resources/face-image/sync-request',
      '{"request_id":"' + ReqId + '"}', ResponseUtf8) then begin
    DoLog('[错误] [终端通信] 人脸抓拍指令发送失败，terminal=' + BaseUrl); Exit; end;
  DoLog('[信息] [人脸抓拍] 收到终端响应，response_size=' + IntToStr(Length(ResponseUtf8)));
  FaceResult := FCallbackParser.ParseImageCapture(ResponseUtf8);
  if not FaceResult.Valid then begin DoLog('[错误] [人脸抓拍] 响应解析失败。'); Exit; end;
  if FaceResult.RequestId = '' then FaceResult.RequestId := ReqId;
  if ExtractFileExt(SaveDir) <> '' then
    SavePath := FFileSaver.SaveBase64ImageToFile(FaceResult.ImageBase64, ResolveExactSaveFile(SaveDir))
  else
    SavePath := FFileSaver.SaveBase64Image(FaceResult.ImageBase64, FaceResult.ImageMimeType,
      SafeResolveSaveDir(SaveDir), FaceResult.RequestId, 'face');
  if SavePath = '' then begin DoLog('[错误] [人脸抓拍] 图片保存失败。'); Exit; end;
  DoLog('[信息] [人脸抓拍] 图片保存成功：' + SavePath); Result := True; end;

function TDelphiProxyServer.CaptureFingerprintDirect(const SaveDir: string; out SavePath: string): Boolean;
var BaseUrl, ReqId, ResponseUtf8: string; FpResult: TImageCallbackResult;
begin Result := False; SavePath := ''; BaseUrl := FTerminalManager.CurrentBaseUrl; ReqId := GenRequestId('FP');
  DoLog('[信息] [终端通信] 正在向终端请求指纹抓拍，request_id=' + ReqId + '，url=' + BaseUrl + '/resources/fingerprint/sync-request');
  if not FTerminalClient.PostJson(BaseUrl, '/resources/fingerprint/sync-request',
      '{"request_id":"' + ReqId + '"}', ResponseUtf8) then begin
    DoLog('[错误] [终端通信] 指纹抓拍指令发送失败，terminal=' + BaseUrl); Exit; end;
  FpResult := FCallbackParser.ParseImageCapture(ResponseUtf8);
  if not FpResult.Valid then begin DoLog('[错误] [指纹抓拍] 响应解析失败。'); Exit; end;
  if FpResult.RequestId = '' then FpResult.RequestId := ReqId;
  if ExtractFileExt(SaveDir) <> '' then
    SavePath := FFileSaver.SaveBase64ImageToFile(FpResult.ImageBase64, ResolveExactSaveFile(SaveDir))
  else
    SavePath := FFileSaver.SaveBase64Image(FpResult.ImageBase64, FpResult.ImageMimeType,
      SafeResolveSaveDir(SaveDir), FpResult.RequestId, 'fingerprint');
  if SavePath = '' then begin DoLog('[错误] [指纹抓拍] 图片保存失败。'); Exit; end;
  DoLog('[信息] [指纹抓拍] 图片保存成功：' + SavePath); Result := True; end;

function TDelphiProxyServer.RequestOCRDirect(const SaveDir: string): string;
var BaseUrl, ResponseUtf8, CallbackUrl: string;
begin Result := ''; BaseUrl := FTerminalManager.CurrentBaseUrl; Result := GenRequestId('OCR');
  CallbackUrl := GetCallbackBase;
  DoLog('[信息] [终端通信] 正在提交OCR识别，request_id=' + Result + '，callback=' + CallbackUrl);
  if not FTerminalClient.PostJson(BaseUrl, '/resources/ocr-document/request',
      '{"request_id":"' + Result + '","callback_url":"' + CallbackUrl + '"}', ResponseUtf8) then begin
    DoLog('[错误] [终端通信] OCR识别指令发送到终端失败。'); Result := ''; Exit; end;
  RememberRequestContext(Result, SaveDir, '');
  DoLog('[信息] [OCR识别] 正在等待终端回调...' + CallbackUrl); end;

function TDelphiProxyServer.RequestNfcDirect(const SaveDir: string): string;
var BaseUrl, ResponseUtf8, CallbackUrl: string;
begin Result := ''; BaseUrl := FTerminalManager.CurrentBaseUrl; Result := GenRequestId('NFC');
  CallbackUrl := GetCallbackBase;
  DoLog('[信息] [终端通信] 正在提交IC卡识别，request_id=' + Result + '，callback=' + CallbackUrl);
  if not FTerminalClient.PostJson(BaseUrl, '/resources/nfc-card/request',
      '{"request_id":"' + Result + '","callback_url":"' + CallbackUrl + '"}', ResponseUtf8) then begin
    DoLog('[错误] [终端通信] IC卡识别指令发送到终端失败。'); Result := ''; Exit; end;
  RememberRequestContext(Result, SaveDir, '');
  DoLog('[信息] [IC卡识别] 正在等待刷卡回调...'); end;

function TDelphiProxyServer.CaptureIrisDirect(const SaveDir: string): string;
var BaseUrl, ResponseUtf8, CallbackUrl: string;
begin Result := ''; BaseUrl := FTerminalManager.CurrentBaseUrl; Result := GenRequestId('IRIS');
  CallbackUrl := GetCallbackBase;
  DoLog('[信息] [终端通信] 正在提交虹膜抓拍，request_id=' + Result + '?，callback=' + CallbackUrl);
  if not FTerminalClient.PostJson(BaseUrl, '/resources/iris/request',
      '{"request_id":"' + Result + '","callback_url":"' + CallbackUrl + '"}', ResponseUtf8) then begin
    DoLog('[错误] [终端通信] 虹膜抓拍指令发送到终端失败。'); Result := ''; Exit; end;
  RememberRequestContext(Result, SaveDir, '');
  DoLog('[信息] [虹膜抓拍] 正在等待终端回调...'); end;

function TDelphiProxyServer.StartCameraPreviewDirect: Boolean;
var BaseUrl: string;
begin BaseUrl := FTerminalManager.CurrentBaseUrl;
  Result := FPreviewManager.StartPreview(prtCamera, pstLocal, 0, BaseUrl);
  if Result then Include(FLocalActivePreviews, prtCamera); end;

function TDelphiProxyServer.StopCameraPreviewDirect: Boolean;
begin Result := FPreviewManager.StopPreview(prtCamera, pstLocal);
  Exclude(FLocalActivePreviews, prtCamera); end;

function TDelphiProxyServer.StartFingerprintPreviewDirect: Boolean;
var BaseUrl: string;
begin BaseUrl := FTerminalManager.CurrentBaseUrl;
  Result := FPreviewManager.StartPreview(prtFingerprint, pstLocal, 0, BaseUrl);
  if Result then Include(FLocalActivePreviews, prtFingerprint); end;

function TDelphiProxyServer.StopFingerprintPreviewDirect: Boolean;
begin Result := FPreviewManager.StopPreview(prtFingerprint, pstLocal);
  Exclude(FLocalActivePreviews, prtFingerprint); end;

function TDelphiProxyServer.StartIrisPreviewDirect: Boolean;
var BaseUrl: string;
begin BaseUrl := FTerminalManager.CurrentBaseUrl;
  Result := FPreviewManager.StartPreview(prtIris, pstLocal, 0, BaseUrl);
  if Result then Include(FLocalActivePreviews, prtIris); end;

function TDelphiProxyServer.StopIrisPreviewDirect: Boolean;
begin Result := FPreviewManager.StopPreview(prtIris, pstLocal);
  Exclude(FLocalActivePreviews, prtIris); end;

// ============================================================
// HTTP HANDLER (for DLL requests)
// ============================================================
procedure TDelphiProxyServer.StartBusinessWorkers;
begin
  if FFaceCaptureWorker = nil then FFaceCaptureWorker := TProxyTaskWorker.Create(Self, '人脸抓拍', 1);
  if FFingerprintCaptureWorker = nil then FFingerprintCaptureWorker := TProxyTaskWorker.Create(Self, '指纹抓拍', 1);
  if FOcrWorker = nil then FOcrWorker := TProxyTaskWorker.Create(Self, 'OCR读取', 1);
  if FNfcWorker = nil then FNfcWorker := TProxyTaskWorker.Create(Self, 'IC卡读取', 1);
  FFaceCaptureWorker.Resume;
  FFingerprintCaptureWorker.Resume;
  FOcrWorker.Resume;
  FNfcWorker.Resume;
  DoLog('[信息] [业务队列] 固定业务线程已启动：人脸抓拍=1，指纹抓拍=1，OCR=1，IC卡=1；每类最多1个执行中+1个等待中，新请求替换旧等待请求。');
end;

procedure TDelphiProxyServer.StopBusinessWorkers;
begin
  if FFaceCaptureWorker <> nil then begin FFaceCaptureWorker.StopWorker; FFaceCaptureWorker.WaitFor; FFaceCaptureWorker.Free; FFaceCaptureWorker := nil; end;
  if FFingerprintCaptureWorker <> nil then begin FFingerprintCaptureWorker.StopWorker; FFingerprintCaptureWorker.WaitFor; FFingerprintCaptureWorker.Free; FFingerprintCaptureWorker := nil; end;
  if FOcrWorker <> nil then begin FOcrWorker.StopWorker; FOcrWorker.WaitFor; FOcrWorker.Free; FOcrWorker := nil; end;
  if FNfcWorker <> nil then begin FNfcWorker.StopWorker; FNfcWorker.WaitFor; FNfcWorker.Free; FNfcWorker := nil; end;
end;

function TDelphiProxyServer.QueueBusinessRequest(Worker: TProxyTaskWorker; const WorkerName, Method, Path, BodyUtf8: string; WaitMs: Integer): string;
var
  Task: TProxyQueuedTask;
  CompletedAfterTimeout: Boolean;
begin
  Result := '{"error":true,"code":"service_unavailable"}';
  if Worker = nil then begin
    DoLog('[错误] [业务队列] 队列未启动：' + WorkerName);
    Exit;
  end;
  Task := TProxyQueuedTask.Create(Method, Path, BodyUtf8, CurrentSwitchGeneration, True);
  if not Worker.Enqueue(Task) then begin
    DoLog('[错误] [业务队列] 队列不可用，无法接受请求：队列=' + WorkerName + '，path=' + Path);
    Task.Free;
    Exit;
  end;
  if Task.DoneEvent.WaitFor(WaitMs) = wrSignaled then begin
    Result := Task.Response;
    Task.Free;
  end else begin
    CompletedAfterTimeout := False;
    Task.StateLock.Acquire;
    try
      if Task.DoneEvent.WaitFor(0) = wrSignaled then
        CompletedAfterTimeout := True
      else begin
        Task.WaitForResponse := False;
        DoLog('[错误] [业务队列] 等待业务结果超时，已返回timeout：队列=' + WorkerName + '，path=' + Path + '，timeout=' + IntToStr(WaitMs) + 'ms');
        Result := '{"error":true,"code":"timeout"}';
      end;
    finally
      Task.StateLock.Release;
    end;
    if CompletedAfterTimeout then begin
      Result := Task.Response;
      Task.Free;
    end;
  end;
end;

function TDelphiProxyServer.HandleRequest(const Method, Path, BodyUtf8: string): string;
begin
  if (Path = '/ping') or (Path = '/terminal/switch') or (Path = '/authorize') or
     (Path = '/process/start') or (Path = '/process/end') then begin
    Result := HandleRequestDirect(Method, Path, BodyUtf8);
    Exit;
  end;
  if Path = '/capture/face' then begin
    Result := QueueBusinessRequest(FFaceCaptureWorker, '人脸抓拍', Method, Path, BodyUtf8, BUSINESS_WAIT_TIMEOUT_MS);
    Exit;
  end;
  if Path = '/capture/fingerprint' then begin
    Result := QueueBusinessRequest(FFingerprintCaptureWorker, '指纹抓拍', Method, Path, BodyUtf8, BUSINESS_WAIT_TIMEOUT_MS);
    Exit;
  end;
  if Path = '/ocr' then begin
    Result := QueueBusinessRequest(FOcrWorker, 'OCR读取', Method, Path, BodyUtf8, BUSINESS_WAIT_TIMEOUT_MS);
    Exit;
  end;
  if Path = '/nfc' then begin
    Result := QueueBusinessRequest(FNfcWorker, 'IC卡读取', Method, Path, BodyUtf8, BUSINESS_WAIT_TIMEOUT_MS);
    Exit;
  end;
  Result := HandleRequestDirect(Method, Path, BodyUtf8);
end;
function TDelphiProxyServer.HandleRequestDirect(const Method, Path, BodyUtf8: string): string;
var RequestId, SaveDir, CallbackUrl, TerminalBaseUrl, SavePath, ResponseUtf8, DllCallbackUrl, PayloadUtf8: string;
  TerminalIndex: Integer; Client: TTerminalClient;
  ThirdPartyHwndVal, FpHwnd, IrisHwnd: HWND;
begin
  if Path = '/ping' then begin Result := '{"status":"ok"}'; Exit; end;
  RequestId := ExtractJsonString(BodyUtf8, 'request_id');
  SaveDir := Utf8ToAnsi(ExtractJsonString(BodyUtf8, 'save_dir'));
  CallbackUrl := ExtractJsonString(BodyUtf8, 'callback_url');
  if SaveDir = '' then SaveDir := FTerminalManager.ProcessSaveDir;
  if SaveDir = '' then SaveDir := ExtractFilePath(ParamStr(0)) + 'captures';
  TerminalBaseUrl := FTerminalManager.CurrentBaseUrl;

  if Path = '/terminal/switch' then begin
    TerminalIndex := ExtractJsonInt(BodyUtf8, 'terminal_index');
    if (TerminalIndex < 1) or (TerminalIndex > 2) then begin
      Result := '{"error":true,"code":"invalid_terminal_index"}'; Exit; end;
    if IsTerminalSwitching then begin
      Result := '{"error":true,"code":"terminal_switching"}'; Exit; end;
    if StartTerminalSwitchAsync(TerminalIndex) then
      Result := '{"status":"ok","accepted":true,"terminal_index":' + IntToStr(TerminalIndex) + '}'
    else
      Result := '{"error":true,"code":"switch_failed","terminal_index":' + IntToStr(TerminalIndex) + '}';
    Exit; end;

  if IsTerminalSwitching then begin
    Result := '{"error":true,"code":"terminal_switching"}'; Exit; end;

  if (CallbackUrl <> '') and (RequestId <> '') then
    RememberRequestContext(RequestId, SaveDir, CallbackUrl);

  if Path = '/process/start' then begin
    if StartProcessDirect(SaveDir) then
      Result := '{"status":"ok"}'
    else
      Result := '{"error":true,"code":"terminal_request_failed"}';
    Exit; end;
  if Path = '/process/end' then begin EndProcessDirect; Result := '{"status":"ok"}'; Exit; end;

  if Path = '/capture/face' then begin
    if CaptureFaceDirect(SaveDir, SavePath) then
      Result := '{"status":"ok",' + JsonStr('save_path', AnsiToUtf8(SavePath)) + '}'
    else Result := '{"error":true,"code":"capture_failed"}'; Exit; end;

  if Path = '/capture/fingerprint' then begin
    if CaptureFingerprintDirect(SaveDir, SavePath) then
      Result := '{"status":"ok",' + JsonStr('save_path', AnsiToUtf8(SavePath)) + '}'
    else Result := '{"error":true,"code":"capture_failed"}'; Exit; end;

  if Path = '/capture/iris' then begin
    DllCallbackUrl := CallbackUrl;
    RememberRequestContext(RequestId, SaveDir, DllCallbackUrl);
    Client := TTerminalClient.Create;
    try
      if Client.PostJson(TerminalBaseUrl, '/resources/iris/request',
          '{"request_id":"' + RequestId + '","callback_url":"' + GetCallbackBase + '"}', ResponseUtf8) then
      begin DoLog('[信息] [DLL请求] DLL下发的虹膜抓拍指令已转发到终端，request_id=' + RequestId); Result := '{"accepted":true}'; end
      else begin DoLog('[错误] [终端通信] DLL下发的虹膜抓拍指令转发到终端失败。'); Result := '{"error":true,"code":"terminal_request_failed"}'; end;
    finally Client.Free; end; Exit; end;

  if Path = '/ocr' then begin
    DllCallbackUrl := CallbackUrl;
    RememberRequestContext(RequestId, SaveDir, DllCallbackUrl);
    Client := TTerminalClient.Create;
    try
      if Client.PostJson(TerminalBaseUrl, '/resources/ocr-document/request',
          '{"request_id":"' + RequestId + '","callback_url":"' + GetCallbackBase + '"}', ResponseUtf8) then
      begin DoLog('[信息] [DLL请求] DLL下发的OCR识别指令已转发到终端，request_id=' + RequestId); Result := '{"accepted":true}'; end
      else begin DoLog('[错误] [终端通信] DLL下发的OCR识别指令转发到终端失败。'); Result := '{"error":true,"code":"terminal_request_failed"}'; end;
    finally Client.Free; end; Exit; end;

  if Path = '/nfc' then begin
    DllCallbackUrl := CallbackUrl;
    RememberRequestContext(RequestId, SaveDir, DllCallbackUrl);
    Client := TTerminalClient.Create;
    try
      if Client.PostJson(TerminalBaseUrl, '/resources/nfc-card/request',
          '{"request_id":"' + RequestId + '","callback_url":"' + GetCallbackBase + '"}', ResponseUtf8) then
      begin DoLog('[信息] [DLL请求] DLL下发的IC卡识别指令已转发到终端，request_id=' + RequestId); Result := '{"accepted":true}'; end
      else begin DoLog('[错误] [终端通信] DLL下发的IC卡识别指令转发到终端失败。'); Result := '{"error":true,"code":"terminal_request_failed"}'; end;
    finally Client.Free; end; Exit; end;

  if Path = '/preview/camera/url' then begin
    if FPreviewManager.RequestPreviewUrl(prtCamera, TerminalBaseUrl, ResponseUtf8) then
      Result := '{"status":"ok",' + JsonStr('preview_url', ResponseUtf8) + '}'
    else Result := '{"error":true,"code":"preview_url_failed"}';
    Exit; end;

  if Path = '/preview/fingerprint/url' then begin
    if FPreviewManager.RequestPreviewUrl(prtFingerprint, TerminalBaseUrl, ResponseUtf8) then
      Result := '{"status":"ok",' + JsonStr('preview_url', ResponseUtf8) + '}'
    else Result := '{"error":true,"code":"preview_url_failed"}';
    Exit; end;

  if Path = '/preview/iris/url' then begin
    if FPreviewManager.RequestPreviewUrl(prtIris, TerminalBaseUrl, ResponseUtf8) then
      Result := '{"status":"ok",' + JsonStr('preview_url', ResponseUtf8) + '}'
    else Result := '{"error":true,"code":"preview_url_failed"}';
    Exit; end;
  if Path = '/preview/camera/start' then begin
    ThirdPartyHwndVal := HWND(ExtractJsonInt(BodyUtf8, 'hwnd'));
    if (ThirdPartyHwndVal = 0) or not IsWindow(ThirdPartyHwndVal) then begin
      DoLog('[错误] [预览管理] 摄像头目标窗口句柄无效，hwnd=' + IntToStr(ThirdPartyHwndVal));
      Result := '{"error":true,"code":"invalid_target_hwnd"}'; Exit; end;
    FThirdPartyCameraHwnd := ThirdPartyHwndVal;
    DoLog('[信息] [DLL请求] DLL下发摄像头预览，target_hwnd=' + IntToStr(ThirdPartyHwndVal));
    Include(FExternalActivePreviews, prtCamera);
    TAsyncStartPreviewThread.Create(Self, prtCamera, pstExternal,
      ThirdPartyHwndVal, TerminalBaseUrl, RequestId, CallbackUrl, CurrentSwitchGeneration);
    Result := '{"accepted":true}'; Exit; end;

  if Path = '/preview/camera/stop' then begin TAsyncStopPreviewThread.Create(Self, prtCamera, pstExternal); Exclude(FExternalActivePreviews, prtCamera); FThirdPartyCameraHwnd := 0; Result := '{"status":"ok"}'; Exit; end;

  if Path = '/preview/fingerprint/start' then begin
    FpHwnd := HWND(ExtractJsonInt(BodyUtf8, 'hwnd'));
    if (FpHwnd = 0) or not IsWindow(FpHwnd) then begin
      DoLog('[错误] [预览管理] 指纹目标窗口句柄无效，hwnd=' + IntToStr(FpHwnd));
      Result := '{"error":true,"code":"invalid_target_hwnd"}'; Exit; end;
    FThirdPartyFingerprintHwnd := FpHwnd;
    Include(FExternalActivePreviews, prtFingerprint);
    TAsyncStartPreviewThread.Create(Self, prtFingerprint, pstExternal,
      FpHwnd, TerminalBaseUrl, RequestId, CallbackUrl, CurrentSwitchGeneration);
    Result := '{"accepted":true}'; Exit; end;

  if Path = '/preview/fingerprint/stop' then begin TAsyncStopPreviewThread.Create(Self, prtFingerprint, pstExternal); Exclude(FExternalActivePreviews, prtFingerprint); FThirdPartyFingerprintHwnd := 0; Result := '{"status":"ok"}'; Exit; end;

  if Path = '/preview/iris/start' then begin
    IrisHwnd := HWND(ExtractJsonInt(BodyUtf8, 'hwnd'));
    if (IrisHwnd = 0) or not IsWindow(IrisHwnd) then begin
      DoLog('[错误] [预览管理] 虹膜目标窗口句柄无效，hwnd=' + IntToStr(IrisHwnd));
      Result := '{"error":true,"code":"invalid_target_hwnd"}'; Exit; end;
    FThirdPartyIrisHwnd := IrisHwnd;
    Include(FExternalActivePreviews, prtIris);
    TAsyncStartPreviewThread.Create(Self, prtIris, pstExternal,
      IrisHwnd, TerminalBaseUrl, RequestId, CallbackUrl, CurrentSwitchGeneration);
    Result := '{"accepted":true}'; Exit; end;

  if Path = '/preview/iris/stop' then begin TAsyncStopPreviewThread.Create(Self, prtIris, pstExternal); Exclude(FExternalActivePreviews, prtIris); FThirdPartyIrisHwnd := 0; Result := '{"status":"ok"}'; Exit; end;

  if Path = '/authorize' then begin
    RequestId := ExtractJsonString(BodyUtf8, 'request_id');
    CallbackUrl := ExtractJsonString(BodyUtf8, 'callback_url');
    DoLog('[' + Utf8ToAnsi(#$E6#$8E#$88#$E6#$9D#$83) + '] request_id=' + RequestId);
    PayloadUtf8 := '{"request_id":"' + RequestId + '","resource_type":"authorization","auth_result":1' +
      ',"ZJHM":"' + ExtractJsonString(BodyUtf8, 'ZJHM') + '"' +
      ',"ZJLB":"' + ExtractJsonString(BodyUtf8, 'ZJLB') + '"' +
      ',"GJDQDM":"' + ExtractJsonString(BodyUtf8, 'GJDQDM') + '"' +
      ',"XM":"' + ExtractJsonString(BodyUtf8, 'XM') + '"' +
      ',"XB":"' + ExtractJsonString(BodyUtf8, 'XB') + '"' +
      ',"CSRQ":"' + ExtractJsonString(BodyUtf8, 'CSRQ') + '"' +
      ',"KADM":"' + ExtractJsonString(BodyUtf8, 'KADM') + '"' +
      ',"message":"' + #$E5#$90#$8C#$E6#$84#$8F#$E6#$8E#$88#$E6#$9D#$83 + '"}';
    if MakeCallback(RequestId, CallbackUrl, PayloadUtf8) then begin
      MarkCompletedCallback(RequestId);
      ForgetRequestContext(RequestId);
    end;
    Result := '{"accepted":true}'; Exit;
  end;

  Result := '{"error":true,"code":"not_found","message":"unknown:' + Path + '"}'; end;

// ============================================================
// HandleTerminalCallback
// ============================================================
function TDelphiProxyServer.HandleTerminalCallback(const BodyUtf8: string): string;
var ResourceType, RequestId, DllCallbackUrl, SaveDir, SavePath, PayloadUtf8: string;
  RequestGeneration: Integer;
  OcrResult: TOcrCallbackResult; NfcResult: TNfcCallbackResult; ImgResult: TImageCallbackResult;
  I: Integer; ImgPath, ImgName: string; SL: TStringList;
begin
  try
  ResourceType := FCallbackParser.GetResourceType(BodyUtf8);
  RequestId := FCallbackParser.ExtractField(BodyUtf8, 'request_id');
  DoLog('[信息] [终端回调] 收到终端回调，resource_type=' + ResourceType + '?，request_id=' + RequestId);

  if IsCompletedCallback(RequestId) then begin
    Result := '{"status":"ignored","code":"duplicate_callback"}';
    Exit;
  end;
  if IsTerminalSwitching then begin
    Result := '{"status":"ignored","code":"terminal_switching"}';
    Exit;
  end;
  if ShouldDropUnknownCallback(RequestId) then begin
    Result := '{"status":"ignored","code":"stale_terminal_callback"}';
    Exit;
  end;
  GetRequestContext(RequestId, SaveDir, DllCallbackUrl, RequestGeneration);
  if (RequestGeneration >= 0) and (RequestGeneration <> CurrentSwitchGeneration) then begin
    ForgetRequestContext(RequestId);
    Result := '{"status":"ignored","code":"stale_terminal_callback"}';
    Exit;
  end;
  if SaveDir = '' then SaveDir := FTerminalManager.ProcessSaveDir;
  if SaveDir = '' then SaveDir := ExtractFilePath(ParamStr(0)) + 'captures';

  if ResourceType = 'ocr_event_status' then begin
    DoLog('[信息] [OCR回调] 收到事件状态回调，等待识别结果中...');
  end
  else if ResourceType = 'ocr_document' then begin
    OcrResult := FCallbackParser.ParseOcrDocument(BodyUtf8);
    if OcrResult.Valid then begin
      SavePath := IncludeTrailingBackslash(FFileSaver.EnsureDir(SaveDir)) + 'OCR.json';
      SL := TStringList.Create;
      try SL.Text := BodyUtf8; SL.SaveToFile(SavePath);
      finally SL.Free; end;
      I := 0;
      while I < OcrResult.EvidenceImagesCount do begin
        if OcrResult.EvidenceImages[I].ImageBase64 <> '' then begin
          ImgName := '';
          if OcrResult.EvidenceImages[I].ImageType = 2 then ImgName := '人像'
          else case OcrResult.EvidenceImages[I].LampType of
            1: ImgName := '可见光';
            2: ImgName := '红外光';
            3: ImgName := '紫外光';
          end;
          if ImgName <> '' then begin
            ImgPath := FFileSaver.SaveBase64ImageToFile(OcrResult.EvidenceImages[I].ImageBase64,
              IncludeTrailingBackslash(FFileSaver.EnsureDir(SaveDir)) + ImgName + '.jpg');
            DoLog('[信息] [OCR] 照片已保存: type=' + IntToStr(OcrResult.EvidenceImages[I].ImageType) +
              ',lamp=' + IntToStr(OcrResult.EvidenceImages[I].LampType) + ',path=' + ImgPath);
          end;
        end;
        I := I + 1;
      end;
      DoLog('[信息] [OCR] 已完成, mrz=' + OcrResult.Mrz + ',evidence_count=' + IntToStr(OcrResult.EvidenceImagesCount) + ',save_path=' + SavePath);
      if DllCallbackUrl = '' then DllCallbackUrl := GetDllCallbackUrl('/ocr');
      if DllCallbackUrl <> '' then begin
        PayloadUtf8 := '{' + JsonStr('request_id', RequestId) + ',' + JsonStr('mrz', OcrResult.Mrz) + ',' +
          JsonStr('save_path', AnsiToUtf8(SavePath)) + '}';
        if MakeCallback(RequestId, DllCallbackUrl, PayloadUtf8) then begin
          MarkCompletedCallback(RequestId);
          ForgetRequestContext(RequestId);
        end; end; end
    else DoLog('[错误] [OCR回调] 识别结果解析失败。'); end

  else if ResourceType = 'nfc_card' then begin
    NfcResult := FCallbackParser.ParseNfcCard(BodyUtf8);
    if NfcResult.Valid then begin
      DoLog('[信息] [IC卡回调] 卡片识别完成，card_text=' + Utf8ToAnsi(NfcResult.CardText));
      if DllCallbackUrl = '' then DllCallbackUrl := GetDllCallbackUrl('/nfc-card');
      if DllCallbackUrl <> '' then begin
        PayloadUtf8 := '{' + JsonStr('request_id', RequestId) + ',' + JsonStr('card_text', NfcResult.CardText) + '}';
        if MakeCallback(RequestId, DllCallbackUrl, PayloadUtf8) then begin
          MarkCompletedCallback(RequestId);
          ForgetRequestContext(RequestId);
        end; end; end
    else DoLog('[错误] [IC卡回调] 回调解析失败，未找到card_text。'); end

  else if ResourceType = 'iris_image' then begin
    ImgResult := FCallbackParser.ParseImageCapture(BodyUtf8);
    if ImgResult.Valid then begin
      SavePath := FFileSaver.SaveBase64Image(ImgResult.ImageBase64, ImgResult.ImageMimeType, SaveDir, RequestId, 'iris');
      DoLog('[信息] [虹膜回调] 图片保存成功，save_path=' + SavePath);
      if DllCallbackUrl <> '' then begin
        PayloadUtf8 := '{' + JsonStr('request_id', RequestId) + ',' + JsonStr('save_path', AnsiToUtf8(SavePath)) + '}';
        if MakeCallback(RequestId, DllCallbackUrl, PayloadUtf8) then begin
          MarkCompletedCallback(RequestId);
          ForgetRequestContext(RequestId);
        end; end; end
    else DoLog('[错误] [虹膜回调] 回调解析失败。'); end

  else if ResourceType = 'face_image' then begin
    ImgResult := FCallbackParser.ParseImageCapture(BodyUtf8);
    if ImgResult.Valid then begin
      SavePath := FFileSaver.SaveBase64Image(ImgResult.ImageBase64, ImgResult.ImageMimeType, SaveDir, RequestId, 'face_async');
      DoLog('[信息] [人脸回调] 图片保存成功，save_path=' + SavePath); end; end

  else if ResourceType = 'fingerprint_image' then begin
    ImgResult := FCallbackParser.ParseImageCapture(BodyUtf8);
    if ImgResult.Valid then begin
      SavePath := FFileSaver.SaveBase64Image(ImgResult.ImageBase64, ImgResult.ImageMimeType, SaveDir, RequestId, 'fingerprint_async');
      DoLog('[信息] [指纹回调] 图片保存成功，save_path=' + SavePath); end; end

  else DoLog('[提示] [终端回调] 收到未知资源类型，resource_type=' + ResourceType);

  Result := '{"status":"accepted"}';
  except
    on E: Exception do begin
      DoLog('[错误] [终端回调] 处理终端回调异常：resource_type=' + ResourceType +
        '，request_id=' + RequestId + '，错误=' + E.Message);
      if DllCallbackUrl = '' then begin
        if ResourceType = 'ocr_document' then
          DllCallbackUrl := GetDllCallbackUrl('/ocr')
        else if ResourceType = 'nfc_card' then
          DllCallbackUrl := GetDllCallbackUrl('/nfc-card')
        else if ResourceType = 'iris_image' then
          DllCallbackUrl := GetDllCallbackUrl('/iris');
      end;
      if (DllCallbackUrl <> '') and (RequestId <> '') then begin
        PayloadUtf8 := '{' + JsonStr('request_id', RequestId) +
          ',"error":true,' + JsonStr('code', 'callback_exception') + ',' +
          JsonStr('message', AnsiToUtf8(E.Message)) + '}';
        MakeCallback(RequestId, DllCallbackUrl, PayloadUtf8);
      end;
      Result := '{"status":"error","code":"callback_exception"}';
    end;
  end;
end;

end.
