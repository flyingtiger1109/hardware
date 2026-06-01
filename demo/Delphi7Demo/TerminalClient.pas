unit TerminalClient;

interface

uses SysUtils;

type
  TTerminalClient = class
  private
    FLastStage: string;
    FLastErrorCode: Cardinal;
    FLastHttpStatus: Cardinal;
    procedure ResetLastResult;
  public
    function PostJson(const BaseUrl, Path, BodyUtf8: string; out ResponseUtf8: string): Boolean;
    function GetJson(const BaseUrl, Path: string; out ResponseUtf8: string): Boolean;
    function DescribeLastResult: string;
    property LastStage: string read FLastStage;
    property LastErrorCode: Cardinal read FLastErrorCode;
    property LastHttpStatus: Cardinal read FLastHttpStatus;
  end;

function SummarizeTerminalResponse(const ResponseUtf8: string): string;

implementation

uses Windows, WinInet, Logger, EncodingHelper;

function Cn(const Utf8Bytes: string): string;
begin
  Result := Utf8ToAnsi(Utf8Bytes);
end;

function ParseUrl(const Url: string; var Host: string; var Port: Integer; var PathOnly: string): Boolean;
var
  U, HostPort: string;
  P: Integer;
begin
  Result := False;
  U := Url;
  if Copy(U, 1, 7) = 'http://' then Delete(U, 1, 7);
  P := Pos('/', U);
  if P = 0 then Exit;
  HostPort := Copy(U, 1, P - 1);
  PathOnly := Copy(U, P, MaxInt);
  P := Pos(':', HostPort);
  if P > 0 then
  begin
    Host := Copy(HostPort, 1, P - 1);
    Port := StrToIntDef(Copy(HostPort, P + 1, MaxInt), 80);
  end
  else
  begin
    Host := HostPort;
    Port := 80;
  end;
  Result := Host <> '';
end;

function InlineText(const Value: string): string;
begin
  Result := StringReplace(Value, #13, ' ', [rfReplaceAll]);
  Result := StringReplace(Result, #10, ' ', [rfReplaceAll]);
end;

function SummarizeTerminalResponse(const ResponseUtf8: string): string;
const
  MaxSummaryLength = 256;
begin
  Result := InlineText(ResponseUtf8);
  if Result = '' then
    Result := '<' + Cn(#$E7#$A9#$BA#$E5#$93#$8D#$E5#$BA#$94) + '>';
  if Length(Result) > MaxSummaryLength then
    Result := Copy(Result, 1, MaxSummaryLength) + '...';
end;

function ResponseSignalsFailure(const ResponseUtf8: string): Boolean;
var
  CompactValue: string;
begin
  CompactValue := LowerCase(ResponseUtf8);
  CompactValue := StringReplace(CompactValue, ' ', '', [rfReplaceAll]);
  CompactValue := StringReplace(CompactValue, #9, '', [rfReplaceAll]);
  CompactValue := StringReplace(CompactValue, #13, '', [rfReplaceAll]);
  CompactValue := StringReplace(CompactValue, #10, '', [rfReplaceAll]);
  Result := (CompactValue = '') or
    (Pos('"error":true', CompactValue) > 0) or
    (Pos('"success":false', CompactValue) > 0) or
    (Pos('"status":"error"', CompactValue) > 0) or
    (Pos('"status":"failed"', CompactValue) > 0) or
    (Pos('"status":"fail"', CompactValue) > 0);
end;

function HttpRequest(Method, Url, BodyUtf8: string; out ResponseUtf8: string;
  out ErrorStage: string; out ErrorCode, HttpStatus: Cardinal): Boolean;
var
  hInet, hConn, hReq: HINTERNET;
  Host, Path, Headers, Chunk: string;
  Port: Integer;
  Buf: array[0..4095] of Byte;
  BytesRead: Cardinal;
  Flags: DWORD;
  Timeout: DWORD;
  StatusLength, StatusIndex: DWORD;
begin
  Result := False;
  ResponseUtf8 := '';
  ErrorStage := 'parse_url';
  ErrorCode := 0;
  HttpStatus := 0;
  if not ParseUrl(Url, Host, Port, Path) then Exit;

  ErrorStage := 'internet_open';
  hInet := InternetOpen('DelphiTerminalClient', INTERNET_OPEN_TYPE_DIRECT, nil, nil, 0);
  if not Assigned(hInet) then
  begin
    ErrorCode := GetLastError;
    Exit;
  end;
  try
    // Set timeouts: 5s connect, 10s send, 10s receive
    Timeout := 5000;
    InternetSetOption(hInet, INTERNET_OPTION_CONNECT_TIMEOUT, @Timeout, SizeOf(Timeout));
    Timeout := 10000;
    InternetSetOption(hInet, INTERNET_OPTION_SEND_TIMEOUT, @Timeout, SizeOf(Timeout));
    InternetSetOption(hInet, INTERNET_OPTION_RECEIVE_TIMEOUT, @Timeout, SizeOf(Timeout));

    ErrorStage := 'create_connection_handle';
    hConn := InternetConnect(hInet, PChar(Host), Port, nil, nil, INTERNET_SERVICE_HTTP, 0, 0);
    if not Assigned(hConn) then
    begin
      ErrorCode := GetLastError;
      Exit;
    end;
    try
      Timeout := 5000;
      InternetSetOption(hConn, INTERNET_OPTION_CONNECT_TIMEOUT, @Timeout, SizeOf(Timeout));
      Timeout := 10000;
      InternetSetOption(hConn, INTERNET_OPTION_SEND_TIMEOUT, @Timeout, SizeOf(Timeout));
      InternetSetOption(hConn, INTERNET_OPTION_RECEIVE_TIMEOUT, @Timeout, SizeOf(Timeout));

      Flags := INTERNET_FLAG_RELOAD or INTERNET_FLAG_NO_CACHE_WRITE;
      ErrorStage := 'open_request';
      hReq := HttpOpenRequest(hConn, PChar(Method), PChar(Path), nil, nil, nil, Flags, 0);
      if not Assigned(hReq) then
      begin
        ErrorCode := GetLastError;
        Exit;
      end;
      try
        Timeout := 5000;
        InternetSetOption(hReq, INTERNET_OPTION_CONNECT_TIMEOUT, @Timeout, SizeOf(Timeout));
        Timeout := 10000;
        InternetSetOption(hReq, INTERNET_OPTION_SEND_TIMEOUT, @Timeout, SizeOf(Timeout));
        InternetSetOption(hReq, INTERNET_OPTION_RECEIVE_TIMEOUT, @Timeout, SizeOf(Timeout));

        Headers := 'Content-Type: application/json; charset=utf-8'#13#10;
      ErrorStage := 'send_request_wait_response_headers';
        if not HttpSendRequest(hReq, PChar(Headers), Length(Headers),
                               PChar(BodyUtf8), Length(BodyUtf8)) then
        begin
          ErrorCode := GetLastError;
          Exit;
        end;

        StatusLength := SizeOf(HttpStatus);
        StatusIndex := 0;
        HttpQueryInfo(hReq, HTTP_QUERY_STATUS_CODE or HTTP_QUERY_FLAG_NUMBER,
          @HttpStatus, StatusLength, StatusIndex);

        ErrorStage := 'read_response';
        repeat
          if not InternetReadFile(hReq, @Buf, SizeOf(Buf), BytesRead) then
          begin
            ErrorCode := GetLastError;
            Exit;
          end;
          if BytesRead > 0 then
          begin
            SetString(Chunk, PAnsiChar(@Buf[0]), BytesRead);
            ResponseUtf8 := ResponseUtf8 + Chunk;
          end;
        until BytesRead = 0;

        ErrorStage := 'completed';
        Result := True;
      finally
        InternetCloseHandle(hReq);
      end;
    finally
      InternetCloseHandle(hConn);
    end;
  finally
    InternetCloseHandle(hInet);
  end;
end;

procedure TTerminalClient.ResetLastResult;
begin
  FLastStage := '';
  FLastErrorCode := 0;
  FLastHttpStatus := 0;
end;

function TTerminalClient.DescribeLastResult: string;
var
  ErrorText, StageText: string;
begin
  StageText := FLastStage;
  if FLastStage = 'parse_url' then
    StageText := Cn(#$E8#$A7#$A3#$E6#$9E#$90#$E8#$AF#$B7#$E6#$B1#$82#$E5#$9C#$B0#$E5#$9D#$80)
  else if FLastStage = 'internet_open' then
    StageText := Cn(#$E5#$88#$9D#$E5#$A7#$8B#$E5#$8C#$96#$E7#$BD#$91#$E7#$BB#$9C#$E7#$BB#$84#$E4#$BB#$B6)
  else if FLastStage = 'create_connection_handle' then
    StageText := Cn(#$E5#$88#$9B#$E5#$BB#$BA#$E8#$BF#$9E#$E6#$8E#$A5#$E5#$8F#$A5#$E6#$9F#$84)
  else if FLastStage = 'open_request' then
    StageText := Cn(#$E5#$88#$9B#$E5#$BB#$BA#$48#$54#$54#$50#$E8#$AF#$B7#$E6#$B1#$82)
  else if FLastStage = 'send_request_wait_response_headers' then
    StageText := Cn(#$E5#$8F#$91#$E9#$80#$81#$E8#$AF#$B7#$E6#$B1#$82#$E6#$88#$96#$E7#$AD#$89#$E5#$BE#$85#$E5#$93#$8D#$E5#$BA#$94#$E5#$A4#$B4)
  else if FLastStage = 'read_response' then
    StageText := Cn(#$E8#$AF#$BB#$E5#$8F#$96#$E5#$93#$8D#$E5#$BA#$94#$E5#$86#$85#$E5#$AE#$B9)
  else if FLastStage = 'completed' then
    StageText := Cn(#$E5#$B7#$B2#$E5#$AE#$8C#$E6#$88#$90#$E5#$93#$8D#$E5#$BA#$94#$E8#$AF#$BB#$E5#$8F#$96);
  Result := Cn(#$E9#$98#$B6#$E6#$AE#$B5) + '=' + StageText + Cn(#$EF#$BC#$8C) + ' ' +
    Cn(#$E9#$94#$99#$E8#$AF#$AF#$E7#$A0#$81) + '=' + IntToStr(FLastErrorCode);
  if FLastErrorCode <> 0 then
  begin
    ErrorText := InlineText(SysErrorMessage(FLastErrorCode));
    if ErrorText <> '' then
      Result := Result + Cn(#$EF#$BC#$8C) + ' ' +
        Cn(#$E9#$94#$99#$E8#$AF#$AF#$E4#$BF#$A1#$E6#$81#$AF) + '=' + ErrorText;
  end;
  if FLastHttpStatus <> 0 then
    Result := Result + Cn(#$EF#$BC#$8C) + ' ' +
      Cn(#$48#$54#$54#$50#$E7#$8A#$B6#$E6#$80#$81#$E7#$A0#$81) + '=' + IntToStr(FLastHttpStatus);
end;

procedure LogHttpResult(const Method, FullUrl, ResponseUtf8: string;
  Client: TTerminalClient; Succeeded: Boolean);
var
  Detail: string;
begin
  Detail := Cn(#$E6#$96#$B9#$E6#$B3#$95) + '=' + Method + Cn(#$EF#$BC#$8C) + ' ' +
    Cn(#$E5#$9C#$B0#$E5#$9D#$80) + '=' + FullUrl + Cn(#$EF#$BC#$8C) + ' ' +
    Client.DescribeLastResult;
  if not Succeeded then
  begin
    GLogger.WriteLog('[' + Cn(#$E9#$94#$99#$E8#$AF#$AF) + '] [' +
      Cn(#$E7#$BB#$88#$E7#$AB#$AF#$48#$54#$54#$50) + '] ' +
      Cn(#$E8#$AF#$B7#$E6#$B1#$82#$E5#$A4#$B1#$E8#$B4#$A5#$EF#$BC#$9A) + Detail);
    Exit;
  end;
  if (Client.LastHttpStatus < 200) or (Client.LastHttpStatus >= 300) then
  begin
    GLogger.WriteLog('[' + Cn(#$E9#$94#$99#$E8#$AF#$AF) + '] [' +
      Cn(#$E7#$BB#$88#$E7#$AB#$AF#$48#$54#$54#$50) + '] ' +
      Cn(#$48#$54#$54#$50#$E7#$8A#$B6#$E6#$80#$81#$E5#$BC#$82#$E5#$B8#$B8#$EF#$BC#$9A) +
      Detail + Cn(#$EF#$BC#$8C) + ' ' +
      Cn(#$E5#$93#$8D#$E5#$BA#$94#$E6#$91#$98#$E8#$A6#$81) + '=' +
      SummarizeTerminalResponse(ResponseUtf8));
    Exit;
  end;
  if ResponseSignalsFailure(ResponseUtf8) then
  begin
    GLogger.WriteLog('[' + Cn(#$E9#$94#$99#$E8#$AF#$AF) + '] [' +
      Cn(#$E7#$BB#$88#$E7#$AB#$AF#$48#$54#$54#$50) + '] ' +
      Cn(#$E7#$BB#$88#$E7#$AB#$AF#$E8#$BF#$94#$E5#$9B#$9E#$E5#$A4#$B1#$E8#$B4#$A5#$E5#$93#$8D#$E5#$BA#$94#$EF#$BC#$9A) +
      Detail + Cn(#$EF#$BC#$8C) + ' ' +
      Cn(#$E5#$93#$8D#$E5#$BA#$94#$E6#$91#$98#$E8#$A6#$81) + '=' +
      SummarizeTerminalResponse(ResponseUtf8));
    Exit;
  end;
  // Small acknowledgements and preview responses are useful; image payloads are not logged.
  if Length(ResponseUtf8) <= 4096 then
    GLogger.WriteLog('[' + Cn(#$E4#$BF#$A1#$E6#$81#$AF) + '] [' +
      Cn(#$E7#$BB#$88#$E7#$AB#$AF#$48#$54#$54#$50) + '] ' +
      Cn(#$E5#$B7#$B2#$E6#$94#$B6#$E5#$88#$B0#$E7#$BB#$88#$E7#$AB#$AF#$E5#$93#$8D#$E5#$BA#$94#$EF#$BC#$9A) +
      Detail + Cn(#$EF#$BC#$8C) + ' ' +
      Cn(#$E5#$93#$8D#$E5#$BA#$94#$E6#$91#$98#$E8#$A6#$81) + '=' +
      SummarizeTerminalResponse(ResponseUtf8));
end;

function TTerminalClient.PostJson(const BaseUrl, Path, BodyUtf8: string; out ResponseUtf8: string): Boolean;
var
  FullUrl: string;
begin
  ResetLastResult;
  FullUrl := BaseUrl;
  if Copy(FullUrl, Length(FullUrl), 1) <> '/' then
    FullUrl := FullUrl + Path
  else
    FullUrl := FullUrl + Copy(Path, 2, MaxInt);
  Result := HttpRequest('POST', FullUrl, BodyUtf8, ResponseUtf8,
    FLastStage, FLastErrorCode, FLastHttpStatus);
  LogHttpResult('POST', FullUrl, ResponseUtf8, Self, Result);
end;

function TTerminalClient.GetJson(const BaseUrl, Path: string; out ResponseUtf8: string): Boolean;
var
  FullUrl: string;
begin
  ResetLastResult;
  FullUrl := BaseUrl;
  if Copy(FullUrl, Length(FullUrl), 1) <> '/' then
    FullUrl := FullUrl + Path
  else
    FullUrl := FullUrl + Copy(Path, 2, MaxInt);
  Result := HttpRequest('GET', FullUrl, '', ResponseUtf8,
    FLastStage, FLastErrorCode, FLastHttpStatus);
  LogHttpResult('GET', FullUrl, ResponseUtf8, Self, Result);
end;

end.
