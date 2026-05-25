program HZCYKJTDemo;

uses
  Forms,
  MainUnit in 'MainUnit.pas' {FormMain};

begin
  Application.Initialize;
  Application.Title := 'HZCYKJTHardWare Delphi 7 Demo';
  Application.CreateForm(TFormMain, FormMain);
  Application.Run;
end.
