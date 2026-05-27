object FormMain: TFormMain
  Left = 250
  Top = 160
  Width = 980
  Height = 680
  Caption = 'HZCYKJTHardWare DLL Test'
  Color = clBtnFace
  Font.Charset = DEFAULT_CHARSET
  Font.Color = clWindowText
  Font.Height = -12
  Font.Name = 'Tahoma'
  Font.Style = []
  OldCreateOrder = False
  OnCreate = FormCreate
  OnDestroy = FormDestroy
  PixelsPerInch = 96
  TextHeight = 14
  object PanelTop: TPanel
    Left = 0
    Top = 0
    Width = 964
    Height = 208
    Align = alTop
    TabOrder = 0
    object LblAuthSample: TLabel
      Left = 8
      Top = 145
      Width = 72
      Height = 14
      Caption = #25480#26435#27169#25311#21442#25968
    end
    object LblAuthZJHM: TLabel
      Left = 8
      Top = 164
      Width = 48
      Height = 14
      Caption = #35777#20214#21495#30721
    end
    object LblAuthZJLB: TLabel
      Left = 170
      Top = 164
      Width = 48
      Height = 14
      Caption = #35777#20214#31867#21035
    end
    object LblAuthGJDQDM: TLabel
      Left = 256
      Top = 164
      Width = 72
      Height = 14
      Caption = #22269#23478#22320#21306#20195#30721
    end
    object LblAuthXM: TLabel
      Left = 370
      Top = 164
      Width = 24
      Height = 14
      Caption = #22995#21517
    end
    object LblAuthXB: TLabel
      Left = 492
      Top = 164
      Width = 24
      Height = 14
      Caption = #24615#21035
    end
    object LblAuthCSRQ: TLabel
      Left = 558
      Top = 164
      Width = 48
      Height = 14
      Caption = #20986#29983#26085#26399
    end
    object LblAuthKADM: TLabel
      Left = 680
      Top = 164
      Width = 48
      Height = 14
      Caption = #21475#23736#20195#30721
    end
    object BtnInit: TButton
      Left = 8
      Top = 8
      Width = 100
      Height = 25
      Caption = #21021#22987#21270
      TabOrder = 0
      OnClick = BtnInitClick
    end
    object BtnRelease: TButton
      Left = 114
      Top = 8
      Width = 100
      Height = 25
      Caption = #37322#25918
      TabOrder = 1
      OnClick = BtnReleaseClick
    end
    object BtnSwitch1: TButton
      Left = 220
      Top = 8
      Width = 100
      Height = 25
      Caption = #32456#31471'1'
      TabOrder = 2
      OnClick = BtnSwitch1Click
    end
    object BtnSwitch2: TButton
      Left = 326
      Top = 8
      Width = 100
      Height = 25
      Caption = #32456#31471'2'
      TabOrder = 3
      OnClick = BtnSwitch2Click
    end
    object BtnStartProcess: TButton
      Left = 432
      Top = 8
      Width = 100
      Height = 25
      Caption = #24320#22987#27969#31243
      TabOrder = 4
      OnClick = BtnStartProcessClick
    end
    object BtnEndProcess: TButton
      Left = 538
      Top = 8
      Width = 100
      Height = 25
      Caption = #32467#26463#27969#31243
      TabOrder = 5
      OnClick = BtnEndProcessClick
    end
    object EdtSaveDir: TEdit
      Left = 8
      Top = 42
      Width = 500
      Height = 22
      TabOrder = 6
      Text = '.\captures'
    end
    object BtnCameraPreview: TButton
      Left = 8
      Top = 76
      Width = 130
      Height = 25
      Caption = #35270#39057#39044#35272
      TabOrder = 7
      OnClick = BtnCameraPreviewClick
    end
    object BtnStopCamPreview: TButton
      Left = 144
      Top = 76
      Width = 130
      Height = 25
      Caption = #20572#27490#35270#39057#39044#35272
      TabOrder = 8
      OnClick = BtnStopCamPreviewClick
    end
    object BtnFpPreview: TButton
      Left = 280
      Top = 76
      Width = 130
      Height = 25
      Caption = #25351#32441#39044#35272
      TabOrder = 9
      OnClick = BtnFpPreviewClick
    end
    object BtnStopFpPreview: TButton
      Left = 416
      Top = 76
      Width = 130
      Height = 25
      Caption = #20572#27490#25351#32441#39044#35272
      TabOrder = 10
      OnClick = BtnStopFpPreviewClick
    end
    object BtnFaceCapture: TButton
      Left = 8
      Top = 112
      Width = 100
      Height = 25
      Caption = #20154#33080#25235#25293
      TabOrder = 11
      OnClick = BtnFaceCaptureClick
    end
    object BtnFpCapture: TButton
      Left = 114
      Top = 112
      Width = 100
      Height = 25
      Caption = #25351#32441#25235#25293
      TabOrder = 12
      OnClick = BtnFpCaptureClick
    end
    object BtnOCR: TButton
      Left = 220
      Top = 112
      Width = 100
      Height = 25
      Caption = 'OCR'
      TabOrder = 13
      OnClick = BtnOCRClick
    end
    object BtnNFC: TButton
      Left = 326
      Top = 112
      Width = 100
      Height = 25
      Caption = 'NFC/IC'
      TabOrder = 14
      OnClick = BtnNFCClick
    end
    object BtnIrisCapture: TButton
      Left = 432
      Top = 112
      Width = 100
      Height = 25
      Caption = #34425#33180#25235#25293
      TabOrder = 15
      OnClick = BtnIrisCaptureClick
    end
    object BtnAuthorize: TButton
      Left = 538
      Top = 112
      Width = 120
      Height = 25
      Caption = #25480#26435#27169#25311
      TabOrder = 16
      OnClick = BtnAuthorizeClick
    end
    object EdtAuthZJHM: TEdit
      Left = 8
      Top = 180
      Width = 154
      Height = 22
      TabOrder = 17
      Text = 'H111111111'
    end
    object EdtAuthZJLB: TEdit
      Left = 170
      Top = 180
      Width = 78
      Height = 22
      TabOrder = 18
      Text = '24'
    end
    object EdtAuthGJDQDM: TEdit
      Left = 256
      Top = 180
      Width = 106
      Height = 22
      TabOrder = 19
      Text = 'HKG'
    end
    object EdtAuthXM: TEdit
      Left = 370
      Top = 180
      Width = 114
      Height = 22
      TabOrder = 20
      Text = 'TEST'
    end
    object EdtAuthXB: TEdit
      Left = 492
      Top = 180
      Width = 58
      Height = 22
      TabOrder = 21
      Text = 'M'
    end
    object EdtAuthCSRQ: TEdit
      Left = 558
      Top = 180
      Width = 114
      Height = 22
      TabOrder = 22
      Text = '19950101'
    end
    object EdtAuthKADM: TEdit
      Left = 680
      Top = 180
      Width = 82
      Height = 22
      TabOrder = 23
      Text = '414'
    end
  end
  object PanelCamera: TPanel
    Left = 0
    Top = 208
    Width = 320
    Height = 252
    Align = alLeft
    Caption = #35270#39057#39044#35272
    Color = clBlack
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWhite
    Font.Height = -16
    Font.Name = 'Tahoma'
    Font.Style = []
    ParentFont = False
    TabOrder = 1
  end
  object PanelFingerprint: TPanel
    Left = 320
    Top = 208
    Width = 320
    Height = 252
    Align = alLeft
    Caption = #25351#32441#39044#35272
    Color = clBlack
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWhite
    Font.Height = -16
    Font.Name = 'Tahoma'
    Font.Style = []
    ParentFont = False
    TabOrder = 2
  end
  object PanelIris: TPanel
    Left = 640
    Top = 208
    Width = 324
    Height = 252
    Align = alClient
    Caption = #34425#33180#39044#35272
    Color = clBlack
    Font.Charset = DEFAULT_CHARSET
    Font.Color = clWhite
    Font.Height = -16
    Font.Name = 'Tahoma'
    Font.Style = []
    ParentFont = False
    TabOrder = 3
  end
  object MemoLog: TMemo
    Left = 0
    Top = 460
    Width = 964
    Height = 181
    Align = alBottom
    ScrollBars = ssBoth
    TabOrder = 4
  end
end
