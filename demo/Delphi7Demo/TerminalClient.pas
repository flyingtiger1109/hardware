unit TerminalClient;

interface

uses SysUtils;

type
  TTerminalClient = class
  public
    function PostJson(const BaseUrl, Path, BodyUtf8: string; out ResponseUtf8: string): Boolean;
    function GetJson(const BaseUrl, Path: string; out ResponseUtf8: string): Boolean;
  end;

implementation

uses Windows, WinInet;

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

function HttpRequest(Method, Url, BodyUtf8: string; out ResponseUtf8: string): Boolean;
var
  hInet, hConn, hReq: HINTERNET;
  Host, Path, Headers: string;
  Port: Integer;
  Buf: array[0..4095] of Byte;
  BytesRead: Cardinal;
  Flags: DWORD;
  Timeout: DWORD;
begin
  Result := False;
  ResponseUtf8 := '';
  if not ParseUrl(Url, Host, Port, Path) then Exit;

  hInet := InternetOpen('DelphiTerminalClient', INTERNET_OPEN_TYPE_DIRECT, nil, nil, 0);
  if not Assigned(hInet) then Exit;
  try
    // Set timeouts: 5s connect, 10s send, 10s receive
    Timeout := 5000;
    InternetSetOption(hInet, INTERNET_OPTION_CONNECT_TIMEOUT, @Timeout, SizeOf(Timeout));
    Timeout := 10000;
    InternetSetOption(hInet, INTERNET_OPTION_SEND_TIMEOUT, @Timeout, SizeOf(Timeout));
    InternetSetOption(hInet, INTERNET_OPTION_RECEIVE_TIMEOUT, @Timeout, SizeOf(Timeout));

    hConn := InternetConnect(hInet, PChar(Host), Port, nil, nil, INTERNET_SERVICE_HTTP, 0, 0);
    if not Assigned(hConn) then Exit;
    try
      Flags := INTERNET_FLAG_RELOAD or INTERNET_FLAG_NO_CACHE_WRITE;
      hReq := HttpOpenRequest(hConn, PChar(Method), PChar(Path), nil, nil, nil, Flags, 0);
      if not Assigned(hReq) then Exit;
      try
        Headers := 'Content-Type: application/json; charset=utf-8'#13#10;
        if not HttpSendRequest(hReq, PChar(Headers), Length(Headers),
                               PChar(BodyUtf8), Length(BodyUtf8)) then Exit;

        repeat
          if not InternetReadFile(hReq, @Buf, SizeOf(Buf), BytesRead) then Exit;
          if BytesRead > 0 then
            ResponseUtf8 := ResponseUtf8 + Copy(string(PChar(@Buf)), 1, BytesRead);
        until BytesRead = 0;

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

function TTerminalClient.PostJson(const BaseUrl, Path, BodyUtf8: string; out ResponseUtf8: string): Boolean;
var
  FullUrl: string;
begin
  FullUrl := BaseUrl;
  if Copy(FullUrl, Length(FullUrl), 1) <> '/' then
    FullUrl := FullUrl + Path
  else
    FullUrl := FullUrl + Copy(Path, 2, MaxInt);
  Result := HttpRequest('POST', FullUrl, BodyUtf8, ResponseUtf8);
end;

function TTerminalClient.GetJson(const BaseUrl, Path: string; out ResponseUtf8: string): Boolean;
var
  FullUrl: string;
begin
  FullUrl := BaseUrl;
  if Copy(FullUrl, Length(FullUrl), 1) <> '/' then
    FullUrl := FullUrl + Path
  else
    FullUrl := FullUrl + Copy(Path, 2, MaxInt);
  Result := HttpRequest('GET', FullUrl, '', ResponseUtf8);
end;

end.
