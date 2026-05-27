unit VlcPlayer;

interface

uses Windows, SysUtils, Classes, ExtCtrls;

type
  TVlcLogCallback = procedure(const Msg: string) of object;
  TVlcPlayer = class;

  TLayoutThread = class(TThread)
  private
    FPlayer: TVlcPlayer;
  protected
    procedure Execute; override;
  public
    constructor Create(APlayer: TVlcPlayer);
  end;

  TVlcPlayer = class
  private
    FhLibVlcCore: HMODULE;
    FhLibVlc: HMODULE;
    FVlcInstance: Pointer;
    FMedia: Pointer;
    FMediaPlayer: Pointer;
    FRunning: Boolean;
    FHostHwnd: HWND;
    FVideoHwnd: HWND;
    FAnchorHwnd: HWND;
    FOverlayHwnd: HWND;
    FOverlayTimer: TTimer;
    FUsingOverlay: Boolean;
    FOverlayVisible: Boolean;
    FLastOverlayRect: TRect;
    FSourceWidth: Integer;
    FSourceHeight: Integer;
    FSwapLayoutDimensions: Boolean;
    FLastHostWidth: Integer;
    FLastHostHeight: Integer;
    FLastSourceWidth: Integer;
    FLastSourceHeight: Integer;
    FLayoutThread: TLayoutThread;
    FVlcDir: string;
    FLastError: string;
    FLogProc: TVlcLogCallback;
    Flibvlc_new: function(argc: Integer; argv: Pointer): Pointer; cdecl;
    Flibvlc_release: procedure(p_instance: Pointer); cdecl;
    Flibvlc_media_new_location: function(p_instance: Pointer; psz_mrl: PChar): Pointer; cdecl;
    Flibvlc_media_add_option: procedure(p_media: Pointer; psz_option: PChar); cdecl;
    Flibvlc_media_release: procedure(p_media: Pointer); cdecl;
    Flibvlc_media_player_new_from_media: function(p_media: Pointer): Pointer; cdecl;
    Flibvlc_media_player_release: procedure(p_player: Pointer); cdecl;
    Flibvlc_media_player_set_hwnd: procedure(p_player: Pointer; drawable: Pointer); cdecl;
    Flibvlc_media_player_play: function(p_player: Pointer): Integer; cdecl;
    Flibvlc_media_player_stop: procedure(p_player: Pointer); cdecl;
    Flibvlc_video_get_size: function(p_player: Pointer; num: Cardinal; var px: Cardinal; var py: Cardinal): Integer; cdecl;
    Flibvlc_video_set_aspect_ratio: procedure(p_player: Pointer; psz_aspect: PChar); cdecl;
    Flibvlc_video_set_scale: procedure(p_player: Pointer; f_factor: Single); cdecl;
    function LoadLibVlc: Boolean;
    procedure UnloadLibVlc;
    function TryLoadFromDir(const Dir: string): Boolean;
    function GetAnchorScreenRect(var R: TRect): Boolean;
    function CreateOverlayHost: Boolean;
    procedure DestroyOverlayHost;
    procedure UpdateOverlayPosition(Sender: TObject);
    function CreateVideoWindow: Boolean;
    procedure ApplyCoverLayout;
  public
    constructor Create;
    destructor Destroy; override;
    function Play(const Url: string; Hwnd: HWND; SourceWidth, SourceHeight: Integer;
      SwapLayoutDimensions: Boolean; NetworkCachingMs, LiveCachingMs: Integer): Boolean;
    procedure Stop;
    procedure SetLogProc(ALogProc: TVlcLogCallback);
    property Running: Boolean read FRunning;
    property LastError: string read FLastError;
  end;

implementation

const
  WS_EX_NOACTIVATE_COMPAT = $08000000;

constructor TLayoutThread.Create(APlayer: TVlcPlayer);
begin
  inherited Create(False);
  FreeOnTerminate := False;
  FPlayer := APlayer;
end;

procedure TLayoutThread.Execute;
begin
  while not Terminated do
  begin
    Sleep(250);
    if not Terminated then
      FPlayer.ApplyCoverLayout;
  end;
end;

procedure TVlcPlayer.ApplyCoverLayout;
var
  R: TRect;
  HostWidth, HostHeight: Integer;
  SourceWidth, SourceHeight, DisplayWidth, DisplayHeight: Integer;
  VideoWidth, VideoHeight, VideoLeft, VideoTop: Integer;
  AspectRatio: string;
begin
  if not FRunning or (FMediaPlayer = nil) or
     not IsWindow(FHostHwnd) or not IsWindow(FVideoHwnd) then Exit;
  if not GetClientRect(FHostHwnd, R) then Exit;
  HostWidth := R.Right - R.Left;
  HostHeight := R.Bottom - R.Top;
  if (HostWidth <= 0) or (HostHeight <= 0) then Exit;

  { Diagnostic version:
    1. Do not poll libvlc_video_get_size in a background layout thread.
    2. Use the configured source size first to avoid waiting for VLC video size.
    3. MoveWindow uses repaint=False to avoid synchronous cross-process repaint. }
  SourceWidth := FSourceWidth;
  SourceHeight := FSourceHeight;
  if (SourceWidth <= 0) or (SourceHeight <= 0) then
  begin
    SourceWidth := HostWidth;
    SourceHeight := HostHeight;
  end;

  if (HostWidth = FLastHostWidth) and (HostHeight = FLastHostHeight) and
     (SourceWidth = FLastSourceWidth) and (SourceHeight = FLastSourceHeight) then Exit;

  DisplayWidth := SourceWidth;
  DisplayHeight := SourceHeight;
  if FSwapLayoutDimensions then
  begin
    DisplayWidth := SourceHeight;
    DisplayHeight := SourceWidth;
  end;
  if (DisplayWidth <= 0) or (DisplayHeight <= 0) then Exit;

  AspectRatio := 'unavailable';
  if @Flibvlc_video_set_scale <> nil then
    Flibvlc_video_set_scale(FMediaPlayer, 0.0);
  if @Flibvlc_video_set_aspect_ratio <> nil then
  begin
    AspectRatio := IntToStr(DisplayWidth) + ':' + IntToStr(DisplayHeight);
    Flibvlc_video_set_aspect_ratio(FMediaPlayer, PChar(AspectRatio));
  end;

  if DisplayWidth * HostHeight > DisplayHeight * HostWidth then
  begin
    VideoHeight := HostHeight;
    VideoWidth := MulDiv(DisplayWidth, HostHeight, DisplayHeight);
    VideoLeft := (HostWidth - VideoWidth) div 2;
    VideoTop := 0;
  end
  else
  begin
    VideoWidth := HostWidth;
    VideoHeight := MulDiv(DisplayHeight, HostWidth, DisplayWidth);
    VideoLeft := 0;
    VideoTop := (HostHeight - VideoHeight) div 2;
  end;

  MoveWindow(FVideoHwnd, VideoLeft, VideoTop, VideoWidth, VideoHeight, False);
  FLastHostWidth := HostWidth;
  FLastHostHeight := HostHeight;
  FLastSourceWidth := SourceWidth;
  FLastSourceHeight := SourceHeight;
end;

constructor TVlcPlayer.Create;
begin
  inherited Create;
  FhLibVlcCore := 0;
  FhLibVlc := 0;
  FVlcInstance := nil;
  FMedia := nil;
  FMediaPlayer := nil;
  FRunning := False;
  FHostHwnd := 0;
  FVideoHwnd := 0;
  FAnchorHwnd := 0;
  FOverlayHwnd := 0;
  FOverlayTimer := nil;
  FUsingOverlay := False;
  FOverlayVisible := False;
  SetRectEmpty(FLastOverlayRect);
  FSourceWidth := 0;
  FSourceHeight := 0;
  FSwapLayoutDimensions := False;
  FLastHostWidth := -1;
  FLastHostHeight := -1;
  FLastSourceWidth := -1;
  FLastSourceHeight := -1;
  FLayoutThread := nil;
  FVlcDir := '';
  FLastError := '';
  FLogProc := nil;
  Flibvlc_new := nil;
  Flibvlc_release := nil;
  Flibvlc_media_new_location := nil;
  Flibvlc_media_add_option := nil;
  Flibvlc_media_release := nil;
  Flibvlc_media_player_new_from_media := nil;
  Flibvlc_media_player_release := nil;
  Flibvlc_media_player_set_hwnd := nil;
  Flibvlc_media_player_play := nil;
  Flibvlc_media_player_stop := nil;
  Flibvlc_video_get_size := nil;
  Flibvlc_video_set_aspect_ratio := nil;
  Flibvlc_video_set_scale := nil;
end;

destructor TVlcPlayer.Destroy;
begin
  Stop;
  UnloadLibVlc;
  inherited Destroy;
end;

procedure TVlcPlayer.SetLogProc(ALogProc: TVlcLogCallback);
begin
  FLogProc := ALogProc;
end;

function TVlcPlayer.TryLoadFromDir(const Dir: string): Boolean;
var
  CorePath, VlcPath, OldDir: string;
begin
  Result := False;
  if Dir = '' then Exit;
  CorePath := Dir + '\libvlccore.dll';
  VlcPath := Dir + '\libvlc.dll';
  if not FileExists(CorePath) or not FileExists(VlcPath) then Exit;

  OldDir := GetCurrentDir;
  SetCurrentDir(Dir);
  try
    FhLibVlcCore := LoadLibrary(PChar(CorePath));
    if FhLibVlcCore = 0 then Exit;
    FhLibVlc := LoadLibrary(PChar(VlcPath));
    if FhLibVlc = 0 then
    begin
      FreeLibrary(FhLibVlcCore);
      FhLibVlcCore := 0;
      Exit;
    end;

    @Flibvlc_new := GetProcAddress(FhLibVlc, 'libvlc_new');
    @Flibvlc_release := GetProcAddress(FhLibVlc, 'libvlc_release');
    @Flibvlc_media_new_location := GetProcAddress(FhLibVlc, 'libvlc_media_new_location');
    @Flibvlc_media_add_option := GetProcAddress(FhLibVlc, 'libvlc_media_add_option');
    @Flibvlc_media_release := GetProcAddress(FhLibVlc, 'libvlc_media_release');
    @Flibvlc_media_player_new_from_media := GetProcAddress(FhLibVlc, 'libvlc_media_player_new_from_media');
    @Flibvlc_media_player_release := GetProcAddress(FhLibVlc, 'libvlc_media_player_release');
    @Flibvlc_media_player_set_hwnd := GetProcAddress(FhLibVlc, 'libvlc_media_player_set_hwnd');
    @Flibvlc_media_player_play := GetProcAddress(FhLibVlc, 'libvlc_media_player_play');
    @Flibvlc_media_player_stop := GetProcAddress(FhLibVlc, 'libvlc_media_player_stop');
    @Flibvlc_video_get_size := GetProcAddress(FhLibVlc, 'libvlc_video_get_size');
    @Flibvlc_video_set_aspect_ratio := GetProcAddress(FhLibVlc, 'libvlc_video_set_aspect_ratio');
    @Flibvlc_video_set_scale := GetProcAddress(FhLibVlc, 'libvlc_video_set_scale');
    if (@Flibvlc_new = nil) or (@Flibvlc_media_new_location = nil) or
       (@Flibvlc_media_add_option = nil) or (@Flibvlc_media_player_new_from_media = nil) or
       (@Flibvlc_media_player_set_hwnd = nil) or (@Flibvlc_media_player_play = nil) then
    begin
      UnloadLibVlc;
      Exit;
    end;
    FVlcDir := Dir;
    FLastError := '';
    Result := True;
  finally
    SetCurrentDir(PChar(OldDir));
  end;
end;

function TVlcPlayer.LoadLibVlc: Boolean;
var
  SearchDirs: array[0..5] of string;
  I: Integer;
begin
  if (FhLibVlcCore <> 0) and (FhLibVlc <> 0) then
  begin
    Result := True;
    Exit;
  end;
  SearchDirs[0] := ExtractFilePath(ParamStr(0));
  SearchDirs[1] := ExtractFilePath(ParamStr(0)) + 'vlc';
  SearchDirs[2] := 'D:\VLC';
  SearchDirs[3] := 'C:\Program Files\VideoLAN\VLC';
  SearchDirs[4] := 'C:\Program Files (x86)\VideoLAN\VLC';
  SearchDirs[5] := 'C:\VLC';
  for I := 0 to 5 do
  begin
    if TryLoadFromDir(SearchDirs[I]) then
    begin
      Result := True;
      Exit;
    end;
  end;
  FLastError := '未找到可用的32位libVLC，请在程序目录的vlc子目录部署libvlc.dll、libvlccore.dll和plugins。';
  Result := False;
end;

procedure TVlcPlayer.UnloadLibVlc;
begin
  if FhLibVlc <> 0 then
  begin
    FreeLibrary(FhLibVlc);
    FhLibVlc := 0;
  end;
  if FhLibVlcCore <> 0 then
  begin
    FreeLibrary(FhLibVlcCore);
    FhLibVlcCore := 0;
  end;
  Flibvlc_new := nil;
  Flibvlc_release := nil;
  Flibvlc_media_new_location := nil;
  Flibvlc_media_add_option := nil;
  Flibvlc_media_release := nil;
  Flibvlc_media_player_new_from_media := nil;
  Flibvlc_media_player_release := nil;
  Flibvlc_media_player_set_hwnd := nil;
  Flibvlc_media_player_play := nil;
  Flibvlc_media_player_stop := nil;
  Flibvlc_video_get_size := nil;
  Flibvlc_video_set_aspect_ratio := nil;
  Flibvlc_video_set_scale := nil;
end;

function TVlcPlayer.GetAnchorScreenRect(var R: TRect): Boolean;
var
  Origin: TPoint;
begin
  Result := False;
  if (FAnchorHwnd = 0) or not IsWindow(FAnchorHwnd) then Exit;
  if not GetClientRect(FAnchorHwnd, R) then Exit;
  Origin.X := R.Left;
  Origin.Y := R.Top;
  if not ClientToScreen(FAnchorHwnd, Origin) then Exit;
  OffsetRect(R, Origin.X - R.Left, Origin.Y - R.Top);
  Result := (R.Right > R.Left) and (R.Bottom > R.Top);
end;

function TVlcPlayer.CreateOverlayHost: Boolean;
var
  R: TRect;
begin
  Result := False;
  if not GetAnchorScreenRect(R) then Exit;
  FOverlayHwnd := CreateWindowEx(WS_EX_TOOLWINDOW or WS_EX_NOACTIVATE_COMPAT,
    'STATIC', '', WS_POPUP or WS_CLIPSIBLINGS or WS_CLIPCHILDREN,
    R.Left, R.Top, R.Right - R.Left, R.Bottom - R.Top, 0, 0, HInstance, nil);
  if FOverlayHwnd = 0 then Exit;
  FHostHwnd := FOverlayHwnd;
  FUsingOverlay := True;
  FOverlayVisible := False;
  SetRectEmpty(FLastOverlayRect);
  Result := True;
end;

procedure TVlcPlayer.DestroyOverlayHost;
begin
  if FOverlayTimer <> nil then
  begin
    FOverlayTimer.Enabled := False;
    FOverlayTimer.Free;
    FOverlayTimer := nil;
  end;
  if FOverlayHwnd <> 0 then
  begin
    DestroyWindow(FOverlayHwnd);
    FOverlayHwnd := 0;
  end;
  FUsingOverlay := False;
  FOverlayVisible := False;
  SetRectEmpty(FLastOverlayRect);
  FAnchorHwnd := 0;
end;

procedure TVlcPlayer.UpdateOverlayPosition(Sender: TObject);
var
  R: TRect;
  TargetRoot, InsertAfter: HWND;
  PositionFlags: UINT;
  OverlayAboveTarget, RectChanged, WasVisible: Boolean;
begin
  if not FUsingOverlay or (FOverlayHwnd = 0) then Exit;
  if (FAnchorHwnd = 0) or not IsWindow(FAnchorHwnd) or
     not IsWindowVisible(FAnchorHwnd) then
  begin
    if FOverlayVisible then
    begin
      ShowWindow(FOverlayHwnd, SW_HIDE);
      FOverlayVisible := False;
    end;
    Exit;
  end;
  TargetRoot := GetAncestor(FAnchorHwnd, GA_ROOT);
  if (TargetRoot = 0) or IsIconic(TargetRoot) then
  begin
    if FOverlayVisible then
    begin
      ShowWindow(FOverlayHwnd, SW_HIDE);
      FOverlayVisible := False;
    end;
    Exit;
  end;
  if not GetAnchorScreenRect(R) then
  begin
    if FOverlayVisible then
    begin
      ShowWindow(FOverlayHwnd, SW_HIDE);
      FOverlayVisible := False;
    end;
    Exit;
  end;

  { Keep the local overlay immediately above the external application,
    while allowing unrelated applications above it to occlude the preview. }
  WasVisible := FOverlayVisible;
  RectChanged := not EqualRect(R, FLastOverlayRect);
  OverlayAboveTarget := GetWindow(TargetRoot, GW_HWNDPREV) = FOverlayHwnd;
  if OverlayAboveTarget and not RectChanged and WasVisible then Exit;

  PositionFlags := SWP_NOACTIVATE;
  if not WasVisible then
    PositionFlags := PositionFlags or SWP_SHOWWINDOW;
  if OverlayAboveTarget then
  begin
    if RectChanged then
      SetWindowPos(FOverlayHwnd, 0, R.Left, R.Top,
        R.Right - R.Left, R.Bottom - R.Top, PositionFlags or SWP_NOZORDER)
    else
      ShowWindow(FOverlayHwnd, SW_SHOWNOACTIVATE);
  end
  else
  begin
  InsertAfter := GetWindow(TargetRoot, GW_HWNDPREV);
  if InsertAfter = 0 then
    InsertAfter := HWND_TOP;
  SetWindowPos(FOverlayHwnd, InsertAfter, R.Left, R.Top,
      R.Right - R.Left, R.Bottom - R.Top, PositionFlags);
  end;
  FOverlayVisible := True;
  FLastOverlayRect := R;
  if FRunning and (RectChanged or not WasVisible) then
    ApplyCoverLayout;
end;

function TVlcPlayer.CreateVideoWindow: Boolean;
begin
  FVideoHwnd := CreateWindowEx(0, 'STATIC', '', WS_CHILD or WS_VISIBLE or
    WS_CLIPSIBLINGS or WS_CLIPCHILDREN, 0, 0, 1, 1, FHostHwnd, 0, HInstance, nil);
  Result := FVideoHwnd <> 0;
end;

function TVlcPlayer.Play(const Url: string; Hwnd: HWND; SourceWidth, SourceHeight: Integer;
  SwapLayoutDimensions: Boolean; NetworkCachingMs, LiveCachingMs: Integer): Boolean;
var
  PluginsPath, PluginArg, NetworkOption, LiveOption: string;
  Argv: array[0..3] of PChar;
  ArgCount: Integer;
  HostProcessId, CurrentProcessId: DWORD;
begin
  Result := False;
  if FRunning then Stop;
  if Url = '' then begin FLastError := '预览地址为空。'; Exit; end;
  if not IsWindow(Hwnd) then begin FLastError := '预览目标窗口句柄无效。'; Exit; end;
  if not LoadLibVlc then Exit;

  HostProcessId := 0;
  GetWindowThreadProcessId(Hwnd, HostProcessId);
  CurrentProcessId := GetCurrentProcessId;

  if NetworkCachingMs < 0 then NetworkCachingMs := 0;
  if LiveCachingMs < 0 then LiveCachingMs := 0;
  ArgCount := 0;
  Argv[ArgCount] := '--no-video-title-show'; Inc(ArgCount);
  Argv[ArgCount] := '--no-xlib'; Inc(ArgCount);
  Argv[ArgCount] := '--quiet'; Inc(ArgCount);
  PluginsPath := FVlcDir + '\plugins';
  if DirectoryExists(PluginsPath) then
  begin
    PluginArg := '--plugin-path=' + PluginsPath;
    Argv[ArgCount] := PChar(PluginArg); Inc(ArgCount);
  end;
  FVlcInstance := Flibvlc_new(ArgCount, @Argv[0]);
  if FVlcInstance = nil then begin FLastError := '创建 VLC 实例失败。'; Exit; end;
  FMedia := Flibvlc_media_new_location(FVlcInstance, PChar(Url));
  if FMedia = nil then
  begin
    FLastError := '创建预览媒体失败。';
    Flibvlc_release(FVlcInstance);
    FVlcInstance := nil;
    Exit;
  end;

  NetworkOption := ':network-caching=' + IntToStr(NetworkCachingMs);
  LiveOption := ':live-caching=' + IntToStr(LiveCachingMs);
  Flibvlc_media_add_option(FMedia, PChar(NetworkOption));
  Flibvlc_media_add_option(FMedia, PChar(LiveOption));
  if Assigned(FLogProc) then
    FLogProc('[信息] [预览渲染] VLC缓存参数已应用：network-caching=' + IntToStr(NetworkCachingMs) +
      '，live-caching=' + IntToStr(LiveCachingMs));

  FMediaPlayer := Flibvlc_media_player_new_from_media(FMedia);
  if FMediaPlayer = nil then
  begin
    FLastError := '创建预览播放器失败。';
    Flibvlc_media_release(FMedia);
    FMedia := nil;
    Flibvlc_release(FVlcInstance);
    FVlcInstance := nil;
    Exit;
  end;
  FAnchorHwnd := Hwnd;
  if HostProcessId <> CurrentProcessId then
  begin
    if not CreateOverlayHost then
    begin
      FLastError := '创建跨进程预览覆盖窗口失败。';
      Flibvlc_media_player_release(FMediaPlayer); FMediaPlayer := nil;
      Flibvlc_media_release(FMedia); FMedia := nil;
      Flibvlc_release(FVlcInstance); FVlcInstance := nil;
      Exit;
    end;
    if Assigned(FLogProc) then
      FLogProc('[信息] [预览渲染] 第三方目标使用本进程覆盖容器：target_hwnd=' +
        IntToStr(Integer(Hwnd)) + '，overlay_hwnd=' + IntToStr(Integer(FOverlayHwnd)));
  end
  else
    FHostHwnd := Hwnd;
  FSourceWidth := SourceWidth;
  FSourceHeight := SourceHeight;
  FSwapLayoutDimensions := SwapLayoutDimensions;
  FLastHostWidth := -1;
  FLastHostHeight := -1;
  FLastSourceWidth := -1;
  FLastSourceHeight := -1;
  if not CreateVideoWindow then
  begin
    FLastError := '创建预览视频子窗口失败。';
    Flibvlc_media_player_release(FMediaPlayer); FMediaPlayer := nil;
    Flibvlc_media_release(FMedia); FMedia := nil;
    Flibvlc_release(FVlcInstance); FVlcInstance := nil;
    FHostHwnd := 0;
    DestroyOverlayHost;
    Exit;
  end;
  Flibvlc_media_player_set_hwnd(FMediaPlayer, Pointer(FVideoHwnd));
  if Flibvlc_media_player_play(FMediaPlayer) <> 0 then
  begin
    FLastError := '启动预览播放失败。';
    Flibvlc_media_player_release(FMediaPlayer); FMediaPlayer := nil;
    Flibvlc_media_release(FMedia); FMedia := nil;
    Flibvlc_release(FVlcInstance); FVlcInstance := nil;
    DestroyWindow(FVideoHwnd); FVideoHwnd := 0; FHostHwnd := 0;
    DestroyOverlayHost;
    Exit;
  end;
  FRunning := True;
  ApplyCoverLayout;
  if FUsingOverlay then
  begin
    FOverlayTimer := TTimer.Create(nil);
    FOverlayTimer.Interval := 200;
    FOverlayTimer.OnTimer := UpdateOverlayPosition;
    FOverlayTimer.Enabled := True;
    UpdateOverlayPosition(nil);
  end;
  { 覆盖窗口不使用后台布局线程，避免窗口操作与播放释放产生竞争。 }
  FLayoutThread := nil;
  FLastError := '';
  Result := True;
end;

procedure TVlcPlayer.Stop;
begin
  if FOverlayTimer <> nil then
    FOverlayTimer.Enabled := False;
  if FLayoutThread <> nil then
  begin
    FLayoutThread.Terminate;
    FLayoutThread.WaitFor;
    FLayoutThread.Free;
    FLayoutThread := nil;
  end;
  if FMediaPlayer <> nil then
  begin
    if @Flibvlc_media_player_stop <> nil then
      Flibvlc_media_player_stop(FMediaPlayer);
    Flibvlc_media_player_release(FMediaPlayer);
    FMediaPlayer := nil;
  end;
  if FMedia <> nil then
  begin
    Flibvlc_media_release(FMedia);
    FMedia := nil;
  end;
  if FVlcInstance <> nil then
  begin
    Flibvlc_release(FVlcInstance);
    FVlcInstance := nil;
  end;
  if FVideoHwnd <> 0 then
  begin
    DestroyWindow(FVideoHwnd);
    FVideoHwnd := 0;
  end;
  DestroyOverlayHost;
  FHostHwnd := 0;
  FAnchorHwnd := 0;
  FRunning := False;
end;

end.
