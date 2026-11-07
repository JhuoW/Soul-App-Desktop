# 窗口自适应与显示比例

## 默认：使用官网自身布局

网页控件始终占满工具栏、状态栏之间的可用区域。默认显示比例为 100%，改变窗口宽高时，网页得到新的可用视口，按官网自身的规则重新排布。调整大小不会重新加载页面，也不会注入 CSS、脚本或读取会话内容。

本次检查的 [官网公开样式](https://home.soulapp.cn/assets/index-2FU8YPGJ.css) 显示，登录页在 980px 等宽度断点下会切换布局，也针对较低的窗口高度调整内容。公开样式只能证明登录页的行为；登录后的聊天页面没有用真实账号验证，后续官网更新也可能改变布局。

## 可选：适配桌面宽度

这个模式按窗口可用宽度计算网页缩放，以 1080 个逻辑像素作为本工具的参考桌面宽度。1080 不是 Soul 官方的最小宽度要求。它适合希望在小窗口内保留较宽页面布局的情况，文字也会相应缩小。

缩放取值有上下限，并按绝对宽度重算，来回调整窗口不会累计缩小。手动放大、缩小或恢复 100% 会切回手动比例。需要官网原生窄窗口布局时，选择恢复 100%。

## 实现边界

使用微软 [WebView2 ZoomFactor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.zoomfactor) 调整显示比例。保留 WebView2 自身的 DPI 管理，不修改 [RasterizationScale](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.rasterizationscale)。窗口宽度换算使用应用当前 DPI；系统额外文本缩放、不同显示器组合仍可能产生差异。

本次没有启用新的跨显示器 DPI 模式，原应用的系统 DPI 感知不等同于 PerMonitorV2。未做所有显示器缩放组合的实机验证，相关条件见微软 [WinForms 高 DPI 文档](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms)。
