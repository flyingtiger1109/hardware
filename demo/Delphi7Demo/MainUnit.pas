unit MainUnit;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ExtCtrls, DelphiProxyServer, TerminalManager, Logger, EncodingHelper;

type
  TFormMain = class(TForm)
    PanelTop: TPanel;
    BtnStartServer: TButton;
    BtnStopServer: TButton;
    BtnStartProcess: TButton;
    BtnEndProcess: TButton;
    BtnSwitchTerminal1: TButton;
    BtnSwitchTerminal2: TButton;
    BtnFaceCapture: TButton;
    BtnFingerprintCapture: TButton;
    BtnOCR: TButton;
    BtnNfcCard: TButton;
    BtnIrisCapture: TButton;
    BtnStartCameraPreview: TButton;
    BtnStopCameraPreview: TButton;
    BtnStartFingerprintPreview: TButton;
    BtnStopFingerprintPreview: TButton;
    BtnStartIrisPreview: TButton;
    BtnStopIrisPreview: TButton;
    BtnStartPlatePreview: TButton;
    BtnStopPlatePreview: TButton;
    MemoLog: TMemo;
    PanelPreview: TPanel;
    PanelCamera: TPanel;
    Splitter1: TSplitter;
    PanelFingerprint: TPanel;
    Splitter2: TSplitter;
    PanelIris: TPanel;
    procedure FormCreate(Sender: TObject);
    procedure FormDestroy(Sender: TObject);
    procedure BtnStartServerClick(Sender: TObject);
    procedure BtnStopServerClick(Sender: TObject);
    procedure BtnStartProcessClick(Sender: TObject);
    procedure BtnEndProcessClick(Sender: TObject);
    procedure BtnSwitchTerminal1Click(Sender: TObject);
    procedure BtnSwitchTerminal2Click(Sender: TObject);
    procedure BtnFaceCaptureClick(Sender: TObject);
    procedure BtnFingerprintCaptureClick(Sender: TObject);
    procedure BtnOCRClick(Sender: TObject);
    procedure BtnNfcCardClick(Sender: TObject);
    procedure BtnIrisCaptureClick(Sender: TObject);
    procedure BtnStartCameraPreviewClick(Sender: TObject);
    procedure BtnStopCameraPreviewClick(Sender: TObject);
    procedure BtnStartFingerprintPreviewClick(Sender: TObject);
    procedure BtnStopFingerprintPreviewClick(Sender: TObject);
    procedure BtnStartIrisPreviewClick(Sender: TObject);
    procedure BtnStopIrisPreviewClick(Sender: TObject);
    procedure BtnStartPlatePreviewClick(Sender: TObject);
    procedure BtnStopPlatePreviewClick(Sender: TObject);
  private
    FServer: TDelphiProxyServer;
    FSaveDir: string;
    procedure ApplyChineseCaptions;
    procedure Log(const S: string);
  public
  end;

var
  FormMain: TFormMain;

implementation

{$R *.dfm}

function Cn(const Utf8Bytes: string): string;
begin
  Result := Utf8ToAnsi(Utf8Bytes);
end;

procedure TFormMain.ApplyChineseCaptions;
begin
  Caption := Cn(#$48#$5A#$43#$59#$4B#$4A#$54#$48#$61#$72#$64#$57#$61#$72#$65#$20#$2D#$20#$E5#$90#$8E#$E7#$AB#$AF#$E6#$9C#$8D#$E5#$8A#$A1);
  Application.Title := Caption;

  BtnStartServer.Caption := Cn(#$E5#$90#$AF#$E5#$8A#$A8#$E6#$9C#$8D#$E5#$8A#$A1);
  BtnStopServer.Caption := Cn(#$E5#$81#$9C#$E6#$AD#$A2#$E6#$9C#$8D#$E5#$8A#$A1);
  BtnStartProcess.Caption := Cn(#$E5#$BC#$80#$E5#$A7#$8B#$E6#$B5#$81#$E7#$A8#$8B);
  BtnEndProcess.Caption := Cn(#$E7#$BB#$93#$E6#$9D#$9F#$E6#$B5#$81#$E7#$A8#$8B);
  BtnSwitchTerminal1.Caption := Cn(#$E7#$BB#$88#$E7#$AB#$AF#$31);
  BtnSwitchTerminal2.Caption := Cn(#$E7#$BB#$88#$E7#$AB#$AF#$32);
  BtnFaceCapture.Caption := Cn(#$E4#$BA#$BA#$E8#$84#$B8#$E6#$8A#$93#$E6#$8B#$8D);
  BtnFingerprintCapture.Caption := Cn(#$E6#$8C#$87#$E7#$BA#$B9#$E6#$8A#$93#$E6#$8B#$8D);
  BtnOCR.Caption := Cn(#$4F#$43#$52#$20#$E9#$98#$85#$E8#$AF#$BB);
  BtnNfcCard.Caption := Cn(#$49#$43#$20#$E5#$8D#$A1#$E8#$AF#$86#$E5#$88#$AB);
  BtnIrisCapture.Caption := Cn(#$E8#$99#$B9#$E8#$86#$9C#$E6#$8A#$93#$E6#$8B#$8D);
  BtnStartCameraPreview.Caption := Cn(#$E5#$BC#$80#$E5#$A7#$8B#$E6#$91#$84#$E5#$83#$8F#$E5#$A4#$B4#$E9#$A2#$84#$E8#$A7#$88);
  BtnStopCameraPreview.Caption := Cn(#$E5#$81#$9C#$E6#$AD#$A2#$E6#$91#$84#$E5#$83#$8F#$E5#$A4#$B4#$E9#$A2#$84#$E8#$A7#$88);
  BtnStartFingerprintPreview.Caption := Cn(#$E5#$BC#$80#$E5#$A7#$8B#$E6#$8C#$87#$E7#$BA#$B9#$E9#$A2#$84#$E8#$A7#$88);
  BtnStopFingerprintPreview.Caption := Cn(#$E5#$81#$9C#$E6#$AD#$A2#$E6#$8C#$87#$E7#$BA#$B9#$E9#$A2#$84#$E8#$A7#$88);
  BtnStartIrisPreview.Caption := Cn(#$E5#$BC#$80#$E5#$A7#$8B#$E8#$99#$B9#$E8#$86#$9C#$E9#$A2#$84#$E8#$A7#$88);
  BtnStopIrisPreview.Caption := Cn(#$E5#$81#$9C#$E6#$AD#$A2#$E8#$99#$B9#$E8#$86#$9C#$E9#$A2#$84#$E8#$A7#$88);
  BtnStartPlatePreview.Caption := Cn(#$E5#$BC#$80#$E5#$A7#$8B#$E8#$BD#$A6#$E7#$89#$8C#$E9#$A2#$84#$E8#$A7#$88);
  BtnStopPlatePreview.Caption := Cn(#$E5#$81#$9C#$E6#$AD#$A2#$E8#$BD#$A6#$E7#$89#$8C#$E9#$A2#$84#$E8#$A7#$88);

  PanelCamera.Caption := Cn(#$E6#$91#$84#$E5#$83#$8F#$E5#$A4#$B4#$E9#$A2#$84#$E8#$A7#$88);
  PanelFingerprint.Caption := Cn(#$E6#$8C#$87#$E7#$BA#$B9#$E9#$A2#$84#$E8#$A7#$88);
  PanelIris.Caption := Cn(#$E8#$99#$B9#$E8#$86#$9C#$E9#$A2#$84#$E8#$A7#$88);
end;

procedure TFormMain.Log(const S: string);
begin
  MemoLog.Lines.Add(FormatDateTime('hh:nn:ss.zzz', Now) + '  ' + S);
  GLogger.WriteLog(S);
end;

procedure TFormMain.FormCreate(Sender: TObject);
begin
  ApplyChineseCaptions;
  MemoLog.Clear;
  FServer := nil;
  FSaveDir := '.\captures';
  Log('Program started. Click [Start Server] to begin.');
end;

procedure TFormMain.FormDestroy(Sender: TObject);
begin
  if FServer <> nil then
  begin
    FServer.Stop;
    FServer.Free;
    FServer := nil;
  end;
end;

procedure TFormMain.BtnStartServerClick(Sender: TObject);
begin
  if FServer <> nil then
  begin
    Log('Server already running');
    Exit;
  end;
  FServer := TDelphiProxyServer.Create(PanelCamera, PanelFingerprint, PanelIris);
  FServer.SetLogProc(Log);
  FServer.Start;
  Log('Server started: http://127.0.0.1:8080');
end;

procedure TFormMain.BtnStopServerClick(Sender: TObject);
begin
  if FServer = nil then
  begin
    Log('Server not running');
    Exit;
  end;
  FServer.Stop;
  FServer.Free;
  FServer := nil;
  Log('Server stopped');
end;

// ============================================================
// BUTTON HANDLERS - direct terminal calls (no HTTP round-trip)
// ============================================================

procedure TFormMain.BtnStartProcessClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.StartProcessDirect(FSaveDir);
end;

procedure TFormMain.BtnEndProcessClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.EndProcessDirect;
end;

procedure TFormMain.BtnSwitchTerminal1Click(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.SwitchTerminalDirect(1);
end;

procedure TFormMain.BtnSwitchTerminal2Click(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.SwitchTerminalDirect(2);
end;

procedure TFormMain.BtnFaceCaptureClick(Sender: TObject);
var
  SavePath: string;
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  if FServer.CaptureFaceDirect(FSaveDir, SavePath) then
    Log('[Capture] Face OK: ' + SavePath)
  else
    Log('[Capture] Face FAILED');
end;

procedure TFormMain.BtnFingerprintCaptureClick(Sender: TObject);
var
  SavePath: string;
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  if FServer.CaptureFingerprintDirect(FSaveDir, SavePath) then
    Log('[Capture] Fingerprint OK: ' + SavePath)
  else
    Log('[Capture] Fingerprint FAILED');
end;

procedure TFormMain.BtnOCRClick(Sender: TObject);
var
  ReqId: string;
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  ReqId := FServer.RequestOCRDirect(FSaveDir);
  if ReqId <> '' then
    Log('[Async] OCR accepted: ' + ReqId)
  else
    Log('[Async] OCR FAILED');
end;

procedure TFormMain.BtnNfcCardClick(Sender: TObject);
var
  ReqId: string;
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  ReqId := FServer.RequestNfcDirect(FSaveDir);
  if ReqId <> '' then
    Log('[Async] NFC accepted: ' + ReqId)
  else
    Log('[Async] NFC FAILED');
end;

procedure TFormMain.BtnIrisCaptureClick(Sender: TObject);
var
  ReqId: string;
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  ReqId := FServer.CaptureIrisDirect(FSaveDir);
  if ReqId <> '' then
    Log('[Async] Iris accepted: ' + ReqId)
  else
    Log('[Async] Iris FAILED');
end;

procedure TFormMain.BtnStartCameraPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  if FServer.StartCameraPreviewDirect then
    Log('[Preview] Camera preview started')
  else
    Log('[Preview] Camera preview FAILED');
end;

procedure TFormMain.BtnStopCameraPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.StopCameraPreviewDirect;
  Log('[Preview] Camera preview stopped');
end;

procedure TFormMain.BtnStartFingerprintPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  if FServer.StartFingerprintPreviewDirect then
    Log('[Preview] Fingerprint preview started')
  else
    Log('[Preview] Fingerprint preview FAILED');
end;

procedure TFormMain.BtnStopFingerprintPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.StopFingerprintPreviewDirect;
  Log('[Preview] Fingerprint preview stopped');
end;

procedure TFormMain.BtnStartIrisPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  if FServer.StartIrisPreviewDirect then
    Log('[Preview] Iris preview started')
  else
    Log('[Preview] Iris preview FAILED');
end;

procedure TFormMain.BtnStopIrisPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('ERROR: Start server first'); Exit; end;
  FServer.StopIrisPreviewDirect;
  Log('[Preview] Iris preview stopped');
end;

procedure TFormMain.BtnStartPlatePreviewClick(Sender: TObject);
begin
  Log('[Preview] Plate preview not supported');
end;

procedure TFormMain.BtnStopPlatePreviewClick(Sender: TObject);
begin
  Log('[Preview] Stop plate preview not supported');
end;

end.
