�? HZCYKJTHardWare.DLL �?閲囬泦缁堢 HTTP 鏈嶅姟璋冪敤 DLL

## 1. 姒傝�?
HZCYKJTHardWare.DLL 鏄竴涓?Windows 鍔ㄦ€侀摼鎺ュ簱锛屼緵绗笁鏂圭▼搴忥紙C++銆丆#銆丏elphi銆丳ython 绛夛級閫氳繃 HTTP 鍗忚璋冪敤宸插瓨鍦ㄧ殑閲囬泦缁堢鏈嶅姟銆?
### 鏍稿績鍔熻兘

- 璋冪敤閲囬泦缁堢�?HTTP API锛堜汉鑴告姄鎷嶃€佹寚绾规姄鎷嶃€丱CR銆侀瑙堬級
- 鎺ユ敹閲囬泦缁堢寮傛 HTTP 鍥炶�?- 瑙ｆ瀽鍥炶皟 JSON銆丅ase64 瑙ｇ爜骞朵繚瀛樺浘鐗?- 閫氳繃缁熶竴浜嬩欢鍥炶皟閫氱煡绗笁鏂瑰鐞嗙粨�?- 浣跨�?libVLC 鎷夊�?RTSP 娴佸苟娓叉煋鍒扮涓夋柟浼犲叆鐨?HWND
- 鏀寔鍙岀粓绔垏鎹紙涓€鍙扮數鑴戣繛鎺ヤ袱涓粓绔級
- 鑷姩璇嗗埆 192.168.x.x 灞€鍩熺綉缃戞

### HZCYKJTHardWare.DLL 涓嶈礋璐?
- 涓嶇洿鎺ユ帶鍒舵憚鍍忓ご銆佹寚绾逛华銆丱CR 闃呰鏈?SDK
- 涓嶅惎鍔ㄤ换浣曟柊鐨?EXE 鏈嶅姟杩涚▼
- 涓嶅惎鍔?CollectorTerminal.exe
- 涓嶈礋璐ｇ鐞嗛噰闆嗙粓绔湇鍔＄殑鐢熷懡鍛ㄦ湡

## 2. 姝ｅ紡浜や粯�?
| 鏂囦�?| 璇存�?|
|------|------|
| **HZCYKJTHardWare.DLL** | 鍔ㄦ€佸簱锛堟敮�?x86 �?x64�?|
| **HZCYKJTHardWare.json** | 閰嶇疆鏂囦欢锛屽繀椤绘斁鍦?DLL 鍚岀洰褰?|

### 鍙€変緷璧?
- **libVLC** �?濡傞�?RTSP 棰勮锛岄渶閮ㄧ讲瀵瑰簲浣嶆暟�?libvlc.dll �?libvlccore.dll
- **plugins/** �?libVLC 鎻掍欢鐩綍锛堥€氬父涓?libvlc.dll 鍚岀洰褰曪級

## 3. 缂栬瘧瑕佹眰

- Visual Studio 2026 (v145 宸ュ叿闆?
- 鏀寔 Win32/x86 �?x64 鍥涚閰嶇疆
- 杩愯搴? /MD (Release) / /MDd (Debug)
- C++20 鏍囧�?
## 4. HZCYKJTHardWare.json 閰嶇疆璇存槑

HZCYKJTHardWare.json 蹇呴』鏀惧湪 HZCYKJTHardWare.DLL 鍚岀洰褰曘€傚鏋滄枃浠朵笉瀛樺湪锛孌LL 浣跨敤鍐呯疆榛樿閰嶇疆锛堟棩蹇椾腑浼氭彁绀猴級�?
### terminal.mode 鏀寔涓夌妯″紡

#### a. auto_subnet锛堥粯璁わ級

DLL 鑷姩璇嗗埆鏈満 192.168.x.x 缃戞锛屾牴�?host_suffix 鎷兼帴缁堢 IP�?
```json
{
  "terminal": {
    "mode": "auto_subnet",
    "port": 8080,
    "auto_subnet_devices": [
      { "index": 1, "host_suffix": 10 },
      { "index": 2, "host_suffix": 11 }
    ]
  }
}
```

渚嬪鏈満 IP �?192.168.1.1锛屽垯锛?- 缁堢�?1: http://192.168.1.10:8080
- 缁堢�?2: http://192.168.1.11:8080

#### b. fixed_url

鐩存帴閰嶇疆姣忎釜缁堢鐨勫畬鏁?URL�?
```json
{
  "terminal": {
    "mode": "fixed_url",
    "fixed_terminals": [
      { "index": 1, "base_url": "http://192.168.1.10:8080" },
      { "index": 2, "base_url": "http://192.168.1.11:8080" }
    ]
  }
}
```

#### c. manual

DLL 鍒濆鍖栨椂涓嶈嚜鍔ㄩ€夋嫨缁堢锛岀敱绗笁鏂硅皟鐢?`HZCYKJTZHardWare_SwitchTerminalByUrl()` �?`HZCYKJTZHardWare_SetTerminalBaseUrl()` 鎵嬪姩鎸囧畾�?
### 鍙岀綉鍗￠厤�?
濡傛灉鐢佃剳鏈夊涓?192.168 缃戞锛屽繀椤婚厤缃?`preferred_subnet_prefix` 鎸囧畾浣跨敤鍝釜缃戞�?
```json
{
  "terminal": {
    "preferred_subnet_prefix": "192.168.1"
  }
}
```

涓嶉厤缃笖瀛樺湪澶氫釜 192.168 缃戞鏃讹紝`HZCYKJTZHardWare_InitSdk` 杩斿�?`CDZD_RET_MULTI_NIC_NEED_CONFIG(-27)`�?
鍙€氳�?`HZCYKJTZHardWare_GetDetectedNetworkInfo()` 鏌ョ湅鎵€鏈夊€欓€夌綉鍗°€?
## 5. API 蹇€熷弬鑰?
### 鍒濆鍖?
```c
HZCYKJTZHardWare_InitSdk();         // 鍒濆鍖?DLL
HZCYKJTZHardWare_ReleaseSdk();     // 閲婃斁璧勬簮
HZCYKJTZHardWare_RegisterEventCallback(myCallback, userData);  // 娉ㄥ唽浜嬩欢鍥炶�?```

### 缁堢绠＄悊

```c
HZCYKJTZHardWare_SwitchTerminal(1);                      // 鍒囨崲鍒扮粓�?1
HZCYKJTZHardWare_SwitchTerminalByUrl("http://...");      // 鎵嬪姩鎸囧畾缁堢�?URL
HZCYKJTZHardWare_CheckTerminalStatus();                  // 妫€娴嬬粓绔湪绾跨姸鎬?HZCYKJTZHardWare_GetDetectedNetworkInfo(buf, size);      // 鑾峰彇缃戠粶妫€娴嬩俊鎭?JSON
```

### 娴佺▼鎺у�?
```c
HZCYKJTZHardWare_StartProcess();     // 娴佺▼寮€濮?(POST /process/start)
HZCYKJTZHardWare_EndProcess();       // 娴佺▼缁撴潫 (POST /process/end) + 娓呯�?pending 璇锋�?```

### 棰勮�?
```c
HZCYKJTZHardWare_StartCameraPreview(hwnd);       // 鍚姩鎽勫儚�?RTSP 棰勮�?HZCYKJTZHardWare_StopCameraPreview();            // 鍋滄鎽勫儚澶撮瑙?HZCYKJTZHardWare_StartFingerprintPreview(hwnd);  // 鍚姩鎸囩汗 RTSP 棰勮�?HZCYKJTZHardWare_StopFingerprintPreview();       // 鍋滄鎸囩汗棰勮�?```

### 鎶撴媿涓?OCR锛堝紓姝ワ級

```c
HZCYKJTZHardWare_CaptureCameraImage(saveDir);      // 浜鸿劯鎶撴媿 (POST /resources/face-image/request)
HZCYKJTZHardWare_CaptureFingerprintImage(saveDir); // 鎸囩汗鎶撴媿 (POST /resources/fingerprint/request)
HZCYKJTZHardWare_RequestOCR(saveDir);              // OCR  (POST /resources/ocr-document/request)
```

鎵€鏈夋姄鎷?OCR 鎺ュ彛涓哄紓姝ヨ皟鐢紝鏈€缁堢粨鏋滈€氳繃浜嬩欢鍥炶皟杩斿洖銆?
## 6. 淇濆瓨璺緞浼樺厛绾?
1. 鎶撴�?OCR 鎺ュ彛鏈浼犲叆鐨?`saveDir`
2. HZCYKJTHardWare.json �?`save.default_root`
3. `HZCYKJTZHardWare_SetSavePath()` 璁剧疆鐨勮繍琛屾椂璺緞
4. HZCYKJTHardWare.DLL 鍚岀洰褰曠殑 `captures\` 瀛愮洰褰?
## 7. 浜嬩欢鍥炶皟

绗笁鏂规敞鍐屽洖璋冨嚱鏁板悗锛孌LL 鍦ㄥ唴閮?worker 绾跨▼涓皟鐢ㄨ鍥炶皟�?
```c
typedef void (__stdcall *TCDZDEventCallback)(
    const CDZD_EVENT* eventData,
    void* userData
);
```

浜嬩欢绫诲瀷鍖呮嫭�?- 1001-1003: 缁堢鐘舵€?- 1101-1102: 娴佺▼鎺у�?- 1201-1203: 鎽勫儚澶撮�?- 1301-1303: 鎸囩汗棰勮
- 1401-1402: 浜鸿劯鎶撴媿
- 1501-1502: 鎸囩汗鎶撴媿
- 1601-1602: OCR
- 1701: 璇锋眰瓒呮椂
- 1999: 閫氱敤閿欒

## 8. libVLC 渚濊禆璇存槑

RTSP 棰勮闇€瑕?libVLC 杩愯搴撱€傞儴缃茶姹傦細

- x86 DLL �?闇€�?32 �?libvlc.dll / libvlccore.dll / plugins\
- x64 DLL �?闇€�?64 �?libvlc.dll / libvlccore.dll / plugins\

濡備笉闇€瑕侀瑙堝姛鑳斤紝鍙互涓嶉儴缃?libVLC 渚濊禆銆傝皟鐢ㄩ瑙堟帴鍙ｆ椂浼氳繑�?CDZD_RET_VLC_INIT_FAILED�?
## 9. x86/x64 娉ㄦ剰浜嬮�?
- **32 浣嶈繘绋嬪彧鑳藉姞杞?x86/HZCYKJTHardWare.DLL**
- **64 浣嶈繘绋嬪彧鑳藉姞杞?x64/HZCYKJTHardWare.DLL**
- DLL 浣嶆暟蹇呴』涓庤皟鐢ㄨ繘绋嬩綅鏁颁竴鑷?- HWND/鎸囬�?鍙ユ焺浣跨敤 void* 绫诲瀷锛屽吋�?32/64 �?
## 10. 绗笁鏂硅皟鐢ㄧず渚?
### C# P/Invoke

```csharp
[DllImport("HZCYKJTHardWare.DLL", CallingConvention = CallingConvention.StdCall)]
public static extern int HZCYKJTZHardWare_InitSdk();

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate void EventCallback(ref CDZD_EVENT evt, IntPtr userData);

[DllImport("HZCYKJTHardWare.DLL", CallingConvention = CallingConvention.StdCall)]
public static extern int HZCYKJTZHardWare_RegisterEventCallback(EventCallback cb, IntPtr userData);
```

### Python ctypes

```python
import ctypes

dll = ctypes.WinDLL("HZCYKJTHardWare.DLL")
dll.HZCYKJTZHardWare_InitSdk.restype = ctypes.c_int
dll.HZCYKJTZHardWare_InitSdk()

# 瀹氫箟鍥炶皟绫诲�?CALLBACK = ctypes.WINFUNCTYPE(None, ctypes.POINTER(CDZD_EVENT), ctypes.c_void_p)
dll.HZCYKJTZHardWare_RegisterEventCallback(CALLBACK(my_handler), None)
```

### Delphi

```delphi
function HZCYKJTZHardWare_InitSdk: Integer; stdcall; external 'HZCYKJTHardWare.DLL';
function HZCYKJTZHardWare_SwitchTerminal(index: Integer): Integer; stdcall; external 'HZCYKJTHardWare.DLL';
```

## 11. 甯歌閿欒�?
| 閿欒鐮?| 鍚�?|
|--------|------|
| 1 | 鎴愬�?|
| -2 | 鏈垵濮嬪寲 |
| -5 | 缁堢涓嶅彲�?|
| -6 | HTTP 璇锋眰澶辫触 |
| -8 | 鏃犳�?HWND |
| -9 | 棰勮宸插湪杩愯�?|
| -21 | libVLC 鍒濆鍖栧け�?|
| -23 | 鏈€夋嫨缁堢 |
| -25 | 缃戞妫€娴嬪け�?|
| -27 | 澶氱綉鍗￠渶閰嶇�?preferred_subnet_prefix |
| -29 | HZCYKJTHardWare.json 鏍煎紡閿欒 |

## 12. Windows 7 32 浣嶅吋�?
- 鐩爣鍏煎 Windows 7 32 浣嶉渶棰濆娉ㄦ�?TLS 鏀寔锛堟湰鍦拌仈璋冩帹鑽愪娇�?http://�?- libVLC 闇€瑕侀€夋嫨鏀寔 Windows 7 鐨勭増鏈?- 杩愯搴撻渶浣跨�?/MD 骞剁‘淇濈洰鏍囨満鍣ㄥ畨瑁呬簡 VC Redist

## 13. 鏃ュ�?
鏃ュ織榛樿鍐欏�?`logs/CDZD_yyyyMMdd.log`锛堢浉瀵逛簬 DLL 鐩綍锛夈€?
鍏抽敭鏃ュ織鍐呭锛?- 鎵€鏈夊鍑哄嚱鏁拌皟鐢?- HTTP 璇锋眰涓庡搷�?- 鍥炶皟鎺ユ敹涓庡鐞?- 鍥剧墖淇濆瓨璺�?- 缃戝崱鏋氫妇缁撴�?- 缁堢鍒囨崲浜嬩�?- HZCYKJTHardWare.json 鍔犺浇缁撴灉

## 14. 閮ㄧ讲娓呭崟

| 蹇呴�?鍙�?| 鏂囦�?| 璇存�?|
|-----------|------|------|
| 蹇呴�?| HZCYKJTHardWare.DLL | 鍔ㄦ€佸簱鏈綋 |
| 蹇呴�?| HZCYKJTHardWare.json | 閰嶇疆鏂囦欢 |
| 鍙�?| libvlc.dll | RTSP 棰勮渚濊禆锛堥渶鍖归厤浣嶆暟锛?|
| 鍙�?| libvlccore.dll | RTSP 棰勮渚濊禆 |
| 鍙�?| plugins/ | libVLC 鎻掍欢鐩綍 |
| 涓嶉渶瑕?| 浠讳�?EXE | 涓嶉渶瑕侀澶栭儴缃蹭笟鍔℃湇鍔＄▼搴?|

## 15. 娉ㄦ剰浜嬮�?
1. 璋冪�?HZCYKJTHardWare.DLL 鍓嶏紝閲囬泦缁堢�?HTTP 鏈嶅姟蹇呴』宸插惎�?2. callback_url 涓嶈兘浣跨敤 127.0.0.1锛孌LL 鑷姩浣跨敤鏈満 192.168.x.x 鍦板�?3. HZCYKJTHardWare.json 鏍煎紡閿欒浼氬鑷?HZCYKJTZHardWare_InitSdk 杩斿�?-29
4. HZCYKJTHardWare.json 涓嶅瓨鍦ㄦ椂 DLL 浣跨敤榛樿閰嶇疆缁х画杩愯
5. 鍙岀綉鍗＄幆澧冨繀椤婚厤缃?preferred_subnet_prefix
6. 涓嶇敓鎴?Mock 妯″紡 �?鎵€鏈夊姛鑳戒笌鐪熷疄缁堢鑱旇�?
