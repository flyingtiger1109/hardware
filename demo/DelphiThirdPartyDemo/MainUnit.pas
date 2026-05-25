unit MainUnit;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ExtCtrls;

const
  DLL_NAME = 'HZCYKJTHardWare.dll';

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
  private
    FInitialized: Boolean;
    procedure Log(const S: string);
    procedure LogRet(const Name: string; Ret: Integer);
    procedure OnEvent(EventJson: PAnsiChar);
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





function SafeUtf8ToStr(P: PAnsiChar): string;
var
  WideLen, AnsiLen: Integer;
  WideText: WideString;
  S: AnsiString;
begin
  if P = nil then begin Result := ''; Exit; end;
  S := AnsiString(P);
  if S = '' then begin Result := ''; Exit; end;
  WideLen := MultiByteToWideChar(CP_UTF8, 0, PAnsiChar(S), Length(S), nil, 0);
  if WideLen <= 0 then begin Result := string(S); Exit; end;
  SetLength(WideText, WideLen);
  MultiByteToWideChar(CP_UTF8, 0, PAnsiChar(S), Length(S), PWideChar(WideText), WideLen);
  AnsiLen := WideCharToMultiByte(CP_ACP, 0, PWideChar(WideText), WideLen, nil, 0, nil, nil);
  SetLength(Result, AnsiLen);
  if AnsiLen > 0 then
    WideCharToMultiByte(CP_ACP, 0, PWideChar(WideText), WideLen, PChar(Result), AnsiLen, nil, nil);
end;

function ExtractJsonStr(const Json, Key: string): string;
var
  K, I: Integer;
  Escaped: Boolean;
begin
  Result := '';
  K := Pos('"' + Key + '"', Json);
  if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', ':', #9, #10, #13]) do Inc(K);
  if (K > Length(Json)) or (Json[K] <> '"') then Exit;
  Inc(K);
  Escaped := False;
  for I := K to Length(Json) do
  begin
    if Escaped then begin
      case Json[I] of
        'n': Result := Result + #10;
        'r': Result := Result + #13;
        't': Result := Result + #9;
      else Result := Result + Json[I]; end;
      Escaped := False;
    end
    else if Json[I] = '' then Escaped := True
    else if Json[I] = '"' then Exit
    else Result := Result + Json[I];
  end;
end;

function ExtractJsonInt(const Json, Key: string): Integer;
var S: string; K: Integer;
begin
  S := ExtractJsonStr(Json, Key);
  if S <> '' then begin Result := StrToIntDef(S, 0); Exit; end;
  Result := 0;
  K := Pos('"' + Key + '"', Json); if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', ':', #9, #10, #13]) do Inc(K);
  S := '';
  while (K <= Length(Json)) and (Json[K] in ['0'..'9', '-']) do begin S := S + Json[K]; Inc(K); end;
  Result := StrToIntDef(S, 0);
end;

procedure EventCallback(EventJson: PAnsiChar); stdcall;
var Json: string;
begin
  if (EventJson = nil) or (GSelf = nil) then Exit;
  Json := SafeUtf8ToStr(EventJson);
  GSelf.OnEvent(PAnsiChar(AnsiString(Json)));
end;

procedure TFormMain.OnEvent(EventJson: PAnsiChar);
var Json, ResType, Msg, Mrz, IcNum, SavePath: string; EvtType, Status: Integer;
begin
  Json := string(EventJson);
  EvtType := ExtractJsonInt(Json, 'event_type');
  Status := ExtractJsonInt(Json, 'status');
  ResType := ExtractJsonStr(Json, 'resource_type');
  Msg := ExtractJsonStr(Json, 'message');
  Mrz := ExtractJsonStr(Json, 'mrz');
  IcNum := ExtractJsonStr(Json, 'ic_number');
  SavePath := ExtractJsonStr(Json, 'save_path');
  Log(Format('[Event] type=%d status=%d resource=%s', [EvtType, Status, ResType]));
  if Mrz <> '' then Log('  MRZ: ' + Mrz);
  if IcNum <> '' then Log('  IC: ' + IcNum);
  if SavePath <> '' then Log('  Save: ' + SavePath);
  if Msg <> '' then Log('  Msg: ' + Msg);
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
  if FInitialized then HZCYKJTHardWare_ReleaseSdk;
end;

procedure TFormMain.Log(const S: string);
begin
  MemoLog.Lines.Add(FormatDateTime('hh:nn:ss.zzz', Now) + '  ' + S);
end;

procedure TFormMain.LogRet(const Name: string; Ret: Integer);
begin
  Log(Format('%s = %d', [Name, Ret]));
end;


procedure TFormMain.BtnInitClick(Sender: TObject);
var Ret: Integer;
begin
  if FInitialized then begin Log('Already initialized'); Exit; end;
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
  if not FInitialized then Exit;
  LogRet('ReleaseSdk', HZCYKJTHardWare_ReleaseSdk);
  FInitialized := False;
end;

procedure TFormMain.BtnSwitch1Click(Sender: TObject);
begin LogRet('SwitchTerminal(1)', HZCYKJTHardWare_SwitchTerminal(1)); end;

procedure TFormMain.BtnSwitch2Click(Sender: TObject);
begin LogRet('SwitchTerminal(2)', HZCYKJTHardWare_SwitchTerminal(2)); end;

procedure TFormMain.BtnStartProcessClick(Sender: TObject);
begin LogRet('StartProcess', HZCYKJTHardWare_StartProcess(PAnsiChar(AnsiString(EdtSaveDir.Text)))); end;

procedure TFormMain.BtnEndProcessClick(Sender: TObject);
begin LogRet('EndProcess', HZCYKJTHardWare_EndProcess); end;

procedure TFormMain.BtnCameraPreviewClick(Sender: TObject);
begin LogRet('StartCameraPreview', HZCYKJTHardWare_StartCameraPreview(Pointer(PanelCamera.Handle))); end;

procedure TFormMain.BtnStopCamPreviewClick(Sender: TObject);
begin LogRet('StopCameraPreview', HZCYKJTHardWare_StopCameraPreview); end;

procedure TFormMain.BtnFpPreviewClick(Sender: TObject);
begin LogRet('StartFingerprintPreview', HZCYKJTHardWare_StartFingerprintPreview(Pointer(PanelFingerprint.Handle))); end;

procedure TFormMain.BtnStopFpPreviewClick(Sender: TObject);
begin LogRet('StopFingerprintPreview', HZCYKJTHardWare_StopFingerprintPreview); end;

procedure TFormMain.BtnFaceCaptureClick(Sender: TObject);
begin LogRet('CaptureCameraImage', HZCYKJTHardWare_CaptureCameraImage(PAnsiChar(AnsiString(EdtSaveDir.Text)))); end;

procedure TFormMain.BtnFpCaptureClick(Sender: TObject);
begin LogRet('CaptureFingerprintImage', HZCYKJTHardWare_CaptureFingerprintImage(PAnsiChar(AnsiString(EdtSaveDir.Text)))); end;

procedure TFormMain.BtnOCRClick(Sender: TObject);
begin LogRet('RequestOCR', HZCYKJTHardWare_RequestOCR(PAnsiChar(AnsiString(EdtSaveDir.Text)))); end;

procedure TFormMain.BtnNFCClick(Sender: TObject);
begin LogRet('RequestNfcCard', HZCYKJTHardWare_RequestNfcCard(PAnsiChar(AnsiString(EdtSaveDir.Text)))); end;

procedure TFormMain.BtnIrisCaptureClick(Sender: TObject);
begin LogRet('CaptureIrisImage', HZCYKJTHardWare_CaptureIrisImage(PAnsiChar(AnsiString(EdtSaveDir.Text)))); end;

end.
