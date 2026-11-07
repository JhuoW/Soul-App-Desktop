# Soul 官方网页启动助手 v1.0.0

首个预编译 Windows 发布版。下载 `SoulWebLauncher.exe` 后双击运行，无需自行编译或安装本助手。

- 打开 Soul 官方网页的独立桌面窗口，优先使用已安装的 Edge，其次 Chrome。
- 提供默认浏览器入口和 Soul 官方发布说明入口。
- 登录、验证码和聊天均在官方网页完成；助手不收集账号凭证或聊天记录。
- 为 EXE 添加 1.0.0 版本信息，并提供 SHA-256 校验文件。

运行条件：Windows 10/11、.NET Framework 4.x、现代浏览器及网络连接。本助手是非官方工具，EXE 未做商业代码签名。

已验证本机编译、助手界面和启动 Edge 窗口；未验证登录后的消息收发。完整限制见仓库 `VALIDATION.md`。

Release 附件和仓库中的 EXE 来自同一次构建。下载后可用 PowerShell 的 `Get-FileHash .\SoulWebLauncher.exe -Algorithm SHA256` 与 `SHA256SUMS.txt` 核对。
