// HZCYKJTHardWare.DLL 测试工具 — 模拟第三方调用
#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include <cstring>

// ============================================================
// 错误码 & 事件类型（与 cdzd_types.h 同步）
// ============================================================
#define HZCYKJTHardWare_RET_OK                      1
#define HZCYKJTHardWare_RET_FAILED                 -1
#define HZCYKJTHardWare_RET_NOT_INITIALIZED        -2
#define HZCYKJTHardWare_RET_INVALID_PARAM          -3
#define HZCYKJTHardWare_RET_TERMINAL_UNREACHABLE   -5
#define HZCYKJTHardWare_RET_HTTP_FAILED            -6
#define HZCYKJTHardWare_RET_CALLBACK_SERVER_FAILED -11
#define HZCYKJTHardWare_RET_PARSE_JSON_FAILED      -12
#define HZCYKJTHardWare_RET_BASE64_FAILED          -13
#define HZCYKJTHardWare_RET_SAVE_FILE_FAILED       -14
#define HZCYKJTHardWare_RET_TERMINAL_NOT_SELECTED  -23
#define HZCYKJTHardWare_RET_TERMINAL_INDEX_INVALID -24
#define HZCYKJTHardWare_RET_SUBNET_DETECT_FAILED  -25
#define HZCYKJTHardWare_RET_TERMINAL_SWITCH_FAILED -26
#define HZCYKJTHardWare_RET_MULTI_NIC_NEED_CONFIG -27
#define HZCYKJTHardWare_RET_CONFIG_NOT_FOUND      -28
#define HZCYKJTHardWare_RET_CONFIG_INVALID        -29
#define HZCYKJTHardWare_RET_VLC_INIT_FAILED       -21

#define HZCYKJTHardWare_EVENT_TERMINAL_ONLINE      1001
#define HZCYKJTHardWare_EVENT_TERMINAL_OFFLINE     1002
#define HZCYKJTHardWare_EVENT_TERMINAL_SWITCHED    1003
#define HZCYKJTHardWare_EVENT_PROCESS_STARTED      1101
#define HZCYKJTHardWare_EVENT_PROCESS_ENDED        1102
#define HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED 1201
#define HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STOPPED 1202
#define HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED  1203
#define HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STARTED 1301
#define HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STOPPED 1302
#define HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_FAILED  1303
#define HZCYKJTHardWare_EVENT_FACE_CAPTURE_SUCCESS  1401
#define HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED   1402
#define HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_SUCCESS 1501
#define HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED  1502
#define HZCYKJTHardWare_EVENT_OCR_SUCCESS           1601
#define HZCYKJTHardWare_EVENT_OCR_FAILED            1602
#define HZCYKJTHardWare_EVENT_REQUEST_TIMEOUT       1701
#define HZCYKJTHardWare_EVENT_ERROR                 1999

#pragma pack(push, 1)
typedef struct {
    int struct_size;
    int event_type;
    const char* request_id;
    const char* resource_type;
    int status;
    const char* error_code;
    const char* message;
    const char* terminal_base_url;
    int terminal_index;
    const char* save_path;
    const char* raw_json;
    const void* data;
    int data_size;
} HZCYKJTHardWare_EVENT;
#pragma pack(pop)

typedef void (__stdcall *THZCYKJTHardWareEventCallback)(const HZCYKJTHardWare_EVENT* event, void* userData);

// 函数指针
typedef int (__stdcall *PFN_InitSdk)(void);
typedef int (__stdcall *PFN_ReleaseSdk)(void);
typedef int (__stdcall *PFN_SwitchTerminal)(int);
typedef int (__stdcall *PFN_StartProcess)(void);
typedef int (__stdcall *PFN_EndProcess)(void);
typedef int (__stdcall *PFN_StartCameraPreview)(void*);
typedef int (__stdcall *PFN_StopCameraPreview)(void);
typedef int (__stdcall *PFN_StartFingerprintPreview)(void*);
typedef int (__stdcall *PFN_StopFingerprintPreview)(void);
typedef int (__stdcall *PFN_CaptureCameraImage)(const char*);
typedef int (__stdcall *PFN_CaptureFingerprintImage)(const char*);
typedef int (__stdcall *PFN_RequestOCR)(const char*);
typedef int (__stdcall *PFN_StartPlatePreview)(void*);
typedef int (__stdcall *PFN_StopPlatePreview)(void);
typedef int (__stdcall *PFN_RegisterEventCallback)(THZCYKJTHardWareEventCallback, void*);

// ============================================================
// 全局
// ============================================================
HMODULE g_hDll = nullptr;

struct {
    PFN_InitSdk                 InitSdk;
    PFN_ReleaseSdk              ReleaseSdk;
    PFN_SwitchTerminal          SwitchTerminal;
    PFN_StartProcess            StartProcess;
    PFN_EndProcess              EndProcess;
    PFN_StartCameraPreview      StartCameraPreview;
    PFN_StopCameraPreview       StopCameraPreview;
    PFN_StartFingerprintPreview  StartFingerprintPreview;
    PFN_StopFingerprintPreview   StopFingerprintPreview;
    PFN_CaptureCameraImage      CaptureCameraImage;
    PFN_CaptureFingerprintImage  CaptureFingerprintImage;
    PFN_RequestOCR              RequestOCR;
    PFN_StartPlatePreview       StartPlatePreview;
    PFN_StopPlatePreview        StopPlatePreview;
    PFN_RegisterEventCallback   RegisterEventCallback;
} g;

int g_totalEvents   = 0;
int g_successEvents = 0;
int g_failEvents    = 0;

// ============================================================
// 辅助函数
// ============================================================
const char* RetCodeName(int code) {
    return code == 1 ? "OK" : "FAIL";
}

const char* EventName(int type) {
    switch (type) {
        case HZCYKJTHardWare_EVENT_TERMINAL_ONLINE:              return "TERMINAL_ONLINE";
        case HZCYKJTHardWare_EVENT_TERMINAL_OFFLINE:             return "TERMINAL_OFFLINE";
        case HZCYKJTHardWare_EVENT_TERMINAL_SWITCHED:            return "TERMINAL_SWITCHED";
        case HZCYKJTHardWare_EVENT_PROCESS_STARTED:              return "PROCESS_STARTED";
        case HZCYKJTHardWare_EVENT_PROCESS_ENDED:                return "PROCESS_ENDED";
        case HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STARTED:       return "CAMERA_PREVIEW_STARTED";
        case HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_STOPPED:       return "CAMERA_PREVIEW_STOPPED";
        case HZCYKJTHardWare_EVENT_CAMERA_PREVIEW_FAILED:        return "CAMERA_PREVIEW_FAILED";
        case HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STARTED:  return "FINGERPRINT_PREVIEW_STARTED";
        case HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_STOPPED:  return "FINGERPRINT_PREVIEW_STOPPED";
        case HZCYKJTHardWare_EVENT_FINGERPRINT_PREVIEW_FAILED:   return "FINGERPRINT_PREVIEW_FAILED";
        case HZCYKJTHardWare_EVENT_FACE_CAPTURE_SUCCESS:         return "FACE_CAPTURE_SUCCESS";
        case HZCYKJTHardWare_EVENT_FACE_CAPTURE_FAILED:          return "FACE_CAPTURE_FAILED";
        case HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_SUCCESS:  return "FINGERPRINT_CAPTURE_SUCCESS";
        case HZCYKJTHardWare_EVENT_FINGERPRINT_CAPTURE_FAILED:   return "FINGERPRINT_CAPTURE_FAILED";
        case HZCYKJTHardWare_EVENT_OCR_SUCCESS:                  return "OCR_SUCCESS";
        case HZCYKJTHardWare_EVENT_OCR_FAILED:                   return "OCR_FAILED";
        case HZCYKJTHardWare_EVENT_REQUEST_TIMEOUT:              return "REQUEST_TIMEOUT";
        case HZCYKJTHardWare_EVENT_ERROR:                        return "ERROR";
        default: return "UNKNOWN";
    }
}

void PrintSeparator(const char* title) {
    printf("\n============================================================\n");
    printf("  %s\n", title);
    printf("============================================================\n");
}

bool IsSuccessCode(int code) {
    return code == 1;
}

// ============================================================
// 事件回调
// ============================================================
void __stdcall OnEvent(const HZCYKJTHardWare_EVENT* e, void* userData) {
    (void)userData;
    g_totalEvents++;

    bool isOk = (e->status == HZCYKJTHardWare_RET_OK);
    if (isOk) g_successEvents++; else g_failEvents++;

    printf("\n  [EVENT #%d] %s %s\n", g_totalEvents,
           isOk ? "OK" : "FAIL", EventName(e->event_type));
    printf("    status=%d", e->status);
    if (e->request_id && e->request_id[0])
        printf("  request_id=%s", e->request_id);
    if (e->resource_type && e->resource_type[0])
        printf("  resource=%s", e->resource_type);
    if (e->terminal_index)
        printf("  terminal_index=%d", e->terminal_index);
    if (e->save_path && e->save_path[0])
        printf("  save=%s", e->save_path);
    if (e->error_code && e->error_code[0])
        printf("  error=%s", e->error_code);
    if (e->message && e->message[0])
        printf("  msg=%s", e->message);
    printf("\n");
}

// ============================================================
// DLL 加载
// ============================================================
bool LoadDll() {
    g_hDll = LoadLibraryA("HZCYKJTHardWare.dll");
    if (!g_hDll) {
        printf("[FATAL] 无法加载 HZCYKJTHardWare.dll (err=%lu)\n", GetLastError());
        return false;
    }

#define LOAD(fn) \
    g.##fn = (PFN_##fn)GetProcAddress(g_hDll, "HZCYKJTHardWare_" #fn); \
    if (!g.##fn) { printf("[FATAL] 找不到: HZCYKJTHardWare_" #fn "\n"); return false; }

    LOAD(InitSdk);
    LOAD(ReleaseSdk);
    LOAD(SwitchTerminal);
    LOAD(StartProcess);
    LOAD(EndProcess);
    LOAD(StartCameraPreview);
    LOAD(StopCameraPreview);
    LOAD(StartFingerprintPreview);
    LOAD(StopFingerprintPreview);
    LOAD(CaptureCameraImage);
    LOAD(CaptureFingerprintImage);
    LOAD(RequestOCR);
    LOAD(StartPlatePreview);
    LOAD(StopPlatePreview);
    LOAD(RegisterEventCallback);
#undef LOAD

    printf("[OK] HZCYKJTHardWare.dll 加载成功，19 个函数全部就绪\n");
    return true;
}

void UnloadDll() {
    if (g_hDll) { FreeLibrary(g_hDll); g_hDll = nullptr; }
}

// ============================================================
// 等待事件
// ============================================================
void WaitForEvents(int timeoutMs) {
    printf("    等待回调 (最长 %d ms)...\n", timeoutMs);
    int step = 200, waited = 0, lastCount = g_totalEvents;
    while (waited < timeoutMs) {
        Sleep(step); waited += step;
        if (g_totalEvents > lastCount) { Sleep(500); break; }
    }
    printf("    收到 %d 个事件\n", g_totalEvents);
}

// ============================================================
// 测试函数
// ============================================================

void Test_InitSdk() {
    PrintSeparator("1. HZCYKJTHardWare_InitSdk");
    int ret = g.InitSdk();
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
    if (ret != 1)
        printf("  [FAIL] 初始化失败，请查看 DLL 日志排查原因\n");
}

void Test_RegisterCallback() {
    PrintSeparator("2. HZCYKJTHardWare_RegisterEventCallback");
    int ret = g.RegisterEventCallback(OnEvent, nullptr);
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
}

void Test_SwitchTerminal(int index) {
    PrintSeparator(index == 1 ? "3. 切换到终端 1" : "3b. 切换到终端 2");
    int ret = g.SwitchTerminal(index);
    printf("  HZCYKJTHardWare_SwitchTerminal(%d) = %d (%s)\n", index, ret, RetCodeName(ret));
    WaitForEvents(1000);
}

void Test_StartProcess() {
    PrintSeparator("4. HZCYKJTHardWare_StartProcess [同步]");
    int ret = g.StartProcess();
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
}

void Test_CaptureCameraImage() {
    PrintSeparator("5. HZCYKJTHardWare_CaptureCameraImage [同步] 人脸抓拍");
    int ret = g.CaptureCameraImage(nullptr);
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
    if (ret == HZCYKJTHardWare_RET_OK) printf("  同步抓拍成功, 返回值即最终结果\n");
}

void Test_CaptureFingerprintImage() {
    PrintSeparator("6. HZCYKJTHardWare_CaptureFingerprintImage [同步] 指纹抓拍");
    int ret = g.CaptureFingerprintImage(nullptr);
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
    if (ret == HZCYKJTHardWare_RET_OK) printf("  同步抓拍成功, 返回值即最终结果\n");
}

void Test_RequestOCR() {
    PrintSeparator("7. HZCYKJTHardWare_RequestOCR [异步]");
    int ret = g.RequestOCR(nullptr);
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
    if (ret == HZCYKJTHardWare_RET_OK) printf("  请求已提交, 等待终端异步回调...\n");
    WaitForEvents(22000);
}

void Test_EndProcess() {
    PrintSeparator("8. HZCYKJTHardWare_EndProcess [同步]");
    int ret = g.EndProcess();
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
}

void Test_Shutdown() {
    PrintSeparator("9. HZCYKJTHardWare_ReleaseSdk");
    int ret = g.ReleaseSdk();
    printf("  返回: %d (%s)\n", ret, RetCodeName(ret));
}

// ============================================================
// 自动测试
// ============================================================
void RunAutoTests() {
    printf("\n=========== HZCYKJTHardWare.DLL 自动化测试 ===========\n");

    Test_InitSdk();
    Test_RegisterCallback();
    Test_SwitchTerminal(1);
    Test_StartProcess();
    Test_CaptureCameraImage();
    Test_CaptureFingerprintImage();
    Test_RequestOCR();
    Test_EndProcess();

    Test_Shutdown();

    PrintSeparator("测试总结");
    printf("  总事件: %d\n", g_totalEvents);
    printf("  成功:   %d\n", g_successEvents);
    printf("  失败:   %d\n", g_failEvents);
    printf("============================================================\n");
}

// ============================================================
// 交互菜单
// ============================================================
void InteractiveMenu() {
    bool initialized = false;
    int choice;

    while (true) {
        printf("\n");
        printf("┌──────────────────────────────────────┐\n");
        printf("│      HZCYKJTHardWare.DLL 交互测试工具          │\n");
        printf("├──────────────────────────────────────┤\n");
        printf("│  1  初始化 DLL         2  注册回调  │\n");
        printf("│  3  终端 1             4  终端 2    │\n");
        printf("│  5  开始流程[同]       6  人脸抓拍[同]│\n");
        printf("│  7  指纹抓拍[同]       8  OCR请求[异]│\n");
        printf("│  9  结束流程[同]      10  释放 DLL  │\n");
        printf("│ 11  全自动测试                       │\n");
        printf("│  0  退出                            │\n");
        printf("└──────────────────────────────────────┘\n");
        printf("选择: ");

        if (scanf_s("%d", &choice) != 1) { while (getchar() != '\n'); continue; }
        while (getchar() != '\n');

        int ret;
        switch (choice) {
        case 0:
            if (initialized) g.ReleaseSdk();
            UnloadDll();
            return;

        case 1:
            ret = g.InitSdk();
            printf("InitSdk = %d (%s)\n", ret, RetCodeName(ret));
            initialized = IsSuccessCode(ret);
            break;

        case 2:
            ret = g.RegisterEventCallback(OnEvent, nullptr);
            printf("RegisterEventCallback = %d (%s)\n", ret, RetCodeName(ret));
            break;

        case 3:
            ret = g.SwitchTerminal(1);
            printf("SwitchTerminal(1) = %d (%s)\n", ret, RetCodeName(ret));
            WaitForEvents(500);
            break;

        case 4:
            ret = g.SwitchTerminal(2);
            printf("SwitchTerminal(2) = %d (%s)\n", ret, RetCodeName(ret));
            WaitForEvents(500);
            break;

        case 5:
            ret = g.StartProcess();
            printf("[同步] StartProcess = %d (%s)\n", ret, RetCodeName(ret));
            break;

        case 6:
            ret = g.CaptureCameraImage(nullptr);
            printf("[同步] CaptureCameraImage = %d (%s)\n", ret, RetCodeName(ret));
            break;

        case 7:
            ret = g.CaptureFingerprintImage(nullptr);
            printf("[同步] CaptureFingerprintImage = %d (%s)\n", ret, RetCodeName(ret));
            break;

        case 8:
            ret = g.RequestOCR(nullptr);
            printf("[异步] RequestOCR = %d (%s)\n", ret, RetCodeName(ret));
            if (ret == HZCYKJTHardWare_RET_OK) printf("  请求已提交, 等待终端异步回调...\n");
            WaitForEvents(22000);
            break;

        case 9:
            ret = g.EndProcess();
            printf("[同步] EndProcess = %d (%s)\n", ret, RetCodeName(ret));
            break;

        case 10:
            ret = g.ReleaseSdk();
            printf("ReleaseSdk = %d (%s)\n", ret, RetCodeName(ret));
            initialized = false;
            break;

        case 11:
            RunAutoTests();
            break;

        case 15:
            ret = g.StartPlatePreview((void*)GetConsoleWindow());
            printf("[预览] StartPlatePreview = %d (%s)\n", ret, RetCodeName(ret));
            break;

        case 16:
            ret = g.StopPlatePreview();
            printf("[预览] StopPlatePreview = %d (%s)\n", ret, RetCodeName(ret));
            break;

        default:
            printf("无效选项\n");
            break;
        }
    }
}

// ============================================================
// main
// ============================================================
int main(int argc, char* argv[]) {
    // 设置控制台为 UTF-8，避免中文乱码
    SetConsoleOutputCP(65001);
    SetConsoleCP(65001);

#ifdef _WIN64
    printf("HZCYKJTHardWare.DLL 测试工具 (x64)\n\n");
#else
    printf("HZCYKJTHardWare.DLL 测试工具 (x86)\n\n");
#endif

    if (!LoadDll()) {
        printf("按 Enter 退出..."); getchar();
        return 1;
    }

    if (argc >= 2 && strcmp(argv[1], "--auto") == 0) {
        RunAutoTests();
        UnloadDll();
        printf("\n按 Enter 退出..."); getchar();
        return 0;
    }

    InteractiveMenu();
    return 0;
}
