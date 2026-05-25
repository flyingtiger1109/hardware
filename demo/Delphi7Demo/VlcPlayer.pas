unit VlcPlayer;

interface

uses Windows, SysUtils, Classes;

type
  TVlcPlayer = class
  private
    FhLibVlcCore: HMODULE;
    FhLibVlc: HMODULE;
    FVlcInstance: Pointer;
    FMedia: Pointer;
    FMediaPlayer: Pointer;
    FRunning: Boolean;
    FRenderHwnd: HWND;
    FVlcDir: string;
    FLastError: string;
    // function pointers
    Flibvlc_new: function(argc: Integer; argv: Pointer): Pointer; cdecl;
    Flibvlc_release: procedure(p_instance: Pointer); cdecl;
    Flibvlc_media_new_location: function(p_instance: Pointer; psz_mrl: PChar): Pointer; cdecl;
    Flibvlc_media_release: procedure(p_media: Pointer); cdecl;
    Flibvlc_media_player_new_from_media: function(p_media: Pointer): Pointer; cdecl;
    Flibvlc_media_player_release: procedure(p_player: Pointer); cdecl;
    Flibvlc_media_player_set_hwnd: procedure(p_player: Pointer; drawable: Pointer); cdecl;
    Flibvlc_media_player_play: function(p_player: Pointer): Integer; cdecl;
    Flibvlc_media_player_stop: procedure(p_player: Pointer); cdecl;
    function LoadLibVlc: Boolean;
    procedure UnloadLibVlc;
    function TryLoadFromDir(const Dir: string): Boolean;
  public
    constructor Create;
    destructor Destroy; override;
    function Play(const Url: string; Hwnd: HWND): Boolean;
    procedure Stop;
    property Running: Boolean read FRunning;
    property LastError: string read FLastError;
  end;

implementation

function ResizeVlcChild(Child: HWND; Param: LPARAM): BOOL; stdcall;
var
  Parent: HWND;
  R: TRect;
begin
  Parent := GetParent(Child);
  if Parent <> 0 then begin
    GetClientRect(Parent, R);
    MoveWindow(Child, 0, 0, R.Right - R.Left, R.Bottom - R.Top, True);
  end;
  Result := True;
end;

type
  TResizeThread = class(TThread)
  private
    FHwnd: HWND;
  protected
    procedure Execute; override;
  public
    constructor Create(AHwnd: HWND);
  end;

constructor TResizeThread.Create(AHwnd: HWND);
begin
  inherited Create(False); // FreeOnTerminate
  FreeOnTerminate := True;
  FHwnd := AHwnd;
end;

procedure TResizeThread.Execute;
var
  I: Integer;
begin
  for I := 1 to 20 do begin
    Sleep(100);
    if not IsWindow(FHwnd) then Break;
    EnumChildWindows(FHwnd, @ResizeVlcChild, 0);
  end;
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
  FRenderHwnd := 0;
  FVlcDir := '';
  FLastError := '';
  Flibvlc_new := nil;
  Flibvlc_release := nil;
  Flibvlc_media_new_location := nil;
  Flibvlc_media_release := nil;
  Flibvlc_media_player_new_from_media := nil;
  Flibvlc_media_player_release := nil;
  Flibvlc_media_player_set_hwnd := nil;
  Flibvlc_media_player_play := nil;
  Flibvlc_media_player_stop := nil;
end;

destructor TVlcPlayer.Destroy;
begin
  Stop;
  UnloadLibVlc;
  inherited Destroy;
end;

function TVlcPlayer.TryLoadFromDir(const Dir: string): Boolean;
var
  CorePath, VlcPath: string;
  OldDir: string;
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
    @Flibvlc_media_release := GetProcAddress(FhLibVlc, 'libvlc_media_release');
    @Flibvlc_media_player_new_from_media := GetProcAddress(FhLibVlc, 'libvlc_media_player_new_from_media');
    @Flibvlc_media_player_release := GetProcAddress(FhLibVlc, 'libvlc_media_player_release');
    @Flibvlc_media_player_set_hwnd := GetProcAddress(FhLibVlc, 'libvlc_media_player_set_hwnd');
    @Flibvlc_media_player_play := GetProcAddress(FhLibVlc, 'libvlc_media_player_play');
    @Flibvlc_media_player_stop := GetProcAddress(FhLibVlc, 'libvlc_media_player_stop');

    if (@Flibvlc_new = nil) or (@Flibvlc_media_new_location = nil) or
       (@Flibvlc_media_player_new_from_media = nil) or (@Flibvlc_media_player_set_hwnd = nil) or
       (@Flibvlc_media_player_play = nil) then
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
  Flibvlc_media_release := nil;
  Flibvlc_media_player_new_from_media := nil;
  Flibvlc_media_player_release := nil;
  Flibvlc_media_player_set_hwnd := nil;
  Flibvlc_media_player_play := nil;
  Flibvlc_media_player_stop := nil;
end;

function TVlcPlayer.Play(const Url: string; Hwnd: HWND): Boolean;
var
  PluginsPath, PluginArg: string;
  Argv: array[0..3] of PChar;
  ArgCount, Retry: Integer;
begin
  Result := False;
  if FRunning then Stop;
  if Url = '' then
  begin
    FLastError := '预览地址为空。';
    Exit;
  end;
  if not IsWindow(Hwnd) then
  begin
    FLastError := '预览目标窗口无效。';
    Exit;
  end;
  if not LoadLibVlc then Exit;

  // build VLC args
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

  // create VLC instance
  FVlcInstance := Flibvlc_new(ArgCount, @Argv[0]);
  if FVlcInstance = nil then
  begin
    FLastError := '创建VLC实例失败。';
    Exit;
  end;

  // create media
  FMedia := Flibvlc_media_new_location(FVlcInstance, PChar(Url));
  if FMedia = nil then
  begin
    FLastError := '创建预览媒体失败，地址=' + Url;
    Flibvlc_release(FVlcInstance);
    FVlcInstance := nil;
    Exit;
  end;

  // create media player
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

  // set render window
  Flibvlc_media_player_set_hwnd(FMediaPlayer, Pointer(Hwnd));

  // start playing
  if Flibvlc_media_player_play(FMediaPlayer) <> 0 then
  begin
    FLastError := '启动预览播放失败。';
    Flibvlc_media_player_release(FMediaPlayer);
    FMediaPlayer := nil;
    Flibvlc_media_release(FMedia);
    FMedia := nil;
    Flibvlc_release(FVlcInstance);
    FVlcInstance := nil;
    Exit;
  end;

  FRunning := True;
  FRenderHwnd := Hwnd;
  FLastError := '';
  // VLC 子窗口异步创建，短暂重试以填满目标窗口。
  for Retry := 1 to 3 do begin
    Sleep(100);
    EnumChildWindows(Hwnd, @ResizeVlcChild, 0);
  end;
  Result := True;
end;

procedure TVlcPlayer.Stop;
begin
  if not FRunning then Exit;

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

  FRunning := False;
end;

end.
