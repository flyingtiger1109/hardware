unit Logger;

interface

uses SysUtils;

type
  TFileLogger = class
  private
    FLogPath: string;
    FLogFile: string;
    FLogFileFull: string;
    procedure EnsureDir;
    procedure InitLogFile;
  public
    constructor Create;
    procedure WriteLog(const Msg: string);
    procedure WriteLogFmt(const Fmt: string; const Args: array of const);
  end;

var
  GLogger: TFileLogger;

implementation

uses Windows;

constructor TFileLogger.Create;
var
  ExeDir: string;
begin
  inherited Create;
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
  FLogFile := Format('HZCYKJTHardWare_%.4d%.2d%.2d.log', [Y, M, D]);
  FLogFileFull := FLogPath + '\' + FLogFile;
end;

procedure TFileLogger.WriteLog(const Msg: string);
var
  F: TextFile;
  Timestamp: string;
begin
  Timestamp := FormatDateTime('yyyy-mm-dd hh:nn:ss.zzz', Now);
  InitLogFile; // re-init in case date changed
  try
    AssignFile(F, FLogFileFull);
    if FileExists(FLogFileFull) then
      Append(F)
    else
      Rewrite(F);
    WriteLn(F, Timestamp + '  ' + Msg);
    CloseFile(F);
  except
    // silent fail
  end;
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
