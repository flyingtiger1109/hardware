object FormMain: TFormMain
  Left = 291
  Top = 134
  Width = 980
  Height = 680
  Caption = 'HZCYKJTHardWare - '#21518#31471#26381#21153
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
    Height = 248
    Align = alTop
    TabOrder = 0
    object BtnStartServer: TButton
      Left = 8
      Top = 4
      Width = 100
      Height = 23
      Caption = #21551#21160#26381#21153
      TabOrder = 0
      OnClick = BtnStartServerClick
    end
    object BtnStopServer: TButton
      Left = 116
      Top = 4
      Width = 100
      Height = 23
      Caption = #20572#27490#26381#21153
      TabOrder = 1
      OnClick = BtnStopServerClick
    end
    object BtnStartProcess: TButton
      Left = 8
      Top = 36
      Width = 100
      Height = 23
      Caption = #24320#22987#27969#31243
      TabOrder = 2
      OnClick = BtnStartProcessClick
    end
    object BtnEndProcess: TButton
      Left = 116
      Top = 36
      Width = 100
      Height = 23
      Caption = #32467#26463#27969#31243
      TabOrder = 3
      OnClick = BtnEndProcessClick
    end
    object BtnSwitchTerminal1: TButton
      Left = 224
      Top = 36
      Width = 80
      Height = 23
      Caption = #32456#31471'1'
      TabOrder = 17
      OnClick = BtnSwitchTerminal1Click
    end
    object BtnSwitchTerminal2: TButton
      Left = 310
      Top = 36
      Width = 80
      Height = 23
      Caption = #32456#31471'2'
      TabOrder = 18
      OnClick = BtnSwitchTerminal2Click
    end
    object BtnFaceCapture: TButton
      Left = 8
      Top = 68
      Width = 100
      Height = 23
      Caption = #20154#33080#25235#25293
      TabOrder = 4
      OnClick = BtnFaceCaptureClick
    end
    object BtnFingerprintCapture: TButton
      Left = 116
      Top = 68
      Width = 100
      Height = 23
      Caption = #25351#32441#25235#25293
      TabOrder = 5
      OnClick = BtnFingerprintCaptureClick
    end
    object BtnOCR: TButton
      Left = 224
      Top = 68
      Width = 100
      Height = 23
      Caption = 'OCR '#38405#35835
      TabOrder = 6
      OnClick = BtnOCRClick
    end
    object BtnNfcCard: TButton
      Left = 332
      Top = 68
      Width = 100
      Height = 23
      Caption = 'IC '#21345#35782#21035
      TabOrder = 7
      OnClick = BtnNfcCardClick
    end
    object BtnIrisCapture: TButton
      Left = 440
      Top = 68
      Width = 100
      Height = 23
      Caption = #34425#33180#25235#25293
      TabOrder = 8
      OnClick = BtnIrisCaptureClick
    end
    object BtnStartCameraPreview: TButton
      Left = 8
      Top = 100
      Width = 160
      Height = 23
      Caption = #24320#22987#25668#20687#22836#39044#35272
      TabOrder = 9
      OnClick = BtnStartCameraPreviewClick
    end
    object BtnStopCameraPreview: TButton
      Left = 176
      Top = 100
      Width = 160
      Height = 23
      Caption = #20572#27490#25668#20687#22836#39044#35272
      TabOrder = 10
      OnClick = BtnStopCameraPreviewClick
    end
    object BtnStartFingerprintPreview: TButton
      Left = 8
      Top = 132
      Width = 160
      Height = 23
      Caption = #24320#22987#25351#32441#39044#35272
      TabOrder = 11
      OnClick = BtnStartFingerprintPreviewClick
    end
    object BtnStopFingerprintPreview: TButton
      Left = 176
      Top = 132
      Width = 160
      Height = 23
      Caption = #20572#27490#25351#32441#39044#35272
      TabOrder = 12
      OnClick = BtnStopFingerprintPreviewClick
    end
    object BtnStartIrisPreview: TButton
      Left = 8
      Top = 164
      Width = 160
      Height = 23
      Caption = #24320#22987#34425#33180#39044#35272
      TabOrder = 13
      OnClick = BtnStartIrisPreviewClick
    end
    object BtnStopIrisPreview: TButton
      Left = 176
      Top = 164
      Width = 160
      Height = 23
      Caption = #20572#27490#34425#33180#39044#35272
      TabOrder = 14
      OnClick = BtnStopIrisPreviewClick
    end
    object BtnStartPlatePreview: TButton
      Left = 8
      Top = 196
      Width = 160
      Height = 23
      Caption = #24320#22987#36710#29260#39044#35272
      TabOrder = 15
      OnClick = BtnStartPlatePreviewClick
    end
    object BtnStopPlatePreview: TButton
      Left = 176
      Top = 196
      Width = 160
      Height = 23
      Caption = #20572#27490#36710#29260#39044#35272
      TabOrder = 16
      OnClick = BtnStopPlatePreviewClick
    end
  end
  object PanelPreview: TPanel
    Left = 0
    Top = 248
    Width = 964
    Height = 300
    Align = alTop
    TabOrder = 1
    object Splitter1: TSplitter
      Left = 301
      Top = 1
      Width = 4
      Height = 298
    end
    object Splitter2: TSplitter
      Left = 605
      Top = 1
      Width = 4
      Height = 298
    end
    object PanelCamera: TPanel
      Left = 1
      Top = 1
      Width = 300
      Height = 298
      Align = alLeft
      Caption = #25668#20687#22836#39044#35272
      Color = clBlack
      Font.Charset = DEFAULT_CHARSET
      Font.Color = clWhite
      Font.Height = -14
      Font.Name = 'Tahoma'
      Font.Style = []
      ParentFont = False
      TabOrder = 0
    end
    object PanelFingerprint: TPanel
      Left = 305
      Top = 1
      Width = 300
      Height = 298
      Align = alLeft
      Caption = #25351#32441#39044#35272
      Color = clBlack
      Font.Charset = DEFAULT_CHARSET
      Font.Color = clWhite
      Font.Height = -14
      Font.Name = 'Tahoma'
      Font.Style = []
      ParentFont = False
      TabOrder = 1
    end
    object PanelIris: TPanel
      Left = 609
      Top = 1
      Width = 354
      Height = 298
      Align = alClient
      Caption = #34425#33180#39044#35272
      Color = clBlack
      Font.Charset = DEFAULT_CHARSET
      Font.Color = clWhite
      Font.Height = -14
      Font.Name = 'Tahoma'
      Font.Style = []
      ParentFont = False
      TabOrder = 2
    end
  end
  object MemoLog: TMemo
    Left = 0
    Top = 548
    Width = 964
    Height = 93
    Align = alClient
    ScrollBars = ssBoth
    TabOrder = 2
  end
end
