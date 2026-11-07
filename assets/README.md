# 图标资源

这是本项目原创的聊天气泡图形，不是 Soul 官方商标。所有图形均由仓库中的向量路径和 GDI+ 绘制代码生成，没有引用外部图片。

- `app-icon.svg`：可编辑向量源。
- `app-icon.ico`：16、20、24、32、40、48、64、128、256 像素的 32-bit RGBA 图标，供 Windows EXE、窗口和托盘选择合适尺寸。
- `app-icon.png`：512 × 512 透明背景高清图。
- `app-icon-{size}.png`：对应小尺寸预览文件。

在仓库根目录运行 `./tools/Generate-Icons.ps1` 可以重新生成。脚本只使用 Windows .NET Framework 编译器和 System.Drawing，无需网络下载。

应用需嵌入资源名 `SoulWebLauncher.AppIcon.ico` 和 `SoulWebLauncher.AppIcon.png`。`src/AppIcons.cs` 提供普通窗口图标、托盘图标、高清图和四帧托盘提示动画；每个返回的图像/图标由调用者负责释放。动画让带红点的图标轻微上下移动，始终保留可见图形，不使用整枚图标消失的闪烁。

`AppIcons.CreateTrayFrames(Image avatar)` 可用官方通知提供的头像生成圆形头像动画。传入图像只在调用期间读取，不保留引用、不负责释放；无效图像退回主图标。调用者可在方法返回后立即释放头像，并在替换/退出时逐个释放返回的 `Icon`。
