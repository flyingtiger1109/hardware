#include "pch.h"
#include "hzsjkjt_context.h"
#include "path_helper.h"

BOOL APIENTRY DllMain(HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        // 保存 DLL 路径，用于初始化时查找 HZCYKJTHardWare.json
        {
            wchar_t path[MAX_PATH] = {0};
            GetModuleFileNameW(hModule, path, MAX_PATH);
            std::wstring ws(path);
            HZCYKJTHardWare::HzsjkjtContext::Instance().dll_dir = HZCYKJTHardWare::PathHelper::WideToUtf8(
                ws.substr(0, ws.find_last_of(L'\\')));
        }
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

