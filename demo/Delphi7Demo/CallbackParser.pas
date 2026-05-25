unit CallbackParser;

interface

uses SysUtils, Classes;

type
  TEvidenceImage = record
    CardType: Integer;
    DataType: Integer;
    ImageType: Integer;
    LampType: Integer;
    ImageBase64: string;
    ImageData: string;
    Width: Integer;
    Height: Integer;
  end;

  TOcrCallbackResult = record
    RequestId: string;
    Mrz: string;
    CardType: Integer;
    EvidenceImages: array of TEvidenceImage;
    EvidenceImagesCount: Integer;
    Valid: Boolean;
  end;

  TNfcCallbackResult = record
    RequestId: string;
    CardText: string;
    Valid: Boolean;
  end;

  TImageCallbackResult = record
    RequestId: string;
    ResourceType: string;
    ImageBase64: string;
    ImageMimeType: string;
    Valid: Boolean;
  end;

  TCallbackParser = class
  public
    function ParseOcrDocument(const BodyUtf8: string): TOcrCallbackResult;
    function ParseNfcCard(const BodyUtf8: string): TNfcCallbackResult;
    function ParseImageCapture(const BodyUtf8: string): TImageCallbackResult;
    function GetResourceType(const BodyUtf8: string): string;
    function ExtractField(const Json, Key: string): string;
    function ExtractIntField(const Json, Key: string): Integer;
    function ExtractJsonArray(const Json, Key: string): string;
    function ParseEvidenceImages(const DataSection: string; var Images: array of TEvidenceImage; MaxCount: Integer): Integer;
  end;

implementation

function TCallbackParser.ExtractField(const Json, Key: string): string;
var
  K, I: Integer;
  Escaped: Boolean;
begin
  Result := '';
  K := Pos('"' + Key + '"', Json);
  if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', #9, #10, #13, ':']) do Inc(K);
  if (K > Length(Json)) or (Json[K] <> '"') then Exit;
  Inc(K);
  Escaped := False;
  for I := K to Length(Json) do
  begin
    if Escaped then
    begin
      case Json[I] of
        'n': Result := Result + #10;
        'r': Result := Result + #13;
        't': Result := Result + #9;
      else
        Result := Result + Json[I];
      end;
      Escaped := False;
    end
    else if Json[I] = '\' then
      Escaped := True
    else if Json[I] = '"' then
      Exit
    else
      Result := Result + Json[I];
  end;
end;

function TCallbackParser.ExtractIntField(const Json, Key: string): Integer;
var
  S: string;
  K: Integer;
begin
  S := ExtractField(Json, Key);
  if S <> '' then begin Result := StrToIntDef(S, 0); Exit; end;
  Result := 0;
  K := Pos('"' + Key + '"', Json);
  if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', #9, #10, #13, ':']) do Inc(K);
  S := '';
  while (K <= Length(Json)) and (Json[K] in ['0'..'9', '-']) do begin S := S + Json[K]; Inc(K); end;
  Result := StrToIntDef(S, 0);
end;

function TCallbackParser.ExtractJsonArray(const Json, Key: string): string;
var
  K, Depth, StartPos: Integer;
begin
  Result := '';
  K := Pos('"' + Key + '"', Json);
  if K = 0 then Exit;
  K := K + Length(Key) + 2;
  while (K <= Length(Json)) and (Json[K] in [' ', #9, #10, #13, ':']) do Inc(K);
  if (K > Length(Json)) or (Json[K] <> '[') then Exit;
  StartPos := K;
  Depth := 1;
  Inc(K);
  while (K <= Length(Json)) and (Depth > 0) do
  begin
    if Json[K] = '[' then Inc(Depth)
    else if Json[K] = ']' then Dec(Depth);
    Inc(K);
  end;
  if Depth = 0 then
    Result := Copy(Json, StartPos, K - StartPos);
end;

function TCallbackParser.GetResourceType(const BodyUtf8: string): string;
begin
  Result := ExtractField(BodyUtf8, 'resource_type');
end;

function TCallbackParser.ParseEvidenceImages(const DataSection: string; var Images: array of TEvidenceImage; MaxCount: Integer): Integer;
var
  ImgArray, Elem: string;
  K, Depth, I: Integer;
begin
  Result := 0;
  ImgArray := ExtractJsonArray(DataSection, 'evidence_images');
  if ImgArray = '' then Exit;

  // Parse each image object in the array
  K := 2; // skip opening '['
  while (K <= Length(ImgArray)) and (Result < MaxCount) do
  begin
    // skip to next '{'
    while (K <= Length(ImgArray)) and (ImgArray[K] <> '{') do Inc(K);
    if K > Length(ImgArray) then Break;

    // find matching '}'
    Depth := 1;
    I := K + 1;
    while (I <= Length(ImgArray)) and (Depth > 0) do
    begin
      if ImgArray[I] = '{' then Inc(Depth)
      else if ImgArray[I] = '}' then Dec(Depth);
      Inc(I);
    end;
    if Depth <> 0 then Break;

    Elem := Copy(ImgArray, K, I - K);

    Images[Result].CardType := ExtractIntField(Elem, 'cardType');
    Images[Result].DataType := ExtractIntField(Elem, 'dataType');
    Images[Result].ImageType := ExtractIntField(Elem, 'imageType');
    Images[Result].LampType := ExtractIntField(Elem, 'lampType');
    Images[Result].ImageBase64 := ExtractField(Elem, 'imageData');
    if Images[Result].ImageBase64 = '' then
      Images[Result].ImageBase64 := ExtractField(Elem, 'image_base64');
    Images[Result].Width := ExtractIntField(Elem, 'imageWidth');
    Images[Result].Height := ExtractIntField(Elem, 'imageHeight');

    Inc(Result);
    K := I;
  end;
end;

function TCallbackParser.ParseOcrDocument(const BodyUtf8: string): TOcrCallbackResult;
var
  DataSection, PersonInfo: string;
  P: Integer;
  // Scan all person_info entries for MRZ fields
  function FindMrzInPersonInfo(const Pi: string): string;
  var
    M1, M2, M3: string;
  begin
    Result := '';
    // Try uppercase field names (terminal uses MRZ1/MRZ2/MRZ3)
    M1 := ExtractField(Pi, 'MRZ1'); if M1 = '' then M1 := ExtractField(Pi, 'mrz1');
    M2 := ExtractField(Pi, 'MRZ2'); if M2 = '' then M2 := ExtractField(Pi, 'mrz2');
    M3 := ExtractField(Pi, 'MRZ3'); if M3 = '' then M3 := ExtractField(Pi, 'mrz3');
    if (M1 <> '') or (M2 <> '') or (M3 <> '') then
      Result := M1 + '^' + M2 + '^' + M3;
  end;
begin
  FillChar(Result, SizeOf(Result), 0);
  Result.RequestId := ExtractField(BodyUtf8, 'request_id');

  // Extract data section
  P := Pos('"data"', BodyUtf8);
  if P > 0 then
  begin
    P := P + 6;
    while (P <= Length(BodyUtf8)) and (BodyUtf8[P] in [' ', #9, #10, #13, ':']) do Inc(P);
    DataSection := Copy(BodyUtf8, P, MaxInt);
  end;

  Result.CardType := ExtractIntField(DataSection, 'card_type');

  // Search ALL occurrences of mrz1/mrz2/mrz3 in the entire body
  // (not just in person_info - they might be at different nesting levels)
  Result.Mrz := FindMrzInPersonInfo(BodyUtf8);

  // Parse evidence_images
  SetLength(Result.EvidenceImages, 20);
  Result.EvidenceImagesCount := ParseEvidenceImages(DataSection, Result.EvidenceImages[0], 20);

  Result.Valid := True;
end;

function TCallbackParser.ParseNfcCard(const BodyUtf8: string): TNfcCallbackResult;
var
  DataSection, RawCardText: string;
  P: Integer;
begin
  FillChar(Result, SizeOf(Result), 0);
  Result.RequestId := ExtractField(BodyUtf8, 'request_id');

  // Try to find card_text in the entire body first
  Result.CardText := ExtractField(BodyUtf8, 'card_text');

  // If not found, try in data section
  if Result.CardText = '' then
  begin
    P := Pos('"data"', BodyUtf8);
    if P > 0 then
    begin
      P := P + 6;
      while (P <= Length(BodyUtf8)) and (BodyUtf8[P] in [' ', #9, #10, #13, ':']) do Inc(P);
      DataSection := Copy(BodyUtf8, P, MaxInt);
      Result.CardText := ExtractField(DataSection, 'card_text');
    end;
  end;

  // Also try card_text as integer (some terminals send numeric card IDs)
  if Result.CardText = '' then
  begin
    RawCardText := ExtractField(BodyUtf8, 'card_text');
    if RawCardText = '' then
    begin
      // Try searching for any field that might contain the card number
      // Common alternative field names
      Result.CardText := ExtractField(BodyUtf8, 'card_id');
      if Result.CardText = '' then
        Result.CardText := ExtractField(BodyUtf8, 'cardId');
      if Result.CardText = '' then
        Result.CardText := ExtractField(BodyUtf8, 'id_number');
    end;
  end;

  Result.Valid := Result.CardText <> '';
end;

function TCallbackParser.ParseImageCapture(const BodyUtf8: string): TImageCallbackResult;
var
  DataSection: string;
  P: Integer;
begin
  FillChar(Result, SizeOf(Result), 0);
  Result.RequestId := ExtractField(BodyUtf8, 'request_id');
  Result.ResourceType := ExtractField(BodyUtf8, 'resource_type');

  // Extract data section
  P := Pos('"data"', BodyUtf8);
  if P > 0 then
  begin
    P := P + 6;
    while (P <= Length(BodyUtf8)) and (BodyUtf8[P] in [' ', #9, #10, #13, ':']) do Inc(P);
    DataSection := Copy(BodyUtf8, P, MaxInt);
  end
  else
    DataSection := BodyUtf8;

  // Try multiple field names based on resource_type
  if Result.ResourceType = 'face_image' then
  begin
    Result.ImageBase64 := ExtractField(DataSection, 'face_capture');
    if Result.ImageBase64 = '' then
      Result.ImageBase64 := ExtractField(DataSection, 'image_base64');
  end
  else if Result.ResourceType = 'fingerprint_image' then
  begin
    Result.ImageBase64 := ExtractField(DataSection, 'image_base64');
    if Result.ImageBase64 = '' then
      Result.ImageBase64 := ExtractField(DataSection, 'fingerprint_capture');
  end
  else if Result.ResourceType = 'iris_image' then
  begin
    Result.ImageBase64 := ExtractField(DataSection, 'leftIris_capture');
  end
  else
  begin
    // Unknown resource type, try common field names
    Result.ImageBase64 := ExtractField(DataSection, 'image_base64');
    if Result.ImageBase64 = '' then
      Result.ImageBase64 := ExtractField(DataSection, 'face_capture');
    if Result.ImageBase64 = '' then
      Result.ImageBase64 := ExtractField(DataSection, 'fingerprint_capture');
  end;

  Result.ImageMimeType := ExtractField(DataSection, 'image_mime_type');
  if Result.ImageMimeType = '' then
  begin
    if Result.ResourceType = 'fingerprint_image' then
      Result.ImageMimeType := 'image/jpeg'
    else
      Result.ImageMimeType := 'image/bmp';
  end;

  Result.Valid := Result.ImageBase64 <> '';
end;

end.
