unit MainUnit;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ExtCtrls;

const
  DLL_NAME = 'HZCYKJTHardWare.dll';

  WM_DLL_EVENT_JSON = WM_USER + 101;
  WM_PREVIEW_DLL_RESULT = WM_USER + 102;

type
  THZCYKJTHardWareEventCallback = procedure(EventJson: PAnsiChar); stdcall;

  TFormMain = class(TForm)
    PanelTop: TPanel;
    BtnInit: TButton;
    BtnRelease: TButton;
    BtnSwitch1: TButton;
    BtnSwitch2: TButton;
    BtnStartProcess: TButton;
    BtnEndProcess: TButton;
    EdtSaveDir: TEdit;
    BtnCameraPreview: TButton;
    BtnStopCamPreview: TButton;
    BtnFpPreview: TButton;
    BtnStopFpPreview: TButton;
    BtnFaceCapture: TButton;
    BtnFpCapture: TButton;
    BtnOCR: TButton;
    BtnNFC: TButton;
    BtnIrisCapture: TButton;
    LblAuthSample: TLabel;
    LblAuthZJHM: TLabel;
    LblAuthZJLB: TLabel;
    LblAuthGJDQDM: TLabel;
    LblAuthXM: TLabel;
    LblAuthXB: TLabel;
    LblAuthCSRQ: TLabel;
    LblAuthKADM: TLabel;
    EdtAuthZJHM: TEdit;
    EdtAuthZJLB: TEdit;
    EdtAuthGJDQDM: TEdit;
    EdtAuthXM: TEdit;
    EdtAuthXB: TEdit;
    EdtAuthCSRQ: TEdit;
    EdtAuthKADM: TEdit;
    PanelCamera: TPanel;
    PanelFingerprint: TPanel;
    PanelIris: TPanel;
    MemoLog: TMemo;
    procedure FormCreate(Sender: TObject);
    procedure FormDestroy(Sender: TObject);
    procedure BtnInitClick(Sender: TObject);
    procedure BtnReleaseClick(Sender: TObject);
    procedure BtnSwitch1Click(Sender: TObject);
    procedure BtnSwitch2Click(Sender: TObject);
    procedure BtnStartProcessClick(Sender: TObject);
    procedure BtnEndProcessClick(Sender: TObject);
    procedure BtnCameraPreviewClick(Sender: TObject);
    procedure BtnStopCamPreviewClick(Sender: TObject);
    procedure BtnFpPreviewClick(Sender: TObject);
    procedure BtnStopFpPreviewClick(Sender: TObject);
    procedure BtnFaceCaptureClick(Sender: TObject);
    procedure BtnFpCaptureClick(Sender: TObject);
    procedure BtnOCRClick(Sender: TObject);
    procedure BtnNFCClick(Sender: TObject);
    procedure BtnIrisCaptureClick(Sender: TObject);
    procedure BtnAuthorizeClick(Sender: TObject);
  private
    FInitialized: Boolean;
    procedure Log(const S: string);
    procedure LogRet(const Name: string; Ret: Integer);
    procedure OnEventJson(const Json: string);
    procedure RunPreviewDllCallAsync(const Name: string; TargetHwnd: HWND; IsCamera: Boolean);

    procedure WMDllEventJson(var Msg: TMessage); message WM_DLL_EVENT_JSON;
    procedure WMPreviewDllResult(var Msg: TMessage); message WM_PREVIEW_DLL_RESULT;
  end;

var
  FormMain: TFormMain;

implementation

{$R *.dfm}

var
  GSelf: TFormMain;

function HZCYKJTHardWare_InitSdk: Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_ReleaseSdk: Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_RegisterEventCallback(Callback: THZCYKJTHardWareEventCallback): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_SwitchTerminal(TerminalIndex: Integer): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StartProcess(SaveDir: PAnsiChar): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_EndProcess: Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StartCameraPreview(Hwnd: Pointer): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StopCameraPreview: Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StartFingerprintPreview(Hwnd: Pointer): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StopFingerprintPreview: Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StartIrisPreview(Hwnd: Pointer): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_StopIrisPreview: Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_CaptureCameraImage(SaveDir: PAnsiChar): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_CaptureFingerprintImage(SaveDir: PAnsiChar): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_CaptureIrisImage(SaveDir: PAnsiChar): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_RequestOCR(SaveDir: PAnsiChar): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_RequestNfcCard(SaveDir: PAnsiChar): Integer; stdcall; external DLL_NAME;
function HZCYKJTHardWare_RequestAuthorize(ZJHM, ZJLB, GJDQDM, XM, XB, CSRQ, KADM: PAnsiChar): Integer; stdcall; external DLL_NAME;

type
  PStringData = ^string;

  PPreviewResult = ^TPreviewResult;
  TPreviewResult = record
    Name: string;
    Ret: Integer;
  end;

  TPreviewCallThread = class(TThread)
  private
    FName: string;
    FTargetHwnd: HWND;
    FIsCamera: Boolean;
  protected
    procedure Execute; override;
  public
    constructor Create(const AName: string; ATargetHwnd: HWND; AIsCamera: Boolean);
  end;

function SafeUtf8ToStr(P: PAnsiChar): string;
var
  WideLen, AnsiLen: Integer;
  WideText: WideString;
  S: AnsiString;
begin
  if P = nil then
  begin
    Result := '';
    Exit;
  end;

  S := AnsiString(P);
  if S = '' then
  begin
    Result := '';
    Exit;
  end;

  WideLen := MultiByteToWideChar(CP_UTF8, 0, PAnsiChar(S), Length(S), nil, 0);
  if WideLen <= 0 then
  begin
    Result := string(S);
    Exit;
  end;

  SetLength(WideText, WideLen);
  MultiByteToWideChar(CP_UTF8, 0, PAnsiChar(S), Length(S), PWideChar(WideText), WideLen);

  AnsiLen := WideCharToMultiByte(CP_ACP, 0, PWideChar(WideText), WideLen, nil, 0, nil, nil);
  SetLength(Result, AnsiLen);
  if AnsiLen > 0 then
    WideCharToMultiByte(CP_ACP, 0, PWideChar(WideText), WideLen, PChar(Result), AnsiLen, nil, nil);
end;

function Cn(const Utf8Text: AnsiString): string;
begin
  Result := SafeUtf8ToStr(PAnsiChar(Utf8Text));
end;

function ExtractJsonStr(const Json, Key: string): string;
var
  K, I: Integer;
  Escaped: Boolean;
begin
  Result := '';
  K := Pos('"' + Key + '"', Json);
  if K = 0 then
    Exit;

  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', ':', #9, #10, #13]) do
    Inc(K);

  if (K > Length(Json)) or (Json[K] <> '"') then
    Exit;

  Inc(K);
  Escaped := False;

  for I := K to Length(Json) do
  begin
    if Escaped then
    begin
      case Json[I] of
        'n': Result := Result + #10;
        'r': Result := Result + #13;
        't': Result := Result + #9;
      else
        Result := Result + Json[I];
      end;
      Escaped := False;
    end
    else if Json[I] = '\' then
      Escaped := True
    else if Json[I] = '"' then
      Exit
    else
      Result := Result + Json[I];
  end;
end;

function ExtractJsonInt(const Json, Key: string): Integer;
var
  S: string;
  K: Integer;
begin
  S := ExtractJsonStr(Json, Key);
  if S <> '' then
  begin
    Result := StrToIntDef(S, 0);
    Exit;
  end;

  Result := 0;
  K := Pos('"' + Key + '"', Json);
  if K = 0 then
    Exit;

  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', ':', #9, #10, #13]) do
    Inc(K);

  S := '';
  while (K <= Length(Json)) and (Json[K] in ['0'..'9', '-']) do
  begin
    S := S + Json[K];
    Inc(K);
  end;

  Result := StrToIntDef(S, 0);
end;

constructor TPreviewCallThread.Create(const AName: string; ATargetHwnd: HWND; AIsCamera: Boolean);
begin
  inherited Create(True);
  FreeOnTerminate := True;

  FName := AName;
  FTargetHwnd := ATargetHwnd;
  FIsCamera := AIsCamera;
end;

procedure TPreviewCallThread.Execute;
var
  Ret: Integer;
  P: PPreviewResult;
begin
  if FIsCamera then
    Ret := HZCYKJTHardWare_StartCameraPreview(Pointer(FTargetHwnd))
  else
    Ret := HZCYKJTHardWare_StartFingerprintPreview(Pointer(FTargetHwnd));

  if GSelf = nil then
    Exit;

  New(P);
  P^.Name := FName;
  P^.Ret := Ret;

  if not PostMessage(GSelf.Handle, WM_PREVIEW_DLL_RESULT, 0, Longint(P)) then
    Dispose(P);
end;

procedure EventCallback(EventJson: PAnsiChar); stdcall;
var
  Json: string;
  P: PStringData;
begin
  if (EventJson = nil) or (GSelf = nil) then
    Exit;

  Json := SafeUtf8ToStr(EventJson);

  New(P);
  P^ := Json;

  if not PostMessage(GSelf.Handle, WM_DLL_EVENT_JSON, 0, Longint(P)) then
    Dispose(P);
end;

procedure TFormMain.OnEventJson(const Json: string);
var
  ResType, Msg, Mrz, IcNum, SavePath: string;
  EvtType, Status: Integer;
begin
  EvtType := ExtractJsonInt(Json, 'event_type');
  Status := ExtractJsonInt(Json, 'status');
  ResType := ExtractJsonStr(Json, 'resource_type');
  Msg := ExtractJsonStr(Json, 'message');
  Mrz := ExtractJsonStr(Json, 'mrz');
  IcNum := ExtractJsonStr(Json, 'ic_number');
  SavePath := ExtractJsonStr(Json, 'save_path');

  Log(Format('[Event] type=%d status=%d resource=%s', [EvtType, Status, ResType]));
  if Mrz <> '' then
    Log('  MRZ: ' + Mrz);
  if IcNum <> '' then
    Log('  IC: ' + IcNum);
  if SavePath <> '' then
    Log('  Save: ' + SavePath);
  if Msg <> '' then
    Log('  Msg: ' + Msg);
  if ResType = 'authorization' then
  begin
    Log('  auth_result=' + IntToStr(ExtractJsonInt(Json, 'auth_result')));
    Log('  ZJHM=' + ExtractJsonStr(Json, 'ZJHM'));
    Log('  ZJLB=' + ExtractJsonStr(Json, 'ZJLB'));
    Log('  GJDQDM=' + ExtractJsonStr(Json, 'GJDQDM'));
    Log('  XM=' + ExtractJsonStr(Json, 'XM'));
    Log('  XB=' + ExtractJsonStr(Json, 'XB'));
    Log('  CSRQ=' + ExtractJsonStr(Json, 'CSRQ'));
    Log('  KADM=' + ExtractJsonStr(Json, 'KADM'));
  end;
end;

procedure TFormMain.FormCreate(Sender: TObject);
begin
  GSelf := Self;
  Caption := 'HZCYKJTHardWare DLL Test';
  FInitialized := False;
  MemoLog.Clear;
  Log('Ready. Click [InitSdk] to start.');
end;

procedure TFormMain.FormDestroy(Sender: TObject);
begin
  GSelf := nil;
  if FInitialized then
    HZCYKJTHardWare_ReleaseSdk;
end;

procedure TFormMain.Log(const S: string);
begin
  MemoLog.Lines.Add(FormatDateTime('hh:nn:ss.zzz', Now) + '  ' + S);
end;

procedure TFormMain.LogRet(const Name: string; Ret: Integer);
begin
  Log(Format('%s = %d', [Name, Ret]));
end;

procedure TFormMain.WMDllEventJson(var Msg: TMessage);
var
  P: PStringData;
begin
  P := PStringData(Msg.LParam);
  if P = nil then
    Exit;

  try
    OnEventJson(P^);
  finally
    Dispose(P);
  end;
end;

procedure TFormMain.WMPreviewDllResult(var Msg: TMessage);
var
  P: PPreviewResult;
begin
  P := PPreviewResult(Msg.LParam);
  if P = nil then
    Exit;

  try
    LogRet(P^.Name, P^.Ret);
  finally
    Dispose(P);
  end;
end;

procedure TFormMain.RunPreviewDllCallAsync(const Name: string; TargetHwnd: HWND; IsCamera: Boolean);
var
  Th: TPreviewCallThread;
begin
  Log(Format('%s submit to worker thread: hwnd=%s', [Name, IntToStr(Integer(TargetHwnd))]));

  Th := TPreviewCallThread.Create(Name, TargetHwnd, IsCamera);
  Th.Resume;
end;

procedure TFormMain.BtnInitClick(Sender: TObject);
var
  Ret: Integer;
begin
  if FInitialized then
  begin
    Log('Already initialized');
    Exit;
  end;

  Ret := HZCYKJTHardWare_InitSdk;
  LogRet('InitSdk', Ret);

  if Ret = 1 then
  begin
    FInitialized := True;
    LogRet('RegisterEventCallback', HZCYKJTHardWare_RegisterEventCallback(@EventCallback));
  end;
end;

procedure TFormMain.BtnReleaseClick(Sender: TObject);
begin
  if not FInitialized then
    Exit;

  LogRet('ReleaseSdk', HZCYKJTHardWare_ReleaseSdk);
  FInitialized := False;
end;

procedure TFormMain.BtnSwitch1Click(Sender: TObject);
begin
  LogRet('SwitchTerminal(1)', HZCYKJTHardWare_SwitchTerminal(1));
end;

procedure TFormMain.BtnSwitch2Click(Sender: TObject);
begin
  LogRet('SwitchTerminal(2)', HZCYKJTHardWare_SwitchTerminal(2));
end;

procedure TFormMain.BtnStartProcessClick(Sender: TObject);
begin
  LogRet('StartProcess', HZCYKJTHardWare_StartProcess(PAnsiChar(AnsiString(EdtSaveDir.Text))));
end;

procedure TFormMain.BtnEndProcessClick(Sender: TObject);
begin
  LogRet('EndProcess', HZCYKJTHardWare_EndProcess);
end;

procedure TFormMain.BtnCameraPreviewClick(Sender: TObject);
begin
  RunPreviewDllCallAsync('StartCameraPreview', PanelCamera.Handle, True);
end;

procedure TFormMain.BtnStopCamPreviewClick(Sender: TObject);
begin
  LogRet('StopCameraPreview', HZCYKJTHardWare_StopCameraPreview);
end;

procedure TFormMain.BtnFpPreviewClick(Sender: TObject);
begin
  RunPreviewDllCallAsync('StartFingerprintPreview', PanelFingerprint.Handle, False);
end;

procedure TFormMain.BtnStopFpPreviewClick(Sender: TObject);
begin
  LogRet('StopFingerprintPreview', HZCYKJTHardWare_StopFingerprintPreview);
end;

procedure TFormMain.BtnFaceCaptureClick(Sender: TObject);
begin
  LogRet('CaptureCameraImage', HZCYKJTHardWare_CaptureCameraImage(PAnsiChar(AnsiString(EdtSaveDir.Text))));
end;

procedure TFormMain.BtnFpCaptureClick(Sender: TObject);
begin
  LogRet('CaptureFingerprintImage', HZCYKJTHardWare_CaptureFingerprintImage(PAnsiChar(AnsiString(EdtSaveDir.Text))));
end;

procedure TFormMain.BtnOCRClick(Sender: TObject);
begin
  LogRet('RequestOCR', HZCYKJTHardWare_RequestOCR(PAnsiChar(AnsiString(EdtSaveDir.Text))));
end;

procedure TFormMain.BtnNFCClick(Sender: TObject);
begin
  LogRet('RequestNfcCard', HZCYKJTHardWare_RequestNfcCard(PAnsiChar(AnsiString(EdtSaveDir.Text))));
end;

procedure TFormMain.BtnIrisCaptureClick(Sender: TObject);
begin
  LogRet('CaptureIrisImage', HZCYKJTHardWare_CaptureIrisImage(PAnsiChar(AnsiString(EdtSaveDir.Text))));
end;

procedure TFormMain.BtnAuthorizeClick(Sender: TObject);
var
  Ret: Integer;
  ZJHM, ZJLB, GJDQDM, XM, XB, CSRQ, KADM: AnsiString;
begin
  ZJHM := AnsiString(EdtAuthZJHM.Text);
  ZJLB := AnsiString(EdtAuthZJLB.Text);
  GJDQDM := AnsiString(EdtAuthGJDQDM.Text);
  XM := AnsiString(EdtAuthXM.Text);
  XB := AnsiString(EdtAuthXB.Text);
  CSRQ := AnsiString(EdtAuthCSRQ.Text);
  KADM := AnsiString(EdtAuthKADM.Text);
  Log(Cn(#$E5#$B7#$B2#$E6#$8F#$90#$E4#$BA#$A4#$E6#$8E#$88#$E6#$9D#$83#$E6#$A8#$A1#$E6#$8B#$9F#$E5#$8F#$82#$E6#$95#$B0#$EF#$BC#$9A) +
    'ZJHM=' + string(ZJHM) + ', XM=' + string(XM) +
    ', ZJLB=' + string(ZJLB) + ', GJDQDM=' + string(GJDQDM) +
    ', XB=' + string(XB) + ', CSRQ=' + string(CSRQ) + ', KADM=' + string(KADM));
  Ret := HZCYKJTHardWare_RequestAuthorize(
    PAnsiChar(ZJHM),
    PAnsiChar(ZJLB),
    PAnsiChar(GJDQDM),
    PAnsiChar(XM),
    PAnsiChar(XB),
    PAnsiChar(CSRQ),
    PAnsiChar(KADM)
  );
  LogRet('RequestAuthorize', Ret);
end;

end.
