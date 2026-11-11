# Luna My Master

《绝地潜兵 2》的模组管理器 —— 把 mod 库、层链诊断、应用/还原做成一个黑黄配色的桌面工具。

![](docs/preview-m4.png)

## 它能做什么

- **导入**：拖入 zip / rar / 7z，自动解压成 mod 库；同名导入会原地替换（保留启用状态与备注）
- **应用更改**：把启用的 mod 按 bundle 重编号成 `patch_N` 层，用硬链接部署到游戏 `data` 目录（跨盘自动回退到复制）
- **还原原版**：一键清空所有层，游戏回到官方状态
- **层链检查**：列出每个 bundle 的层数 / 来源 mod / 多层覆盖警告
- **占用检查**：按装备代号统计谁占了什么，红绿球 + 护甲/武器标签 + 手动隐藏条目
- **N 网联动**：按 Nexus 编号拉取名称 / 简介 / 图标 / 分类，检测更新（版本号或文件时间戳变化）
- **完整性校验**：记录导入时的文件指纹，文件夹被动过就红字提醒重新导入
- **托盘 / 关闭行为**：可切换"直接退出"或"收进后台"

## 一些实现细节

- `.NET 9` + `WPF`，无第三方依赖（NuGet 只用来还原 SDK 自带包）
- 自绘无边框窗口：`WindowChrome` + DWM 圆角 + 自定义最小化/恢复动画（拦截 `WM_SYSCOMMAND/SC_MINIMIZE`）
- Nexus API key 用 DPAPI 加密存在 `%APPDATA%\HD2MM\secret.bin`
- 层部署用 `CreateHardLinkW`，部署前重编号并在同 bundle 内按「共享材质 → 变体」排序
- `DO NOT DISABLE` 的选项强制部署，避免共享材质层被手动关掉导致护甲透明

## 构建

```powershell
dotnet build HD2MM.csproj -c Release
dotnet publish HD2MM.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

需要 .NET 9 SDK 与 Windows 10/11。

## 说明

- 仅供个人整理模组用，使用前请自行备份游戏 `data` 目录
- 与 Arrowhead / Sony 无任何关联
