# 阶段 0 验收

日期：2026-10-25

## 目标

确认本机开发环境，建立可编译、可测试的最小 WPF 工程，并明确后续阶段边界。阶段 0 不实现产品功能。

## 环境记录

| 项目 | 结果 |
| --- | --- |
| Windows | 10.0.26100，x64 |
| Git | 2.54.0.windows.1 |
| 系统 .NET SDK | 无 |
| 系统 .NET 运行时 | .NET 7.0.7、8.0.28（仅运行时） |
| 项目 .NET SDK | 10.0.402，工作区内安装 |
| 最低运行时 | .NET Desktop Runtime 10.0.13 |
| .NET 10 支持截止 | 2028-11-14 |
| 测试框架 | MSTest 4.0.2 |

SDK ZIP 的 SHA-512 已与 Microsoft 官方 `release-metadata/10.0/releases.json` 对比一致。

## 依赖检查

| 依赖 | 范围 | 许可证 | 兼容性与部署影响 |
| --- | --- | --- | --- |
| MSTest 4.0.2 | 测试 | MIT | Microsoft 维护，支持 .NET 8+；仅进入测试输出，不随应用发布 |

应用本身没有新增第三方运行时依赖。NuGet 版本由 `Directory.Packages.props` 集中管理，测试包由仓库内 `NuGet.Config` 从官方 NuGet.org 还原。MSTest 会带入测试运行器、适配器和覆盖率相关传递包，但这些均不进入最终桌面应用。

`dotnet list package --vulnerable --include-transitive` 查询结果为三个项目均无已知漏洞包。

## 完成标准

- [x] 工作区新建独立 Git 仓库，未覆盖已有文件
- [x] .NET 10 SDK 可用
- [x] WPF、核心库、测试项目可加入同一解决方案
- [x] WPF 启动壳与核心库引用关系成立
- [x] 阶段边界和固定产品约束有文档记录
- [x] NuGet 还原成功
- [x] 解决方案构建通过（0 警告，0 错误）
- [x] 自动化测试通过（1/1）
- [x] 格式检查通过
- [x] 直接及传递依赖无已知漏洞
- [x] WPF 启动壳实际启动（驻留 3 秒后测试进程正常结束）

## 环境注意事项

- 本机没有系统级 .NET SDK；当前使用工作区内的 `D:\codex\.dotnet\dotnet.exe`。
- 仓库提交不包含 SDK、NuGet 缓存或构建输出；CI/新开发机需安装 .NET 10.0.4xx SDK 后执行文档中的还原、构建和测试命令。
