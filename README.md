# 晾图

Windows 原生截图与桌面参考资料工具，使用 C#、WPF、Win32、SQLite 和本地文件系统实现。

当前版本：`v1.0.0`。已实现可运行的 V1 主链路：

- 顶部晾衣绳、缩略图、点击复制、右键标注、短拖排序和拖入图片
- 长按拖到桌面或点击木夹可取下图片，桌面图钉可一键挂回晾衣绳
- 晾衣绳图片左上角可删除，拖出到资料管理器或其他应用只复制文件，不移动原图
- 图片库支持多选、批量删除、预览、标注、贴图和重新挂回
- 真正透明的内容窗口：只绘制绳子、夹子和圆角裁切图片，空白区域点击穿透
- 内容窗口位于屏幕上方外侧，唤出时以真实的窗口坐标完成垂落、单次回弹和反向收回
- 默认 120 秒自动隐藏，可关闭；悬停和交互暂停计时，结束后重新开始完整两分钟
- 剪贴板图片自动收集、暂停收集和内容哈希去重
- `Alt+S` 区域截图与 `F1` 窗口截图
- 标注编辑器：画笔、箭头、矩形、文字、马赛克、撤销/重做和原位保存
- `F3` 桌面贴图、置顶、移动、缩放、透明度调整和关联图片同步
- SQLite 元数据、缩略图缓存、排序与贴图状态持久化、编辑恢复日志
- 系统托盘、设置、图片库、开机启动和可配置快捷键
- 统一深色晾衣绳视觉、液体玻璃圆角相框、木夹与篮子入口，以及浅色 Windows 工作区
- 产品图标已应用到 EXE、窗口和系统托盘
- 单实例运行；重复启动会唤出已有晾衣绳，第二个进程静默退出
- 提供 win-x64 self-contained 单文件发布和 ZIP 交付包

## 技术基线

- .NET 10 LTS（`10.0.402` SDK）
- WPF + 少量 Win32 P/Invoke
- SQLite：`Microsoft.Data.Sqlite` 10.0.13
- 测试：MSTest 4.0.2

除 SQLite 外，应用没有额外的第三方运行时依赖。

## 数据位置

- 图片：默认 `%USERPROFILE%\Pictures\Screenshots`
- 数据库：`%LOCALAPPDATA%\LiangTu\liangtu.db`
- 缩略图缓存：`%LOCALAPPDATA%\LiangTu\cache\thumbnails`
- 设置：`%LOCALAPPDATA%\LiangTu\settings.json`
- 错误日志：`%LOCALAPPDATA%\LiangTu\errors.log`

图片目录可以在设置中修改。数据库、缓存和日志不会写入图片目录。

## 默认快捷键

| 功能 | 快捷键 |
| --- | --- |
| 区域截图 | `Alt+S` |
| 窗口截图 | `F1` |
| 将剪贴板图片贴到桌面 | `F3` |

快捷键可在托盘菜单中选择“设置快捷键与自动隐藏”修改。应用不接管 `Win+Shift+S`。

## 项目结构

```text
src/
  LiangTu.App/          WPF 界面、Win32 适配、SQLite、图标与发布配置
  LiangTu.Core/         不依赖 WPF/Win32 的实体与自动隐藏状态机
scripts/
  publish-win-x64.ps1   一键还原、发布和 ZIP 打包
tools/
  Generate-AppIcon.ps1  生成多尺寸产品图标
tests/
  LiangTu.Core.Tests/   状态机与领域规则测试
  LiangTu.App.Tests/    数据库迁移、导入、排序和原位编辑测试
docs/
  architecture.md       架构边界与技术决策
  phase-0.md            阶段 0 环境记录
  v1-core.md            V1 核心功能验收记录
  ui-design.md          UI 设计方向与实机验收记录
  release.md            1.0.0 发布与交付验收
```

## 构建与运行

本机 SDK 位于 `D:\codex\.dotnet\dotnet.exe`，NuGet 缓存位于工作区。

```powershell
$dotnet = "D:\codex\.dotnet\dotnet.exe"
$env:DOTNET_CLI_HOME = "D:\codex\.dotnet-home"
$env:NUGET_PACKAGES = "D:\codex\.nuget\packages"

& $dotnet restore .\LiangTu.slnx --configfile .\NuGet.Config
& $dotnet build .\LiangTu.slnx --no-restore
& $dotnet test .\LiangTu.slnx --no-build --no-restore
& $dotnet run --project .\src\LiangTu.App\LiangTu.App.csproj --no-build --no-restore
```

直接启动 `bin` 下的 apphost 时，需要把工作区运行时加入 `DOTNET_ROOT`：

```powershell
$env:DOTNET_ROOT = "D:\codex\.dotnet"
```

发布给其他 Windows 用户时执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-win-x64.ps1
```

输出位置：

- 单文件程序：`artifacts\publish\win-x64\LiangTu.exe`
- 交付压缩包：`artifacts\publish\LiangTu-1.0.0-win-x64.zip`
- SHA-256：`artifacts\publish\SHA256SUMS.txt`

## 当前边界

- 截图以当前鼠标所在显示器为单位处理，支持 Per-Monitor V2 DPI；不跨多个显示器拖出单个选区。
- 系统保护或硬件覆盖层窗口可能无法可靠截取，此时窗口截图会退回区域选择。
- 尚未提供安装包、代码签名、图片库删除操作和自动更新。
- 编辑保存默认不建立备份；通过临时文件、校验、原子替换和启动恢复日志保证一致性。
