# 1.0.0 发布与交付

日期：2026-10-25

## 版本信息

| 属性 | 值 |
| --- | --- |
| 产品 | 晾图 |
| 版本 | 1.0.0 |
| 文件版本 | 1.0.0.0 |
| 产品版本 | 1.0.0 |
| 公司 | LiangTu |
| 目标 | Windows x64 |

版本属性定义在 `Directory.Build.props`，界面版本从 `ProductInfo.Version` 读取。

## 产品图标

图标方向沿用现有视觉系统：深灰应用瓦片、暖白下垂晾衣绳、单张青绿山景照片。图标避免多元素堆叠，保证在 16px 托盘尺寸仍有清晰轮廓。

源生成脚本：`tools/Generate-AppIcon.ps1`

输出：

- `src/LiangTu.App/Assets/liangtu.ico`
- `src/LiangTu.App/Assets/liangtu-256.png`

ICO 包含 `16、24、32、48、64、128、256` 七个尺寸，并用于可执行文件、WPF 窗口和托盘图标。

## 单实例

使用命名事件 `Local\LiangTu.ShowRequested`：

- 首个进程持有所在用户会话内的实例句柄。
- 重复启动时，第二个进程发送唤出信号后立即退出。
- 首个进程收到信号后显示晾衣绳，并把托盘状态同步为“隐藏晾衣绳”。
- 不再弹出模态提示窗口。

## 发布配置

发布配置位于 `src/LiangTu.App/Properties/PublishProfiles/win-x64.pubxml`：

- `win-x64`
- `SelfContained`
- `PublishSingleFile`
- 原生库包含在单文件内
- 单文件压缩
- 不启用裁剪，避免 WPF/XAML 反射行为被破坏
- 不发布 PDB

## 交付验收

- Debug 构建：0 警告，0 错误
- Release 构建：0 警告，0 错误
- EXE 图标可从发布文件中提取
- 产品版本显示为 `1.0.0`
- 在 `DOTNET_ROOT` 指向不存在目录且关闭多级运行时查找时，发布版正常启动
- SQLite 首次启动初始化成功，无错误日志
- 从 ZIP 解包后独立启动成功
- 设置界面可读取并显示 `v1.0.0`
- 第二实例退出，首个实例从隐藏位置重新唤出

## 产物

- 单文件 EXE：约 76.7 MB
- ZIP：约 70.9 MB
- SHA-256 清单随发布目录生成

当前未提供数字签名和安装程序；对外分发前应完成代码签名。
