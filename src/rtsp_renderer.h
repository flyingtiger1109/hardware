#pragma once
#include "pch.h"

namespace HZCYKJTHardWare {

// RTSP 渲染器抽象接口
class IRtspRenderer {
public:
    virtual ~IRtspRenderer() = default;

    // 开始渲染 RTSP 流到指定 HWND
    virtual int Start(const std::string& url, HWND hwnd) = 0;

    // 停止渲染
    virtual int Stop() = 0;

    // 是否正在运行
    virtual bool IsRunning() const = 0;

    // 最近一次失败原因，供调用层透传给回调事件
    virtual std::string LastErrorMessage() const { return ""; }
};

// 工厂函数：创建 libVLC RTSP 渲染器
std::unique_ptr<IRtspRenderer> CreateLibVlcRtspRenderer();

} // namespace HZCYKJTHardWare
