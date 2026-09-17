# Remy Asset Copilot

把图片和文字描述转化为可放入 Rhino 场景的 3D 资产。

**0.5.1 Beta · Windows 通用安装版**。支持 Windows 10/11 x64、Rhino 8.0 起的 Rhino 8 系列。Rhino 8.0–8.11 使用 .NET 7，8.12+ 可使用 .NET 7 / 8；启动入口自动选择已安装的兼容运行时。已在 Rhino 8.35 上分别验证 .NET 7 和 .NET 8；更早宿主版本仍需外部试装；不支持 macOS。

## 下载安装

在本仓库 **Releases** 中下载安装程序。`Code → Download ZIP` 仅提供源码，不能代替安装包。

| 文件 | 用途 |
| --- | --- |
| `RemyAssetCopilot-Setup-0.5.1-Full.exe` | 首次安装推荐；含可选的本地图片尺寸识别 |
| `RemyAssetCopilot-Setup-0.5.1-Standard.exe` | 体积较小；支持生成和导入，不含本地图片尺寸识别模型 |
| `RemyAssetCopilot-Setup-0.5.1-Update.exe` | 仅更新已经安装的 0.5.0 通用版；不能用于首次安装或 0.4.x 迁移 |

1. 保存项目并退出全部 Rhino。
2. 双击安装程序，选择程序目录、Rhino.exe 和数据目录。任何本机磁盘均可，不要求 D 盘。
3. 从开始菜单打开 **Remy Asset Copilot**；也可在 Rhino 中输入 `AssetCopilot`。
4. 新建或打开 Rhino 文档。在插件中填写自己的 Tripo API Key 后即可生成；需要可用的 API 额度。
5. 也可以用“+ → 本地 GLB”先测试预览、缩放和场景放置。

不需要自行安装 Node.js、Python 或开发工具。安装器默认仅为当前 Windows 用户安装，无需管理员权限。升级会保留原数据目录；卸载不删除模型、贴图、设置和工作缓存。原 0.4.x 用户选择原安装目录进行迁移即可。

当前安装包未进行代码签名。已完成本机安装/更新/卸载与中文路径测试，仍建议在另一台 Windows 机器上试装后正式推广。若 Releases 尚无附件，表示安装包还未上传。

## 功能

- 统一输入框：纯文字、上传/粘贴图片、图片加文字、本地 GLB。
- Tripo API 生成、PBR 材质预览、压缩 GLB 解码、插入 Rhino 场景。
- 手动尺寸、现实尺寸估算、参考面三种尺寸方式，结合 Rhino 文档单位进行缩放。
- 完整窗口/紧凑窗口切换，保留输入和任务状态；Remy 动画与加载动效。
- 模型、贴图及任务本地保存，支持恢复查询已提交任务。

现实尺寸是估算：图片通过本地 CLIP 识别，文字通过目标词和显式尺寸解析，结合尺寸规则推断，并非照片测量。图片加文字生成可能涉及图像处理与 3D 生成两步，按 Tripo API 规则计费。

## 文件位置

默认程序：`%LOCALAPPDATA%\Programs\RemyAssetCopilot`。

默认数据：`%LOCALAPPDATA%\RemyAssetCopilot\Data`。安装时可改到 D/E 等本机磁盘，之后也能在插件中单独修改模型保存目录。`remy-install.ini` 记录本机路径，不提交到 Git。

源代码和可再分发资源位于 `AssetCopilot/src`、`viewer`、`tools` 和 `sample`。安装器源码与构建说明见 [installer/README.md](installer/README.md)。

构建插件：安装 .NET 8 SDK 后运行 `AssetCopilot/build.cmd`。首次构建从 NuGet 获取固定版本的 Rhino 8.0 SDK 与 WebView2 SDK，不需要本机 Rhino 开发引用。插件目标为 .NET 7，运行时兼容 .NET 8。完整安装包的构建还需要 runtime 组件，详见安装器文档。

## GitHub 发布

此仓库不包含个人图片、生成模型、API Key、任务记录、缓存、编译结果和大体积运行库。

1. 将本文件夹内源码提交到仓库。
2. 创建 Release，标签建议 `v0.5.1`，标记为预发行版。
3. 在 Release 附件中上传 Full、Standard、Update 三个 EXE 与 `SHA256SUMS-0.5.1.txt`；不要把 EXE 放入源码目录提交。
4. 发布说明可使用 `RELEASE_NOTES.md`。

API Key 只在插件当前窗口中使用，不要写进源码或截图。

## 许可

第三方说明见 [THIRD-PARTY-NOTICES.md](AssetCopilot/THIRD-PARTY-NOTICES.md)。项目尚未指定代码许可证；本次没有替作者选择开源许可证。Remy Logo 与视频素材独立于第三方代码许可。
