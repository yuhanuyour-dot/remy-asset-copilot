# Windows 安装包构建

运行安装包的用户不需要安装 Python、Node.js、.NET SDK 或 Inno Setup。需要 Windows x64 Rhino 8.0+（8.x）。8.0–8.11 使用 Rhino 随附的 .NET 7；8.12+ 支持 .NET 7/8，启动入口优先使用已安装的 .NET 8。缺少运行时请修复 Rhino 安装。

## 构建环境

- Windows x64、.NET 8 SDK。NuGet 自动获取 RhinoCommon 8.0.23304.9001 和 WebView2 1.0.1938.49；插件编译目标 net7.0-windows。无需本机 Rhino 开发引用。
- Python 3.9+，仅使用标准库；Inno Setup 6.7.3。
- 运行组件目录：从已安装的 Full 版本取 `runtime/`，通过 `-RuntimeSource` 指定。大型组件不提交到 Git。
- 本次使用 Node 24.19.0、Transformers.js 3.8.1、ONNX Runtime 1.21.0。模型和依赖锁定信息见 `runtime-manifests/`。

```powershell
# 在仓库根目录执行；按本机环境替换尖括号中的路径。
.\installer\Build-Installer.ps1 -Iscc "<Inno Setup 目录>\ISCC.exe" -RuntimeSource "<已安装完整版目录>\runtime"
```

Python 命令不叫 python 时增加 `-Python "<python.exe>"`。

流程：编译插件 → 在 work 中建立新的发布副本 → 白名单复制程序和运行组件 → 编译 Setup → 输出到 releases。不会读取 settings.json、last-asset.json、个人图片或模型。

Standard 约 25 MiB，包含 GLB 解码需要的 Node；Full 约 166 MiB，额外附带可选图片尺寸识别；Update 约 3.3 MiB，不包含大型运行组件，不能用于第一次安装。

`stage.py` 仅在发布副本中跳过 macOS/Linux/ARM 的 ONNX 原生库及不使用的独立文本/图像权重；保留实际使用的 joint CLIP 模型及许可证。源运行环境保持不变。

## 测试与实现

```powershell
dotnet run --project .\installer\tests\AppPaths.Tests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File .\installer\tests\RhinoRuntime.Tests.ps1
```

`/DQA=1` 可编译隔离测试安装器：使用不同的 AppId 和 Rhino 注册测试分支，不影响真实插件注册。正式发布不要使用该开关。

安装器支持 `/DIR=...`、`/DATADIR=...`、`/RHINOEXE=...`、`/LANG=zhcn` 或 `/LANG=en`。升级时现有 DataRoot 优先于命令行，防止意外切换数据目录。

程序根目录中的 `remy-install.ini` 使用 UTF-16，记录 DataRoot 和 Rhino.exe；用户模型与设置独立保存。卸载仅清除安装器拥有的文件，模型/设置/工作缓存保留。新版本安装前备份旧插件目录到数据目录 backups 中。

目前 EXE 未签名，也尚未在另一台干净 Windows 机器上验证。公开发布前需要外部试装，生产发行可在签名流程中配置代码签名证书。Inno Setup 的使用许可见其官网；第三方语言文件来源见 ChineseSimplified.isl 文件头。

## Rhino 版本兼容

启动器与安装器分别测试 8.0、8.11、8.12、8.20、8.35 边界。8.0–8.11 使用 `/netcore`，避免传入当时尚不存在的 `/netcore-8`。本版支持 .NET Core 模式，不支持 Rhino 的 .NET Framework 模式；优先使用安装器创建的启动入口。

Rhino 8.0 SDK 只发布 net48 引用，项目有意把它作为 .NET 7 的编译接口基线；不打包 RhinoCommon/Rhino.UI/Eto DLL。8.7 才引入的 Texture.TreatAsLinear 通过可选反射启用：8.7+ 保留原贴图色彩处理，8.0–8.6 使用宿主原生通道默认处理。

已在现有 Rhino 8.35 上验证 .NET 7.0.1 与 .NET 8。8.0 原版宿主尚未实机验证，发布前建议让早期 Rhino 8 用户试装。详见验证记录-0.5.1.md。
