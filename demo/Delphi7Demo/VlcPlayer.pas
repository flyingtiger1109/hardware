unit VlcPlayer;

interface

uses Windows, SysUtils, Classes;

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
  VlcWidth, VlcHeight: Cardinal;
  AspectRatio: string;
begin
  if not FRunning or (FMediaPlayer = nil) or
     not IsWindow(FHostHwnd) or not IsWindow(FVideoHwnd) then Exit;
  if not GetClientRect(FHostHwnd, R) then Exit;
  HostWidth := R.Right - R.Left;
  HostHeight := R.Bottom - R.Top;
  if (HostWidth <= 0) or (HostHeight <= 0) then Exit;

  SourceWidth := FSourceWidth;
  SourceHeight := FSourceHeight;
  if @Flibvlc_video_get_size <> nil then
  begin
    VlcWidth := 0;
    VlcHeight := 0;
    if (Flibvlc_video_get_size(FMediaPlayer, 0, VlcWidth, VlcHeight) <> 0) or
       (VlcWidth = 0) or (VlcHeight = 0) then Exit;
    SourceWidth := VlcWidth;
    SourceHeight := VlcHeight;
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

  AspectRatio := '接口不可用';
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

  MoveWindow(FVideoHwnd, VideoLeft, VideoTop, VideoWidth, VideoHeight, True);
  FLastHostWidth := HostWidth;
  FLastHostHeight := HostHeight;
  FLastSourceWidth := SourceWidth;
  FLastSourceHeight := SourceHeight;
  if Assigned(FLogProc) then
    FLogProc('[信息] [预览渲染] 覆盖布局已应用：原始视频=' + IntToStr(SourceWidth) + 'x' + IntToStr(SourceHeight) +
      '，显示尺寸=' + IntToStr(DisplayWidth) + 'x' + IntToStr(DisplayHeight) +
      '，目标窗口=' + IntToStr(HostWidth) + 'x' + IntToStr(HostHeight) +
      '，视频窗口位置=' + IntToStr(VideoLeft) + ',' + IntToStr(VideoTop) +
      '，视频窗口尺寸=' + IntToStr(VideoWidth) + 'x' + IntToStr(VideoHeight) +
      '，VLC显示比例=' + AspectRatio);
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
begin
  Result := False;
  if FRunning then Stop;
  if Url = '' then begin FLastError := '预览地址为空。'; Exit; end;
  if not IsWindow(Hwnd) then begin FLastError := '预览目标窗口句柄无效。'; Exit; end;
  if not LoadLibVlc then Exit;

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
    Exit;
  end;
  FRunning := True;
  ApplyCoverLayout;
  FLayoutThread := TLayoutThread.Create(Self);
  FLastError := '';
  Result := True;
end;

procedure TVlcPlayer.Stop;
begin
  if FLayoutThread <> nil then
  begin
    FLayoutThread.Terminate;
    FLayoutThread.WaitFor;
    FLayoutThread.Free;
    FLayoutThread := nil;
  end;
  if FMediaPlayer <> nil then
  begin
    if @Flibvlc_media_player_stop <> nil then Flibvlc_media_player_stop(FMediaPlayer);
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
  FHostHwnd := 0;
  FRunning := False;
end;

end.