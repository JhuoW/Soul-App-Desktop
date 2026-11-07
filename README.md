# Soul 官方网页启动助手

研究与构建日期：2026-11-07（北京时间）。这是个人使用的第三方启动助手，不是 Soul 官方软件；聊天服务和页面均由 Soul 官方提供。

## 下载并使用

仓库包含预编译的 `SoulWebLauncher.exe`。推荐从 [最新 Release](https://github.com/JhuoW/Soul-App-Desktop/releases/latest) 下载 EXE 和 `SHA256SUMS.txt`，也可以下载或克隆整个仓库后直接运行根目录的 EXE。

- [下载最新版 EXE](https://github.com/JhuoW/Soul-App-Desktop/releases/latest/download/SoulWebLauncher.exe)
- [下载校验文件](https://github.com/JhuoW/Soul-App-Desktop/releases/latest/download/SHA256SUMS.txt)

需要自行编译时，在 Windows PowerShell 中进入仓库目录并运行：

```powershell
.\src\build.ps1
.\SoulWebLauncher.exe
```

构建成功后会在仓库根目录生成 `SoulWebLauncher.exe`。已有本地编译文件时，可直接按下面步骤使用。

1. 双击 `SoulWebLauncher.exe`。
2. 点击“打开独立聊天窗口”。程序优先使用电脑中已安装的 Microsoft Edge，其次使用 Google Chrome。
3. 在 Soul 官方页面自行阅读协议并完成扫码或手机号登录。是否能登录、是否需要额外验证及可用功能，以官方页面为准。
4. 如独立窗口不可用，点击“用默认浏览器打开”，或直接访问 **https://home.soulapp.cn/**。

无需安装本助手，不要求管理员权限，不需要 Android 模拟器。适用于有 .NET Framework 4.x 和受支持现代浏览器的 Windows 10/11；本次仅在当前 Windows 11 电脑验证。可把整个文件夹保留在任意自己的目录。退出助手不会关闭聊天窗口。

如果只需要电脑聊天，直接使用官方网页已经足够。也可在 Edge 中打开网页，选择“设置及其他 → 更多工具 → 应用 → 将此站点安装为应用”（菜单名称可能随版本不同）。这样由 Edge 自己管理桌面/任务栏快捷方式。[微软操作说明](https://support.microsoft.com/en-us/edge/install-manage-or-uninstall-apps-in-microsoft-edge)

## 研究结论

**Soul 已有官方网页版，官方网页的 Windows 窗口化是当前最合适的路线。无需重新实现登录、消息协议或加密。**

| 问题 | 结论与证据边界 |
| --- | --- |
| 是否支持官方电脑聊天？ | 支持。Soul 官网发布页明确指向 `home.soulapp.cn`，介绍 Windows、Mac 使用和手机消息同步。[官方公告](https://www.soulapp.cn/media/news/article-32) |
| 登录入口是否实际可见？ | 本次打开官方入口后进入 `https://home.soulapp.cn/login`，页面显示“扫码登录”和“手机号”选项。未勾选协议、未输入账号或验证码、未扫码登录。 |
| 是否有原生 Windows 客户端？ | 本次未核实独立原生 Windows 安装包的发布者与技术形态；官方网页可用不等于存在原生安装包。无需依赖这一点实现电脑聊天。 |
| 是否有授权第三方聊天 API/SDK？ | 本次在官方公开资料中未找到面向第三方聊天客户端的公开接口文档与授权流程。这不是证明接口绝不存在；在获得官方明确许可前，不据此开发协议客户端。 |
| 能否做可运行 MVP？ | 可以。交付的是固定官方地址的窗口启动器；页面、登录和聊天由 Soul 与已安装浏览器处理。 |

官网公告没有显示完整发布日期，因此本报告不推断其最初上线年份。调查结果为本次访问时的状态，后续功能和协议可能变化。

## hongguo-player 实现方式调查

调查版本：`main` 的提交 `a4688b02e57c11c159b15d4ce369f53f98299b2d`。该仓库公开的是安装包、说明与反馈资料，README 明确不包含源代码。当前文件树未发现依赖清单、核心登录/播放器源码或 LICENSE。因此无法进行源码级实现审计，也没有依据把它直接认定为 Electron 项目。

根据作者公开 README 和发布说明，可还原出以下结构，均属于作者声明而非本次运行验证：

| 层次 | 公开证据所支持的判断 |
| --- | --- |
| 界面与运行时 | 发布说明提到 Go 应用层、TypeScript 检查和前端构建；Windows 说明涉及 WebView2。无法确认具体 UI 框架。 |
| 登录 | 支持手机号登录；v1.2.14 新增红果 App 扫码登录。作者说明登录状态保存在本机。 |
| 平台交互 | 文档提到首次注册红果设备，以及随平台变化更新获取播放信息的处理方式；是否为公开授权接口无法核实。 |
| 播放 | 文档提到本机 `127.0.0.1` 视频服务、WebView2 解码及仅客户端可播放的加密下载。 |

v1.2.9 的发布说明还涉及绕开风控，v1.2.19 涉及完整签名请求。这些做法不符合本次“不绕过登录、风控、验证码或加密”的要求，不予采用。公开 GitHub 仓库也不等于平台接口授权。

可借鉴的是独立窗口等桌面交互思路。无法直接迁移的是红果平台的身份、设备、内容接口；也没有公开核心源码可供合法直接复用。本次未下载或运行红果安装包，未进行逆向工程。

证据：

- [固定版本 README](https://github.com/yangbin1322/hongguo-player/blob/a4688b02e57c11c159b15d4ce369f53f98299b2d/README.md)
- [固定版本完整文件树](https://api.github.com/repos/yangbin1322/hongguo-player/git/trees/a4688b02e57c11c159b15d4ce369f53f98299b2d?recursive=1)
- [Go / TypeScript 发布说明](https://github.com/yangbin1322/hongguo-player/releases/tag/beta-2026-11-07-ui)
- [扫码登录发布说明](https://github.com/yangbin1322/hongguo-player/releases/tag/v1.2.14)
- [v1.2.9 发布说明](https://github.com/yangbin1322/hongguo-player/releases/tag/v1.2.9)
- [v1.2.19 发布说明](https://github.com/yangbin1322/hongguo-player/releases/tag/v1.2.19)

## 授权边界与路线选择

[Soul 用户协议](https://themis.soulapp.cn/m/GFYf-6-DTTWO0x7OoErgbg.html)页面显示更新及生效日期为 2025-12-30。第 1.4 条涉及官方或授权渠道；第 3.2.11(14) 项限制反向工程及尝试取得源码；第 3.2.15 条允许非商业性使用，商业性使用涉及销售、复制、散发、预装、捆绑等时要求事先书面许可。本报告据此选择直接使用官方服务的入口，不将普通账号使用权解释为第三方接口开发授权，也不声称已获得 Soul 的商业合作或品牌许可。

| 路线 | 本次决定 |
| --- | --- |
| 官方网页 + Edge/Chrome 独立窗口 | 已实现。依靠官方登录与消息页面，无额外账号系统。 |
| 微软“手机连接”中的 Android 应用窗口 | 备选。需要支持的手机型号/地区、同一 Wi-Fi 及用户完成官方配对。应用仍运行在手机上。[微软说明](https://support.microsoft.com/en-us/windows/apps/mobileapps/use-apps-from-your-android-device-on-your-pc) |
| scrcpy USB 镜像官方 Android 应用 | 备选。用户授权 USB 调试后可在电脑操作手机；镜像工具会接触屏幕像素。中文输入需按手机输入法配置。没有在本机安装或启用此方案。[官方项目](https://github.com/Genymobile/scrcpy) |
| 模拟器 | 当前无必要，增加设备环境与兼容性变量；不处理任何模拟器识别或风控限制。 |
| 独立第三方聊天协议客户端 | 暂不实施。需先取得明确的官方接口文档、开发权限和授权范围。 |

如果将来采用 Android 备选，只使用官方原版应用和正常设备授权；若平台拒绝运行或控制，则停止，不尝试规避。

## 数据如何处理

```text
本助手 ──启动固定 URL──> 已安装的 Edge / Chrome ──官方 HTTPS 页面──> Soul
```

本助手没有账号输入框，不读 Cookie、浏览器存储、聊天数据库或剪贴板；不代理网络、不注入页面脚本、不自动发送消息、不设置远程调试端口，不修改证书检查或其他浏览器安全选项，也没有遥测和后台服务。

**浏览器和 Soul 官方页面仍会按自身机制处理并保存登录状态、网站数据和聊天信息。**本助手沿用所选浏览器的用户环境，并非独立隔离的浏览器资料目录。关闭窗口不等于退出账号；需要退出时请在官方页面退出，并按需通过浏览器自身界面管理站点数据。已有浏览器扩展或企业策略仍可能影响网页。

## 构建与文件

- `SoulWebLauncher.exe`：随仓库和 Release 提供的预编译程序，未做商业代码签名。
- `src/SoulWebLauncher.cs`：完整 C# 源码。
- `src/app.manifest`：普通用户权限及 DPI 声明。
- `src/build.ps1`：使用 Windows 自带 .NET Framework 编译器构建，无 NuGet 或网络下载。
- `VALIDATION.md`：本次验证结果和未测试范围。
- `VERSION`：发布版本；当前为 `1.0.0`。
- `SHA256SUMS.txt`：本次构建的 EXE 校验值。
- `RELEASE_NOTES.md`：当前版本的发布说明。
- `.github/workflows/release.yml`：在 `master` 分支更新 `VERSION` 时校验并发布对应 EXE；也支持手动运行。

在 PowerShell 中进入 `src` 目录，运行 `./build.ps1` 可重新构建。若系统策略不允许运行脚本，可直接用已安装的 C# 编译工具编译源码；本项目不修改脚本策略、安全设置或系统启动项。

发布新版本时，同步修改 `VERSION`、源码中的程序集版本和 `src/app.manifest` 版本，更新 `RELEASE_NOTES.md`，运行构建脚本，再把源码、EXE 和生成的校验文件一起提交到 `master`。GitHub Actions 会检查版本与校验值，创建草稿 Release，上传仓库中的同一份 EXE 和校验文件，校验附件后正式发布。已有版本内容不一致时流程会失败，不覆盖旧版附件；应使用新版本号。

当前编译器未启用确定性构建，因此重新编译的哈希可能不同。`SHA256SUMS.txt` 用于核对本次发布文件，不表示任意机器重建都会产生相同字节。

命令行 `SoulWebLauncher.exe --open` 可跳过助手界面，直接请求浏览器打开固定的官方地址。它不接受任意 URL 或浏览器参数。

如需移除，先关闭助手，再删除此文件夹即可。浏览器数据由浏览器管理，不随文件夹删除。本程序没有安装卸载服务，也不自动添加桌面快捷方式或开机启动。
