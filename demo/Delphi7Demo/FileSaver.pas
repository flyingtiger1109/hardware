
unit FileSaver;

interface

uses SysUtils, Classes;

type
  TFileSaver = class
  public
    function SaveBase64Image(const Base64Str, MimeType, SaveDir, RequestId, Prefix: string): string;
    function SaveBase64ImageToFile(const Base64Str, FilePath: string): string;
    function SaveJsonFile(const JsonStr, SaveDir, RequestId, FileName: string): string;
    function CreateDateFolder(const BaseDir: string): string;
    function CreateRequestFolder(const BaseDir, RequestId: string): string;
    function EnsureDir(const Dir: string): string;
  private
    function Base64Decode(const S: string; out Buf: Pointer; out BufSize: Integer): Boolean;
    function GetExtensionFromMime(const MimeType: string): string;
  end;

implementation

function TFileSaver.EnsureDir(const Dir: string): string;
begin
  Result := Dir;
  if Result = '' then
    Result := ExtractFilePath(ParamStr(0)) + 'captures';
  if not DirectoryExists(Result) then
    ForceDirectories(Result);
end;

function TFileSaver.CreateDateFolder(const BaseDir: string): string;
var
  Y, M, D: Word;
begin
  DecodeDate(Date, Y, M, D);
  Result := EnsureDir(BaseDir) + '\' +
    Format('%.4d', [Y]) + Format('%.2d', [M]) + Format('%.2d', [D]);
  if not DirectoryExists(Result) then
    ForceDirectories(Result);
end;

function TFileSaver.CreateRequestFolder(const BaseDir, RequestId: string): string;
begin
  Result := BaseDir + '\' + RequestId;
  if not DirectoryExists(Result) then
    ForceDirectories(Result);
end;

function TFileSaver.GetExtensionFromMime(const MimeType: string): string;
begin
  if Pos('jpeg', LowerCase(MimeType)) > 0 then
    Result := '.jpg'
  else if Pos('png', LowerCase(MimeType)) > 0 then
    Result := '.png'
  else if Pos('bmp', LowerCase(MimeType)) > 0 then
    Result := '.bmp'
  else
    Result := '.dat';
end;

const
  B64: array[0..63] of Char = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';

function TFileSaver.Base64Decode(const S: string; out Buf: Pointer; out BufSize: Integer): Boolean;
var
  I, J, Pad, Val, N: Integer;
  OutBuf: PByte;
begin
  Result := False;
  Buf := nil;
  BufSize := 0;
  if S = '' then Exit;

  N := Length(S);
  Pad := 0;
  if (N > 0) and (S[N] = '=') then begin Inc(Pad); Dec(N); end;
  if (N > 0) and (S[N] = '=') then begin Inc(Pad); Dec(N); end;

  BufSize := (N div 4) * 3 - Pad;
  if BufSize <= 0 then Exit;

  GetMem(Buf, BufSize);
  OutBuf := PByte(Buf);
  J := 0;
  Val := 0;
  for I := 1 to N do
  begin
    case S[I] of
      'A'..'Z': Val := Val shl 6 or (Ord(S[I]) - 65);
      'a'..'z': Val := Val shl 6 or (Ord(S[I]) - 71);
      '0'..'9': Val := Val shl 6 or (Ord(S[I]) + 4);
      '+': Val := Val shl 6 or 62;
      '/': Val := Val shl 6 or 63;
      else Continue;
    end;
    Inc(J);
    if J = 4 then
    begin
      if (Integer(OutBuf) - Integer(Buf) + 2) < BufSize then
      begin
        OutBuf^ := Byte((Val shr 16) and $FF); Inc(OutBuf);
        OutBuf^ := Byte((Val shr 8) and $FF); Inc(OutBuf);
        OutBuf^ := Byte(Val and $FF); Inc(OutBuf);
      end;
      J := 0;
      Val := 0;
    end;
  end;
  Result := True;
end;

function TFileSaver.SaveBase64Image(const Base64Str, MimeType, SaveDir, RequestId, Prefix: string): string;
var
  Dir, Ext, FilePath: string;
  Buf: Pointer;
  BufSize: Integer;
  FS: TFileStream;
begin
  Result := '';
  if Base64Str = '' then Exit;

  Dir := EnsureDir(SaveDir);
  Ext := GetExtensionFromMime(MimeType);
  FilePath := IncludeTrailingBackslash(Dir) + Prefix + '_' + RequestId + Ext;

  if not Base64Decode(Base64Str, Buf, BufSize) then Exit;
  try
    FS := TFileStream.Create(FilePath, fmCreate);
    try
      FS.Write(Buf^, BufSize);
      Result := FilePath;
    finally
      FS.Free;
    end;
  finally
    FreeMem(Buf);
  end;
end;

function TFileSaver.SaveBase64ImageToFile(const Base64Str, FilePath: string): string;
var
  Buf: Pointer;
  BufSize: Integer;
  FS: TFileStream;
begin
  Result := '';
  if (Base64Str = '') or (FilePath = '') then Exit;
  if not Base64Decode(Base64Str, Buf, BufSize) then Exit;
  try
    FS := TFileStream.Create(FilePath, fmCreate);
    try
      FS.Write(Buf^, BufSize);
      Result := FilePath;
    finally
      FS.Free;
    end;
  finally
    FreeMem(Buf);
  end;
end;

function TFileSaver.SaveJsonFile(const JsonStr, SaveDir, RequestId, FileName: string): string;
var
  Dir, FilePath: string;
  SL: TStringList;
begin
  Result := '';
  if JsonStr = '' then Exit;
  Dir := EnsureDir(SaveDir);
  FilePath := IncludeTrailingBackslash(Dir) + FileName + '_' + RequestId + '.json';
  SL := TStringList.Create;
  try
    SL.Text := JsonStr;
    SL.SaveToFile(FilePath);
    Result := FilePath;
  finally
    SL.Free;
  end;
end.

end.
