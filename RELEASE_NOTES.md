# Remy Asset Copilot 0.5.2 Beta

安装后，用户可正常打开 Rhino，在命令行输入 `AssetCopilot`（无空格）直接打开插件。

- 同时兼容 Rhino 的 .NET Framework、.NET 7 和 .NET 8 模式，解决 0.5.1 在 Framework 模式下初始化失败的问题。
- 无需专用启动器，无需 SetDotNetRuntime，不修改 Rhino 的全局运行时设置。
- 保持原有 UI、动画、材质预览、三种尺寸方式、输入及场景放置流程。
- 安装时关闭所有 Rhino；安装完成后正常启动 Rhino，输入 AssetCopilot。
- 首次安装推荐 Full；Standard 不含本地图片尺寸识别；Update 用于已有 0.5.0/0.5.1 通用安装。

要求 Windows 10/11 x64、Rhino 8.0 起的 8.x。使用 Rhino 8.0 SDK 构建；本机 Rhino 8.35 上的 Framework、.NET 7、.NET 8 各通过 161 项界面与场景检查，Framework 核心检查 134 项通过。Rhino 8.0 原版仍待实机验证。

在线生成需要自己的 Tripo API Key 和额度。安装包尚未代码签名。发布本版时请创建新标签 v0.5.2 并上传对应 0.5.2 EXE，不要继续分发旧版 0.5.1 作为默认安装包。
