unit PreviewManager;

interface

uses
  Windows, SysUtils, Classes, ExtCtrls, Controls,
  TerminalClient, VlcPlayer;

type
  TPreviewResourceType = (prtCamera, prtFingerprint, prtIris);
  TPreviewSessionType = (pstLocal, pstExternal);
  TPreviewResourceSet = set of TPreviewResourceType;
  TPreviewLogCallback = procedure(const Msg: string) of object;

  TPreviewManager = class
  private
    FVlcLocalCamera: TVlcPlayer;
    FVlcLocalFingerprint: TVlcPlayer;
    FVlcLocalIris: TVlcPlayer;
    FVlcExternalCamera: TVlcPlayer;
    FVlcExternalFingerprint: TVlcPlayer;
    FVlcExternalIris: TVlcPlayer;
    FCameraPanel: TPanel;
    FFingerprintPanel: TPanel;
    FIrisPanel: TPanel;
    FLocalCameraTargetHwnd: HWND;
    FLocalFingerprintTargetHwnd: HWND;
    FLocalIrisTargetHwnd: HWND;
    FExternalCameraTargetHwnd: HWND;
    FExternalFingerprintTargetHwnd: HWND;
    FExternalIrisTargetHwnd: HWND;
    FNetworkCachingMs: Integer;
    FLiveCachingMs: Integer;
    FLogProc: TPreviewLogCallback;
    FCommandVlc: TVlcPlayer;
    FCommandUrl: string;
    FCommandHwnd: HWND;
    FCommandSourceWidth: Integer;
    FCommandSourceHeight: Integer;
    FCommandSwapLayoutDimensions: Boolean;
    FCommandPlayResult: Boolean;
    procedure RunOnMainThread(Method: TThreadMethod);
    procedure ExecutePlayCommand;
    procedure ExecuteStopCommand;
    procedure ExecuteFreeCommand;
    procedure DoLog(const Msg: string);
    function ResTypeToTerminalPath(ResType: TPreviewResourceType): string;
    function ResTypeToLogName(ResType: TPreviewResourceType): string;
    function SessionTypeToLogName(SessionType: TPreviewSessionType): string;
    function GetVlcPlayer(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType): TVlcPlayer;
    procedure SetVlcPlayer(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType; Vlc: TVlcPlayer);
    function GetTargetHwnd(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType): HWND;
    procedure SetTargetHwnd(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType; HwndValue: HWND);
  public
    constructor Create(ACameraPanel, AFingerprintPanel, AIrisPanel: TPanel);
    destructor Destroy; override;
    procedure SetLogProc(ALogProc: TPreviewLogCallback);
    procedure SetCachingMs(NetworkCachingMs, LiveCachingMs: Integer);
    // Local and external sessions own separate VLC players for each resource.
    // TargetHwnd: if 0, use Delphi panel; otherwise render relative to it.
    function StartPreview(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType; TargetHwnd: HWND;
      const TerminalBaseUrl: string): Boolean;
    function StopPreview(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType): Boolean;
    function IsPreviewRunning(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType): Boolean;
    function GetRenderHwnd(ResType: TPreviewResourceType;
      SessionType: TPreviewSessionType): HWND;
    function GetDefaultHostHwnd(ResType: TPreviewResourceType): HWND;
  end;

implementation

uses EncodingHelper;

function ExtractJsonField(const Json, Key: string): string;
var
  K, I: Integer;
  Escaped: Boolean;
begin
  Result := '';
  K := Pos('"' + Key + '"', Json);
  if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', #9, #10, #13, ':']) do Inc(K);
  if (K > Length(Json)) or (Json[K] <> '"') then Exit;
  Inc(K);
  Escaped := False;
  for I := K to Length(Json) do
  begin
    if Escaped then begin
      case Json[I] of
        'n': Result := Result + #10; 'r': Result := Result + #13; 't': Result := Result + #9;
      else Result := Result + Json[I]; end;
      Escaped := False;
    end
    else if Json[I] = '\' then Escaped := True
    else if Json[I] = '"' then Exit
    else Result := Result + Json[I];
  end;
end;

constructor TPreviewManager.Create(ACameraPanel, AFingerprintPanel, AIrisPanel: TPanel);
begin
  inherited Create;
  FCameraPanel := ACameraPanel;
  FFingerprintPanel := AFingerprintPanel;
  FIrisPanel := AIrisPanel;
  FVlcLocalCamera := nil; FVlcLocalFingerprint := nil; FVlcLocalIris := nil;
  FVlcExternalCamera := nil; FVlcExternalFingerprint := nil; FVlcExternalIris := nil;
  FLocalCameraTargetHwnd := 0; FLocalFingerprintTargetHwnd := 0; FLocalIrisTargetHwnd := 0;
  FExternalCameraTargetHwnd := 0; FExternalFingerprintTargetHwnd := 0; FExternalIrisTargetHwnd := 0;
  FNetworkCachingMs := 150; FLiveCachingMs := 150;
  FLogProc := nil;
  FCommandVlc := nil;
  FCommandUrl := '';
  FCommandHwnd := 0;
  FCommandSourceWidth := 0;
  FCommandSourceHeight := 0;
  FCommandSwapLayoutDimensions := False;
  FCommandPlayResult := False;
end;

destructor TPreviewManager.Destroy;
begin
  StopPreview(prtCamera, pstLocal);
  StopPreview(prtFingerprint, pstLocal);
  StopPreview(prtIris, pstLocal);
  StopPreview(prtCamera, pstExternal);
  StopPreview(prtFingerprint, pstExternal);
  StopPreview(prtIris, pstExternal);
  inherited Destroy;
end;

procedure TPreviewManager.SetLogProc(ALogProc: TPreviewLogCallback);
begin FLogProc := ALogProc; end;

procedure TPreviewManager.SetCachingMs(NetworkCachingMs, LiveCachingMs: Integer);
begin
  if NetworkCachingMs < 0 then NetworkCachingMs := 0;
  if LiveCachingMs < 0 then LiveCachingMs := 0;
  FNetworkCachingMs := NetworkCachingMs;
  FLiveCachingMs := LiveCachingMs;
end;

procedure TPreviewManager.DoLog(const Msg: string);
begin if Assigned(FLogProc) then FLogProc(Msg); end;

procedure TPreviewManager.RunOnMainThread(Method: TThreadMethod);
begin
  if GetCurrentThreadID = MainThreadID then
    Method
  else
    TThread.Synchronize(nil, Method);
end;

procedure TPreviewManager.ExecutePlayCommand;
begin
  FCommandPlayResult := False;
  if FCommandVlc = nil then Exit;
  FCommandPlayResult := FCommandVlc.Play(FCommandUrl, FCommandHwnd,
    FCommandSourceWidth, FCommandSourceHeight, FCommandSwapLayoutDimensions,
    FNetworkCachingMs, FLiveCachingMs);
end;

procedure TPreviewManager.ExecuteStopCommand;
begin
  if FCommandVlc = nil then Exit;
  FCommandVlc.Stop;
end;

procedure TPreviewManager.ExecuteFreeCommand;
begin
  if FCommandVlc = nil then Exit;
  FCommandVlc.Free;
end;

function TPreviewManager.GetVlcPlayer(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType): TVlcPlayer;
begin
  if SessionType = pstLocal then
  begin
    case ResType of
      prtCamera: Result := FVlcLocalCamera;
      prtFingerprint: Result := FVlcLocalFingerprint;
      prtIris: Result := FVlcLocalIris;
      else Result := nil;
    end;
  end
  else
  begin
    case ResType of
      prtCamera: Result := FVlcExternalCamera;
      prtFingerprint: Result := FVlcExternalFingerprint;
      prtIris: Result := FVlcExternalIris;
      else Result := nil;
    end;
  end;
end;

procedure TPreviewManager.SetVlcPlayer(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType; Vlc: TVlcPlayer);
begin
  if SessionType = pstLocal then
  begin
    case ResType of
      prtCamera: FVlcLocalCamera := Vlc;
      prtFingerprint: FVlcLocalFingerprint := Vlc;
      prtIris: FVlcLocalIris := Vlc;
    end;
  end
  else
  begin
    case ResType of
      prtCamera: FVlcExternalCamera := Vlc;
      prtFingerprint: FVlcExternalFingerprint := Vlc;
      prtIris: FVlcExternalIris := Vlc;
    end;
  end;
end;

function TPreviewManager.GetTargetHwnd(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType): HWND;
begin
  if SessionType = pstLocal then
  begin
    case ResType of
      prtCamera: Result := FLocalCameraTargetHwnd;
      prtFingerprint: Result := FLocalFingerprintTargetHwnd;
      prtIris: Result := FLocalIrisTargetHwnd;
      else Result := 0;
    end;
  end
  else
  begin
    case ResType of
      prtCamera: Result := FExternalCameraTargetHwnd;
      prtFingerprint: Result := FExternalFingerprintTargetHwnd;
      prtIris: Result := FExternalIrisTargetHwnd;
      else Result := 0;
    end;
  end;
end;

procedure TPreviewManager.SetTargetHwnd(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType; HwndValue: HWND);
begin
  if SessionType = pstLocal then
  begin
    case ResType of
      prtCamera: FLocalCameraTargetHwnd := HwndValue;
      prtFingerprint: FLocalFingerprintTargetHwnd := HwndValue;
      prtIris: FLocalIrisTargetHwnd := HwndValue;
    end;
  end
  else
  begin
    case ResType of
      prtCamera: FExternalCameraTargetHwnd := HwndValue;
      prtFingerprint: FExternalFingerprintTargetHwnd := HwndValue;
      prtIris: FExternalIrisTargetHwnd := HwndValue;
    end;
  end;
end;

function TPreviewManager.GetRenderHwnd(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType): HWND;
var
  HwndValue: HWND;
begin
  HwndValue := GetTargetHwnd(ResType, SessionType);
  if HwndValue = 0 then
    Result := GetDefaultHostHwnd(ResType)
  else
    Result := HwndValue;
end;

function TPreviewManager.GetDefaultHostHwnd(ResType: TPreviewResourceType): HWND;
begin
  case ResType of
    prtCamera: Result := FCameraPanel.Handle;
    prtFingerprint: Result := FFingerprintPanel.Handle;
    prtIris: Result := FIrisPanel.Handle;
    else Result := 0;
  end;
end;

function TPreviewManager.ResTypeToTerminalPath(ResType: TPreviewResourceType): string;
begin
  case ResType of
    prtCamera: Result := '/resources/face-preview/request';
    prtFingerprint: Result := '/resources/fingerprint-preview/request';
    prtIris: Result := '/resources/iris-preview/request';
    else Result := '';
  end;
end;

function TPreviewManager.ResTypeToLogName(ResType: TPreviewResourceType): string;
begin
  case ResType of
    prtCamera: Result := 'camera';
    prtFingerprint: Result := 'fingerprint';
    prtIris: Result := 'iris';
    else Result := 'unknown';
  end;
end;

function TPreviewManager.SessionTypeToLogName(SessionType: TPreviewSessionType): string;
begin
  if SessionType = pstLocal then
    Result := 'local'
  else
    Result := 'external';
end;

function TPreviewManager.IsPreviewRunning(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType): Boolean;
var
  Vlc: TVlcPlayer;
begin
  Vlc := GetVlcPlayer(ResType, SessionType);
  Result := (Vlc <> nil) and Vlc.Running;
end;

function TPreviewManager.StartPreview(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType; TargetHwnd: HWND;
  const TerminalBaseUrl: string): Boolean;
var
  Client: TTerminalClient;
  ResponseUtf8, PreviewUrl, Status, TerminalPath: string;
  RenderHwnd: HWND;
  SourceWidth, SourceHeight: Integer;
  SwapLayoutDimensions: Boolean;
  Vlc: TVlcPlayer;
begin
  Result := False;

  // 同一会话已在同一目标显示时不重复启动。
  if IsPreviewRunning(ResType, SessionType) and
    (GetTargetHwnd(ResType, SessionType) = TargetHwnd) then
  begin
    DoLog(Format('[提示] [预览控制] 预览已在目标窗口运行，无需重复启动：resource=%s，session=%s。',
      [ResTypeToLogName(ResType), SessionTypeToLogName(SessionType)]));
    Result := True;
    Exit;
  end;

  // 同一会话目标改变时，仅重启该会话，不影响另一来源的预览。
  if IsPreviewRunning(ResType, SessionType) then
  begin
    DoLog(Format('[信息] [预览控制] 目标窗口已变更，正在重新启动预览：resource=%s，session=%s。',
      [ResTypeToLogName(ResType), SessionTypeToLogName(SessionType)]));
    StopPreview(ResType, SessionType);
  end;

  TerminalPath := ResTypeToTerminalPath(ResType);
  if TerminalPath = '' then Exit;

  // 向终端请求预览地址。
  Client := TTerminalClient.Create;
  try
    DoLog('[信息] [终端调用] 正在请求预览地址：' + TerminalBaseUrl + TerminalPath);
    if not Client.PostJson(TerminalBaseUrl, TerminalPath, '{}', ResponseUtf8) then
    begin
      DoLog('[错误] [终端调用] 请求预览地址失败。');
      Exit;
    end;
    Status := ExtractJsonField(ResponseUtf8, 'status');
    PreviewUrl := ExtractJsonField(ResponseUtf8, 'preview_url');
    DoLog('[信息] [终端调用] 已收到预览地址响应：status=' + Status + '，url=' + PreviewUrl);
    if (Status <> 'ok') or (PreviewUrl = '') then
    begin
      DoLog('[错误] [终端调用] 终端返回的预览地址无效。');
      Exit;
    end;
  finally
    Client.Free;
  end;

  // 选择本地容器或第三方锚点目标。
  if (TargetHwnd <> 0) and IsWindow(TargetHwnd) then
    RenderHwnd := TargetHwnd
  else
    RenderHwnd := GetDefaultHostHwnd(ResType);

  SwapLayoutDimensions := False;
  case ResType of
    prtCamera: begin SourceWidth := 480; SourceHeight := 640; SwapLayoutDimensions := True; end;
    prtFingerprint: begin SourceWidth := 640; SourceHeight := 640; end;
    else begin SourceWidth := 640; SourceHeight := 480; end;
  end;

  // 视频子窗口采用 cover 布局，由父窗口在中心位置裁剪溢出内容。
  Vlc := TVlcPlayer.Create;
  Vlc.SetLogProc(FLogProc);
  FCommandVlc := Vlc;
  FCommandUrl := PreviewUrl;
  FCommandHwnd := RenderHwnd;
  FCommandSourceWidth := SourceWidth;
  FCommandSourceHeight := SourceHeight;
  FCommandSwapLayoutDimensions := SwapLayoutDimensions;
  RunOnMainThread(ExecutePlayCommand);
  if not FCommandPlayResult then
  begin
    DoLog('[错误] [预览渲染] VLC启动失败：' + Vlc.LastError);
    RunOnMainThread(ExecuteFreeCommand);
    FCommandVlc := nil;
    FCommandUrl := '';
    Exit;
  end;
  FCommandVlc := nil;
  FCommandUrl := '';

  SetVlcPlayer(ResType, SessionType, Vlc);
  SetTargetHwnd(ResType, SessionType, TargetHwnd);

  DoLog('[信息] [预览渲染] VLC预览已启动：resource=' + ResTypeToLogName(ResType) +
    '，session=' + SessionTypeToLogName(SessionType) + '，url=' + PreviewUrl +
    '，hwnd=' + IntToStr(RenderHwnd) + '，target=' + IntToStr(TargetHwnd));
  Result := True;
end;

function TPreviewManager.StopPreview(ResType: TPreviewResourceType;
  SessionType: TPreviewSessionType): Boolean;
var
  Vlc: TVlcPlayer;
begin
  Result := False;
  Vlc := GetVlcPlayer(ResType, SessionType);
  if Vlc = nil then Exit;

  FCommandVlc := Vlc;
  RunOnMainThread(ExecuteStopCommand);
  RunOnMainThread(ExecuteFreeCommand);
  FCommandVlc := nil;
  SetVlcPlayer(ResType, SessionType, nil);
  SetTargetHwnd(ResType, SessionType, 0);

  DoLog(Format('[信息] [预览渲染] 预览已停止：resource=%s，session=%s。',
    [ResTypeToLogName(ResType), SessionTypeToLogName(SessionType)]));
  Result := True;
end;

end.
