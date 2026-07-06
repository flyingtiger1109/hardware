unit MainUnit;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ExtCtrls, DelphiProxyServer, TerminalManager, Logger, EncodingHelper;

const
  WM_APPEND_APP_LOG = WM_USER + 101;
  WM_AUTO_START_SERVER = WM_USER + 102;
  WM_MINIMIZE_AFTER_EXTERNAL_PREVIEW = WM_USER + 103;

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
    procedure AppendLogLine(const S: string);
    procedure WMAppendAppLog(var Msg: TMessage); message WM_APPEND_APP_LOG;
    procedure WMAutoStartServer(var Msg: TMessage); message WM_AUTO_START_SERVER;
    procedure WMMinimizeAfterExternalPreview(var Msg: TMessage); message WM_MINIMIZE_AFTER_EXTERNAL_PREVIEW;
    procedure ExternalPreviewReady;
    procedure Log(const S: string);
  public
  end;

var
  FormMain: TFormMain;

implementation

{$R *.dfm}

type
  PLogMessage = ^string;

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

procedure TFormMain.AppendLogLine(const S: string);
begin
  MemoLog.Lines.Add('[' + FormatDateTime('yyyy-mm-dd hh:nn:ss.zzz', Now) +
    '] ' + S);
end;

procedure TFormMain.WMAppendAppLog(var Msg: TMessage);
var
  LogMessage: PLogMessage;
begin
  LogMessage := PLogMessage(Msg.LParam);
  if LogMessage = nil then Exit;
  try
    AppendLogLine(LogMessage^);
  finally
    Dispose(LogMessage);
  end;
end;

procedure TFormMain.WMAutoStartServer(var Msg: TMessage);
begin
  BtnStartServerClick(nil);
end;

procedure TFormMain.ExternalPreviewReady;
begin
  if HandleAllocated then
    PostMessage(Handle, WM_MINIMIZE_AFTER_EXTERNAL_PREVIEW, 0, 0);
end;

procedure TFormMain.WMMinimizeAfterExternalPreview(var Msg: TMessage);
begin
  Log('[��Ϣ] [Ԥ��] �ⲿԤ���ѳɹ��ص��������������Զ���С������������');
  if WindowState <> wsMinimized then
    Application.Minimize;
end;
procedure TFormMain.Log(const S: string);
var
  LogMessage: PLogMessage;
begin
  GLogger.WriteLog(S);
  if GetCurrentThreadID = MainThreadID then
    AppendLogLine(S)
  else
  begin
    New(LogMessage);
    LogMessage^ := S;
    if not PostMessage(Handle, WM_APPEND_APP_LOG, 0, LPARAM(LogMessage)) then
      Dispose(LogMessage);
  end;
end;

procedure TFormMain.FormCreate(Sender: TObject);
begin
  ApplyChineseCaptions;
  MemoLog.Clear;
  FServer := nil;
  FSaveDir := '.\captures';
  Log('[��Ϣ] [����] �����������������Զ���������');
  PostMessage(Handle, WM_AUTO_START_SERVER, 0, 0);
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
    Log('[��ʾ] [����] �����������У������ظ�������');
    Exit;
  end;
  FServer := TDelphiProxyServer.Create(PanelCamera, PanelFingerprint, PanelIris);
  FServer.SetLogProc(Log);
  FServer.SetExternalPreviewReadyProc(ExternalPreviewReady);
  FServer.Start;
  Log('[��Ϣ] [����] ������������ʵ�ʼ��������鿴������־��');
end;

procedure TFormMain.BtnStopServerClick(Sender: TObject);
begin
  if FServer = nil then
  begin
    Log('[��ʾ] [����] ������δ����������ֹͣ��');
    Exit;
  end;
  FServer.Stop;
  FServer.Free;
  FServer := nil;
  Log('[��Ϣ] [����] ������ֹͣ��');
end;

// ============================================================
// BUTTON HANDLERS - direct terminal calls (no HTTP round-trip)
// ============================================================

procedure TFormMain.BtnStartProcessClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [����] ����ʧ�ܣ�������������'); Exit; end;
  FServer.StartProcessDirect(FSaveDir);
end;

procedure TFormMain.BtnEndProcessClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [����] ����ʧ�ܣ�������������'); Exit; end;
  FServer.EndProcessDirect;
end;

procedure TFormMain.BtnSwitchTerminal1Click(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [�ն��л�] ����ʧ�ܣ�������������'); Exit; end;
  FServer.SwitchTerminalDirect(1);
end;

procedure TFormMain.BtnSwitchTerminal2Click(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [�ն��л�] ����ʧ�ܣ�������������'); Exit; end;
  FServer.SwitchTerminalDirect(2);
end;

procedure TFormMain.BtnFaceCaptureClick(Sender: TObject);
var
  SavePath: string;
begin
  if FServer = nil then begin Log('[����] [����ץ��] ����ʧ�ܣ�������������'); Exit; end;
  if FServer.CaptureFaceDirect(FSaveDir, SavePath) then
    Log('[��Ϣ] [����ץ��] ץ�ĳɹ�������·��=' + SavePath)
  else
    Log('[����] [����ץ��] ץ��ʧ�ܡ�');
end;

procedure TFormMain.BtnFingerprintCaptureClick(Sender: TObject);
var
  SavePath: string;
begin
  if FServer = nil then begin Log('[����] [ָ��ץ��] ����ʧ�ܣ�������������'); Exit; end;
  if FServer.CaptureFingerprintDirect(FSaveDir, SavePath) then
    Log('[��Ϣ] [ָ��ץ��] ץ�ĳɹ�������·��=' + SavePath)
  else
    Log('[����] [ָ��ץ��] ץ��ʧ�ܡ�');
end;

procedure TFormMain.BtnOCRClick(Sender: TObject);
var
  ReqId: string;
begin
  if FServer = nil then begin Log('[����] [OCRʶ��] ����ʧ�ܣ�������������'); Exit; end;
  ReqId := FServer.RequestOCRDirect(FSaveDir);
  if ReqId <> '' then
    Log('[��Ϣ] [OCRʶ��] ������������request_id=' + ReqId)
  else
    Log('[����] [OCRʶ��] �����ύʧ�ܡ�');
end;

procedure TFormMain.BtnNfcCardClick(Sender: TObject);
var
  ReqId: string;
begin
  if FServer = nil then begin Log('[����] [IC��ʶ��] ����ʧ�ܣ�������������'); Exit; end;
  ReqId := FServer.RequestNfcDirect(FSaveDir);
  if ReqId <> '' then
    Log('[��Ϣ] [IC��ʶ��] ������������request_id=' + ReqId)
  else
    Log('[����] [IC��ʶ��] �����ύʧ�ܡ�');
end;

procedure TFormMain.BtnIrisCaptureClick(Sender: TObject);
var
  ReqId: string;
begin
  if FServer = nil then begin Log('[����] [��Ĥץ��] ����ʧ�ܣ�������������'); Exit; end;
  ReqId := FServer.CaptureIrisDirect(FSaveDir);
  if ReqId <> '' then
    Log('[��Ϣ] [��Ĥץ��] ������������request_id=' + ReqId)
  else
    Log('[����] [��Ĥץ��] �����ύʧ�ܡ�');
end;

procedure TFormMain.BtnStartCameraPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [����ͷԤ��] ����ʧ�ܣ�������������'); Exit; end;
  if FServer.StartCameraPreviewDirect then
    Log('[��Ϣ] [����ͷԤ��] Ԥ����������')
  else
    Log('[����] [����ͷԤ��] Ԥ������ʧ�ܡ�');
end;

procedure TFormMain.BtnStopCameraPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [����ͷԤ��] ����ʧ�ܣ�������������'); Exit; end;
  FServer.StopCameraPreviewDirect;
  Log('[��Ϣ] [����ͷԤ��] Ԥ����ֹͣ��');
end;

procedure TFormMain.BtnStartFingerprintPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [ָ��Ԥ��] ����ʧ�ܣ�������������'); Exit; end;
  if FServer.StartFingerprintPreviewDirect then
    Log('[��Ϣ] [ָ��Ԥ��] Ԥ����������')
  else
    Log('[����] [ָ��Ԥ��] Ԥ������ʧ�ܡ�');
end;

procedure TFormMain.BtnStopFingerprintPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [ָ��Ԥ��] ����ʧ�ܣ�������������'); Exit; end;
  FServer.StopFingerprintPreviewDirect;
  Log('[��Ϣ] [ָ��Ԥ��] Ԥ����ֹͣ��');
end;

procedure TFormMain.BtnStartIrisPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [��ĤԤ��] ����ʧ�ܣ�������������'); Exit; end;
  if FServer.StartIrisPreviewDirect then
    Log('[��Ϣ] [��ĤԤ��] Ԥ����������')
  else
    Log('[����] [��ĤԤ��] Ԥ������ʧ�ܡ�');
end;

procedure TFormMain.BtnStopIrisPreviewClick(Sender: TObject);
begin
  if FServer = nil then begin Log('[����] [��ĤԤ��] ����ʧ�ܣ�������������'); Exit; end;
  FServer.StopIrisPreviewDirect;
  Log('[��Ϣ] [��ĤԤ��] Ԥ����ֹͣ��');
end;

procedure TFormMain.BtnStartPlatePreviewClick(Sender: TObject);
begin
  Log('[��ʾ] [����Ԥ��] ��ǰ�汾�ݲ�֧�ֳ���Ԥ����');
end;

procedure TFormMain.BtnStopPlatePreviewClick(Sender: TObject);
begin
  Log('[��ʾ] [����Ԥ��] ��ǰ�汾�ݲ�֧�ֳ���Ԥ��ֹͣ������');
end;

end.
