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
    Height = 160
    Align = alTop
    TabOrder = 0
    object BtnInit: TButton
      Left = 8
      Top = 8
      Width = 100
      Height = 25
      Caption = 'InitSdk'
      TabOrder = 0
      OnClick = BtnInitClick
    end
    object BtnRelease: TButton
      Left = 114
      Top = 8
      Width = 100
      Height = 25
      Caption = 'Release'
      TabOrder = 1
      OnClick = BtnReleaseClick
    end
    object BtnSwitch1: TButton
      Left = 220
      Top = 8
      Width = 100
      Height = 25
      Caption = 'Terminal 1'
      TabOrder = 2
      OnClick = BtnSwitch1Click
    end
    object BtnSwitch2: TButton
      Left = 326
      Top = 8
      Width = 100
      Height = 25
      Caption = 'Terminal 2'
      TabOrder = 3
      OnClick = BtnSwitch2Click
    end
    object BtnStartProcess: TButton
      Left = 432
      Top = 8
      Width = 100
      Height = 25
      Caption = 'StartProc'
      TabOrder = 4
      OnClick = BtnStartProcessClick
    end
    object BtnEndProcess: TButton
      Left = 538
      Top = 8
      Width = 100
      Height = 25
      Caption = 'EndProc'
      TabOrder = 5
      OnClick = BtnEndProcessClick
    end
    object EdtSaveDir: TEdit
      Left = 8
      Top = 42
      Width = 500
      Height = 25
      TabOrder = 6
      Text = '.\captures'
    end
    object BtnCameraPreview: TButton
      Left = 8
      Top = 76
      Width = 130
      Height = 25
      Caption = 'Cam Preview'
      TabOrder = 7
      OnClick = BtnCameraPreviewClick
    end
    object BtnStopCamPreview: TButton
      Left = 144
      Top = 76
      Width = 130
      Height = 25
      Caption = 'Stop Cam'
      TabOrder = 8
      OnClick = BtnStopCamPreviewClick
    end
    object BtnFpPreview: TButton
      Left = 280
      Top = 76
      Width = 130
      Height = 25
      Caption = 'FP Preview'
      TabOrder = 9
      OnClick = BtnFpPreviewClick
    end
    object BtnStopFpPreview: TButton
      Left = 416
      Top = 76
      Width = 130
      Height = 25
      Caption = 'Stop FP'
      TabOrder = 10
      OnClick = BtnStopFpPreviewClick
    end
    object BtnFaceCapture: TButton
      Left = 8
      Top = 112
      Width = 100
      Height = 25
      Caption = 'Face Cap'
      TabOrder = 11
      OnClick = BtnFaceCaptureClick
    end
    object BtnFpCapture: TButton
      Left = 114
      Top = 112
      Width = 100
      Height = 25
      Caption = 'FP Cap'
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
      Caption = 'Iris Cap'
      TabOrder = 15
      OnClick = BtnIrisCaptureClick
    end
  end
  object PanelCamera: TPanel
    Left = 0
    Top = 160
    Width = 320
    Height = 300
    Align = alLeft
    Caption = 'Camera Preview'
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
    Top = 160
    Width = 320
    Height = 300
    Align = alLeft
    Caption = 'Fingerprint Preview'
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
    Top = 160
    Width = 324
    Height = 300
    Align = alClient
    Caption = 'Iris Preview'
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
