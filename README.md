# Soul 桌面窗口

在 Windows 独立窗口中使用 [Soul 官方网页版](https://home.soulapp.cn/)，支持关闭确认、托盘驻留和官方网页通知提醒。**本项目不是 Soul 官方客户端，与 Soul 无隶属关系。**登录、验证码、消息收发均由官方网页完成。

## 下载和运行

- [下载最新版 SoulWebLauncher.exe](https://github.com/JhuoW/Soul-App-Desktop/releases/latest/download/SoulWebLauncher.exe)
- [Release 与更新说明](https://github.com/JhuoW/Soul-App-Desktop/releases/latest)
- [SHA-256 校验文件](https://github.com/JhuoW/Soul-App-Desktop/releases/latest/download/SHA256SUMS.txt)

仓库根目录也包含相同版本的预编译 EXE。双击即可启动，无需安装本助手。要求 Windows 10/11、.NET Framework 4.7.2 或更新版本，以及 [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)。通常 Windows 11 已有运行时；缺少时应用会给出官方安装入口，不静默安装或修改安全设置。EXE 未做商业代码签名。

## v1.2.0 操作方式

- 窗口和网页区域随尺寸变化自动调整，窄窗口下精简顶部信息并收起次要操作，为聊天留出空间。
- 默认以 **100%** 显示，让官网随窗口宽高触发自身的响应式布局；也可以选择 **适配桌面宽度**，或手动放大、缩小、恢复 **100%**。调整窗口大小不会重新加载网页。
- 加载、重试和失败状态有明确反馈；保留页面时刷新不再先切回整页启动提示。

- 高清原创聊天图标：16–256px 多尺寸 ICO、512px PNG；托盘和高分辨率窗口使用相应图标。
- 点击窗口 **X** 时，可选择 **最小化到托盘 / 直接退出 / 取消**。普通最小化按钮仍保留在任务栏。
- 双击托盘图标恢复聊天窗口。右键菜单提供恢复、首页、刷新、提醒开关、停止本次提醒和退出等操作。
- 窗口留在托盘时，官方网页继续运行；彻底退出后无法继续接收通知。电脑休眠、断网或网站限制也可能影响消息。
- 重复启动会唤回现有窗口，避免打开多个本地会话。
- 主窗口提供官方地址展示和在系统浏览器打开的备用入口。外部浏览器窗口独立运行，不受本程序的关闭或托盘操作控制。

显示行为与边界详见 [窗口自适应说明](docs/DISPLAY.md)。官网原生布局与“适配桌面宽度”是两种显示选择；后者按 1080 个逻辑像素的参考宽度缩小页面，不保证触发官网的窄屏布局。

常用快捷键：

| 操作 | 快捷键 |
| --- | --- |
| 刷新网页 | F5 / Ctrl+R |
| 回到首页 | Alt+Home |
| 放大 / 缩小 | Ctrl++ / Ctrl+- |
| 恢复 100%（官网原生布局） | Ctrl+0 |

## 新消息提醒的真实范围

应用接收 WebView2 的 **官方网页通知事件**，不访问 Soul 的内部聊天协议。网页请求通知权限时，由你决定是否允许。

当官方页面确实发出受支持的通知时，托盘图标会带红点跳动；通知提供可用头像时显示该头像，否则使用应用图标。头像仅从 Soul 官方域的 HTTPS 图片地址按大小限制读取，不携带 Cookie、不重定向、不保存到磁盘。它可能是应用图标，**不保证是发信人头像**。

**不是每条聊天消息都必然触发提醒。**只有官网主动发出的非持久 Web Notification 可接入；如果官网没有发通知，或只使用 Web Push / Service Worker 持久通知，此版本不能补造提醒。未登录真实账号验证每条消息是否触发。通知标题或正文不会被本程序读取；官网的原生通知仍可能按它自身设置展示内容。

恢复窗口或选择“停止提醒”只清除本地托盘动画，**不等于把 Soul 会话标为已读**。提醒开关控制本地托盘动画，网站通知权限由正常授权流程管理。

## 登录状态与本地数据

自 v1.1.0 起使用独立 WebView2 页面。首次使用或从早期外部浏览器版本升级时，需要在官方页面登录，不导入旧浏览器的 Cookie 或密码。v1.2.0 沿用同一个本地网页环境，窗口缩放不会主动清除登录状态。

WebView2 自行管理的站点数据位于 `%LOCALAPPDATA%\SoulWebLauncher\WebView2`；经校验的微软加载组件位于同目录下的 `Runtime`。本程序不提取账号凭证、Cookie、聊天数据库，不注入脚本、不抓取消息、不自动发送消息，不绕过登录、验证码、风控、证书或加密。

窗口位置、大小、最大化状态和显示比例单独保存在 `%LOCALAPPDATA%\SoulWebLauncher\window-preferences.ini`，不含账号或聊天内容。无效配置会回到默认值，窗口会限制在可用显示器范围内；恢复显示默认值不清除官网登录状态。

关闭或隐藏窗口不等于退出账号。退出账号请使用官方网页提供的退出功能。卸载本助手可删除 EXE；本地站点数据不会随 EXE 删除而自动清除。

## 从源码构建

安装 .NET Framework 4.7.2 Developer Pack 后，在 Windows PowerShell 中执行：

```powershell
.\src\build.ps1
```

首次构建从官方 NuGet 源下载固定版本 `Microsoft.Web.WebView2 1.0.3124.44`，核对固定 SHA-256 后使用；缓存位于 `.packages`，不会提交。构建脚本把所需 SDK 组件和图标内嵌进单个 EXE，并生成 `SHA256SUMS.txt`。无需另外分发 SDK DLL；系统 WebView2 Runtime 仍需存在。

图标原始 SVG、各尺寸 PNG 和 ICO 在 `assets`；可运行 `tools/Generate-Icons.ps1` 重新生成。第三方组件许可见 [THIRD_PARTY_NOTICES.txt](THIRD_PARTY_NOTICES.txt)，程序也提供许可查看入口。

发布新版本时，同步更新 `VERSION`、`src/Bootstrap.cs` 的程序集版本、`src/app.manifest` 和 `RELEASE_NOTES.md`，重新构建并提交 EXE 与校验文件。更新 `master` 的 `VERSION` 会触发发布流程：校验版本、创建草稿、上传同一份 EXE、核对远端摘要后发布；不会覆盖旧版本附件。构建不保证跨机器确定性，应核对本次发布的校验文件。

最初的项目调研见 [docs/RESEARCH.md](docs/RESEARCH.md)，实际测试范围见 [VALIDATION.md](VALIDATION.md)。
