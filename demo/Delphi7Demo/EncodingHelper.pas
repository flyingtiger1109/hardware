
unit EncodingHelper;

interface

function Utf8ToAnsi(const S: string): string;
function AnsiToUtf8(const S: string): string;

implementation

uses Windows;

function Utf8ToAnsi(const S: string): string;
var
  WideLen, AnsiLen: Integer;
  WideStr: PWideChar;
begin
  Result := S;
  if S = '' then Exit;
  WideLen := MultiByteToWideChar(CP_UTF8, 0, PChar(S), -1, nil, 0);
  if WideLen <= 0 then Exit;
  GetMem(WideStr, WideLen * SizeOf(WideChar));
  try
    MultiByteToWideChar(CP_UTF8, 0, PChar(S), -1, WideStr, WideLen);
    AnsiLen := WideCharToMultiByte(CP_ACP, 0, WideStr, -1, nil, 0, nil, nil);
    if AnsiLen > 0 then
    begin
      SetLength(Result, AnsiLen - 1);
      WideCharToMultiByte(CP_ACP, 0, WideStr, -1, PChar(Result), AnsiLen, nil, nil);
    end;
  finally
    FreeMem(WideStr);
  end;
end;

function AnsiToUtf8(const S: string): string;
var
  WideLen, Utf8Len: Integer;
  WideStr: PWideChar;
begin
  Result := S;
  if S = '' then Exit;
  WideLen := MultiByteToWideChar(CP_ACP, 0, PChar(S), -1, nil, 0);
  if WideLen <= 0 then Exit;
  GetMem(WideStr, WideLen * SizeOf(WideChar));
  try
    MultiByteToWideChar(CP_ACP, 0, PChar(S), -1, WideStr, WideLen);
    Utf8Len := WideCharToMultiByte(CP_UTF8, 0, WideStr, -1, nil, 0, nil, nil);
    if Utf8Len > 0 then
    begin
      SetLength(Result, Utf8Len - 1);
      WideCharToMultiByte(CP_UTF8, 0, WideStr, -1, PChar(Result), Utf8Len, nil, nil);
    end;
  finally
    FreeMem(WideStr);
  end;
end;

end.
