
unit TerminalManager;

interface

type
  TTerminalInfo = record
    Index: Integer;
    Name: string;
    BaseUrl: string;
  end;

  TTerminalManager = class
  private
    FTerminals: array[1..2] of TTerminalInfo;
    FCurrentIndex: Integer;
    FProcessActive: Boolean;
    FProcessSaveDir: string;
    function GetCurrentBaseUrl: string;
    function GetCurrentName: string;
  public
    constructor Create;
    procedure LoadFromConfig(const JsonText: string);
    function SwitchTo(Index: Integer): Boolean;
    function IsSameTerminal(Index: Integer): Boolean;
    function GetTerminal(Index: Integer): TTerminalInfo;
    property CurrentIndex: Integer read FCurrentIndex;
    property CurrentBaseUrl: string read GetCurrentBaseUrl;
    property CurrentName: string read GetCurrentName;
    property ProcessActive: Boolean read FProcessActive write FProcessActive;
    property ProcessSaveDir: string read FProcessSaveDir write FProcessSaveDir;
  end;

implementation

uses SysUtils, EncodingHelper;

function ExtractJsonStr(const Json, Key: string): string;
var
  K, StartPos, EndPos: Integer;
begin
  Result := '';
  K := Pos('"' + Key + '"', Json);
  if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] <> ':') do Inc(K);
  Inc(K);
  while (K <= Length(Json)) and (Json[K] in [' ', #9, #10, #13]) do Inc(K);
  if (K > Length(Json)) or (Json[K] <> '"') then Exit;
  Inc(K);
  EndPos := K;
  while (EndPos <= Length(Json)) and (Json[EndPos] <> '"') do Inc(EndPos);
  Result := Copy(Json, K, EndPos - K);
end;

constructor TTerminalManager.Create;
begin
  inherited Create;
  FCurrentIndex := 1;
  FProcessActive := False;
  FProcessSaveDir := '';
  FTerminals[1].Index := 1;
  FTerminals[1].Name := 'жу╤к' + '1';
  FTerminals[1].BaseUrl := 'http://192.168.20.30:9098';
  FTerminals[2].Index := 2;
  FTerminals[2].Name := 'жу╤к' + '2';
  FTerminals[2].BaseUrl := 'http://192.168.20.31:9098';
end;

function TTerminalManager.GetCurrentBaseUrl: string;
begin
  Result := FTerminals[FCurrentIndex].BaseUrl;
end;

function TTerminalManager.GetCurrentName: string;
begin
  Result := FTerminals[FCurrentIndex].Name;
end;

function TTerminalManager.GetTerminal(Index: Integer): TTerminalInfo;
begin
  if (Index >= 1) and (Index <= 2) then
    Result := FTerminals[Index]
  else
  begin
    Result.Index := 0;
    Result.Name := '';
    Result.BaseUrl := '';
  end;
end;

procedure TTerminalManager.LoadFromConfig(const JsonText: string);
var
  Name, BaseUrl: string;
begin
  // fixed_terminals array
  Name := ExtractJsonStr(JsonText, 'name');
  BaseUrl := ExtractJsonStr(JsonText, 'base_url');
  if (Name <> '') and (BaseUrl <> '') then
  begin
    // Check if this is terminal 1 or 2 by URL
    if Pos('30', BaseUrl) > 0 then
    begin
      FTerminals[1].Name := Utf8ToAnsi(Name);
      FTerminals[1].BaseUrl := BaseUrl;
    end
    else if Pos('31', BaseUrl) > 0 then
    begin
      FTerminals[2].Name := Utf8ToAnsi(Name);
      FTerminals[2].BaseUrl := BaseUrl;
    end;
  end;
end;

function TTerminalManager.SwitchTo(Index: Integer): Boolean;
begin
  Result := False;
  if (Index < 1) or (Index > 2) then Exit;
  FCurrentIndex := Index;
  Result := True;
end;

function TTerminalManager.IsSameTerminal(Index: Integer): Boolean;
begin
  Result := (FCurrentIndex = Index);
end;

end.
