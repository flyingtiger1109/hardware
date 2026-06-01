unit Logger;

interface

uses Windows, SysUtils, Classes;

type
  TFileLogger = class
  private
    FLogPath: string;
    FLogFile: string;
    FLogFileFull: string;
    FLock: TRTLCriticalSection;
    procedure EnsureDir;
    procedure InitLogFile;
  public
    constructor Create;
    destructor Destroy; override;
    procedure WriteLog(const Msg: string);
    procedure WriteLogFmt(const Fmt: string; const Args: array of const);
  end;

var
  GLogger: TFileLogger;

implementation

constructor TFileLogger.Create;
var
  ExeDir: string;
begin
  inherited Create;
  InitializeCriticalSection(FLock);
  ExeDir := ExtractFilePath(ParamStr(0));
  FLogPath := ExeDir + 'HZCYKJTHardWareExe_Logs';
  EnsureDir;
  InitLogFile;
end;

procedure TFileLogger.EnsureDir;
begin
  if not DirectoryExists(FLogPath) then
    ForceDirectories(FLogPath);
end;

procedure TFileLogger.InitLogFile;
var
  Y, M, D: Word;
begin
  DecodeDate(Date, Y, M, D);
  FLogFile := Format('HZCYKJTHardWareExe_Logs_%.4d%.2d%.2d.log', [Y, M, D]);
  FLogFileFull := FLogPath + '\' + FLogFile;
end;

procedure TFileLogger.WriteLog(const Msg: string);
var
  FS: TFileStream;
  LineAnsi: string;
begin
  LineAnsi := '[' + FormatDateTime('yyyy-mm-dd hh:nn:ss.zzz', Now) +
    '] ' + Msg + #13#10;
  EnterCriticalSection(FLock);
  try
    InitLogFile;
    if FileExists(FLogFileFull) then
      FS := TFileStream.Create(FLogFileFull, fmOpenReadWrite or fmShareDenyNone)
    else
      FS := TFileStream.Create(FLogFileFull, fmCreate or fmShareDenyNone);
    try
      FS.Seek(0, soFromEnd);
      if LineAnsi <> '' then
        FS.WriteBuffer(LineAnsi[1], Length(LineAnsi));
    finally
      FS.Free;
    end;
  except
    // 日志失败不能影响设备业务流程。
  end;
  LeaveCriticalSection(FLock);
end;

destructor TFileLogger.Destroy;
begin
  DeleteCriticalSection(FLock);
  inherited Destroy;
end;

procedure TFileLogger.WriteLogFmt(const Fmt: string; const Args: array of const);
begin
  WriteLog(Format(Fmt, Args));
end;

initialization
  GLogger := TFileLogger.Create;

finalization
  GLogger.Free;

end.
