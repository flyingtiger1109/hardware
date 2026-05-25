unit PreviewManager;

interface

uses
  Windows, SysUtils, Classes, ExtCtrls, Controls,
  TerminalClient, VlcPlayer;

type
  TPreviewResourceType = (prtCamera, prtFingerprint, prtIris);
  TPreviewLogCallback = procedure(const Msg: string) of object;

  TPreviewManager = class
  private
    FVlcCamera: TVlcPlayer;
    FVlcFingerprint: TVlcPlayer;
    FVlcIris: TVlcPlayer;
    FCameraPanel: TPanel;
    FFingerprintPanel: TPanel;
    FIrisPanel: TPanel;
    FCameraTargetHwnd: HWND;
    FFingerprintTargetHwnd: HWND;
    FIrisTargetHwnd: HWND;
    FLogProc: TPreviewLogCallback;
    procedure DoLog(const Msg: string);
    function ResTypeToTerminalPath(ResType: TPreviewResourceType): string;
    function GetVlcPlayer(ResType: TPreviewResourceType): TVlcPlayer;
    procedure SetVlcPlayer(ResType: TPreviewResourceType; Vlc: TVlcPlayer);
    function GetTargetHwnd(ResType: TPreviewResourceType): HWND;
    procedure SetTargetHwnd(ResType: TPreviewResourceType; HwndValue: HWND);
  public
    constructor Create(ACameraPanel, AFingerprintPanel, AIrisPanel: TPanel);
    destructor Destroy; override;
    procedure SetLogProc(ALogProc: TPreviewLogCallback);
    // TargetHwnd: if 0, use Delphi panel; otherwise VLC renders directly to it
    function StartPreview(ResType: TPreviewResourceType; TargetHwnd: HWND;
      const TerminalBaseUrl: string): Boolean;
    function StopPreview(ResType: TPreviewResourceType): Boolean;
    function IsPreviewRunning(ResType: TPreviewResourceType): Boolean;
    function GetRenderHwnd(ResType: TPreviewResourceType): HWND;
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
  FVlcCamera := nil; FVlcFingerprint := nil; FVlcIris := nil;
  FCameraTargetHwnd := 0; FFingerprintTargetHwnd := 0; FIrisTargetHwnd := 0;
  FLogProc := nil;
end;

destructor TPreviewManager.Destroy;
begin
  StopPreview(prtCamera); StopPreview(prtFingerprint); StopPreview(prtIris);
  inherited Destroy;
end;

procedure TPreviewManager.SetLogProc(ALogProc: TPreviewLogCallback);
begin FLogProc := ALogProc; end;

procedure TPreviewManager.DoLog(const Msg: string);
begin if Assigned(FLogProc) then FLogProc(Msg); end;

function TPreviewManager.GetVlcPlayer(ResType: TPreviewResourceType): TVlcPlayer;
begin
  case ResType of
    prtCamera: Result := FVlcCamera;
    prtFingerprint: Result := FVlcFingerprint;
    prtIris: Result := FVlcIris;
    else Result := nil;
  end;
end;

procedure TPreviewManager.SetVlcPlayer(ResType: TPreviewResourceType; Vlc: TVlcPlayer);
begin
  case ResType of
    prtCamera: FVlcCamera := Vlc;
    prtFingerprint: FVlcFingerprint := Vlc;
    prtIris: FVlcIris := Vlc;
  end;
end;

function TPreviewManager.GetTargetHwnd(ResType: TPreviewResourceType): HWND;
begin
  case ResType of
    prtCamera: Result := FCameraTargetHwnd;
    prtFingerprint: Result := FFingerprintTargetHwnd;
    prtIris: Result := FIrisTargetHwnd;
    else Result := 0;
  end;
end;

procedure TPreviewManager.SetTargetHwnd(ResType: TPreviewResourceType; HwndValue: HWND);
begin
  case ResType of
    prtCamera: FCameraTargetHwnd := HwndValue;
    prtFingerprint: FFingerprintTargetHwnd := HwndValue;
    prtIris: FIrisTargetHwnd := HwndValue;
  end;
end;

function TPreviewManager.GetRenderHwnd(ResType: TPreviewResourceType): HWND;
var
  HwndValue: HWND;
begin
  HwndValue := GetTargetHwnd(ResType);
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

function TPreviewManager.IsPreviewRunning(ResType: TPreviewResourceType): Boolean;
var
  Vlc: TVlcPlayer;
begin
  Vlc := GetVlcPlayer(ResType);
  Result := (Vlc <> nil) and Vlc.Running;
end;

function TPreviewManager.StartPreview(ResType: TPreviewResourceType; TargetHwnd: HWND;
  const TerminalBaseUrl: string): Boolean;
var
  Client: TTerminalClient;
  ResponseUtf8, PreviewUrl, Status, TerminalPath: string;
  RenderHwnd: HWND;
  Vlc: TVlcPlayer;
begin
  Result := False;

  // If already running with same target, just return OK
  if IsPreviewRunning(ResType) and (GetTargetHwnd(ResType) = TargetHwnd) then
  begin
    DoLog('[提示] [预览控制] 预览已在目标窗口运行，无需重复启动。');
    Result := True;
    Exit;
  end;

  // If running but target changed, stop old first
  if IsPreviewRunning(ResType) then
  begin
    DoLog('[信息] [预览控制] 目标窗口已变更，正在重新启动预览。');
    StopPreview(ResType);
  end;

  TerminalPath := ResTypeToTerminalPath(ResType);
  if TerminalPath = '' then Exit;

  // Request preview URL from terminal
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

  // Determine render target
  if (TargetHwnd <> 0) and IsWindow(TargetHwnd) then
    RenderHwnd := TargetHwnd
  else
    RenderHwnd := GetDefaultHostHwnd(ResType);

  // Create VLC and render DIRECTLY to the target HWND
  Vlc := TVlcPlayer.Create;
  if not Vlc.Play(PreviewUrl, RenderHwnd) then
  begin
    DoLog('[错误] [预览渲染] VLC启动失败：' + Vlc.LastError);
    Vlc.Free;
    Exit;
  end;

  SetVlcPlayer(ResType, Vlc);
  SetTargetHwnd(ResType, TargetHwnd);

  DoLog('[信息] [预览渲染] VLC预览已启动：url=' + PreviewUrl + '，hwnd=' +
    IntToStr(RenderHwnd) + '，target=' + IntToStr(TargetHwnd));
  Result := True;
end;

function TPreviewManager.StopPreview(ResType: TPreviewResourceType): Boolean;
var
  Vlc: TVlcPlayer;
begin
  Result := False;
  Vlc := GetVlcPlayer(ResType);
  if Vlc = nil then Exit;

  Vlc.Stop;
  Vlc.Free;
  SetVlcPlayer(ResType, nil);
  SetTargetHwnd(ResType, 0);

  DoLog('[信息] [预览渲染] 预览已停止。');
  Result := True;
end;

end.
