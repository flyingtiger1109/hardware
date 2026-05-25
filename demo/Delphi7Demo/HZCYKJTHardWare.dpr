program HZCYKJTHardWare;

uses
  Forms,
  MainUnit in 'MainUnit.pas' {FormMain},
  DelphiProxyServer in 'DelphiProxyServer.pas',
  VlcPlayer in 'VlcPlayer.pas',
  PreviewManager in 'PreviewManager.pas';

begin
  Application.Initialize;
  Application.Title := 'HZCYKJTHardWare Delphi Service Demo';
  Application.CreateForm(TFormMain, FormMain);
  Application.Run;
end.
