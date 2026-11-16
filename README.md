# 桌宠守护 · Windows 整合版

这个仓库保存当前可运行的 Windows 桌宠：redcowork 日常桌宠、5→4→3→2→1 小队倒计时、60 秒踩踏守护、部件归位，以及用户提供的左右拖鞋。

## 下载运行

下载 [Windows 运行包](packages/desktop-pet-windows.zip)，完整解压后双击 `WobblyPet.exe`。请保留同目录 DLL、deps 和 runtimeconfig 文件。需要 Windows 10 / 11 x64 和 .NET 9 Windows Desktop Runtime。

| 操作 | 动作 |
| --- | --- |
| S | 启动倒计时；同一批 60 只小人保持可见，散开后向桌宠当前位置聚拢攻击 |
| L / R | 日常桌宠左右脚支撑 |
| F / H | 守护时左 / 右拖鞋踩踏，落地时才判定命中 |
| WASD / IJKL | 守护时移动左 / 右脚；鼠标可瞄准所在一侧的脚 |
| Esc | 守护时暂停 / 继续 |
| Ctrl + Alt + R | 重置桌宠和当前事件，恢复初始大小、置顶和位置 |
| Ctrl + Alt + Q | 退出桌宠及倒计时 / 守护窗口 |

右键菜单也可开始、提前结束、重置和退出。60 秒结束后小队撤退、部件归位，恢复日常桌宠，可再次按 S。拖动或缩放桌宠时，小队会追向新位置；屏幕底边处的脚踏和部件堆保持可见。

运行包信息与 SHA256 见 [packages/README.md](packages/README.md)。原始拖鞋图已嵌入程序，无需保留聊天附件。

## 源码与构建

当前 Windows 主程序在 [`desktop-pet/`](desktop-pet/README.md)，使用 .NET 9 / WPF 原生透明窗口，不依赖网页或 Electron。完整构建、动作生成与测试说明见该目录的 README。

```powershell
dotnet build desktop-pet/DesktopPet.csproj -c Release
dotnet run --project desktop-pet/DesktopPet.csproj -c Release
dotnet publish desktop-pet/DesktopPet.csproj -c Release -r win-x64 --self-contained false -o publish/desktop-pet
```

## 验证

```powershell
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-guard output/guard-qa
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-controls output/controls-qa
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --smoke-flow output/flow-qa
```

已有验证记录保存在 [`docs/validation/`](docs/validation/)：完整实际窗口流程、倒计时连续交接、踩踏命中、暂停继续、部件归位、再次开始、重置 / 退出清理、拖鞋朝向与屏幕边缘约束。完整实际窗口流程约需 95 秒，测试窗口在屏幕外运行。

## 原网页 / Electron 版本与备份

原有 `dist/`、`desktop/`、`tools/`、`package.json` 和站点配置保留在原路径。原说明移至 [docs/legacy-web-desktop.md](docs/legacy-web-desktop.md)。原网页版本仍可通过 `npm start` 运行。

本次整合基于旧 `main` 提交 `db31ddc7bd8c3d7c350cfb816b86a0bfa005ec64`；对应备份分支名为 `backup/main-before-native-20261116`，更新 `main` 前需先完成该远端备份。原 `main` 的历史保留为本次提交的祖先，不改写历史。

Windows 整合源码来自 [mutuxiang/redcowork](https://github.com/mutuxiang/redcowork) 的 `feat/countdown-pet-guard` 分支，源提交 `5675aee`。守护事件来源与动作修改记录见 [desktop-pet/guard-source/README.md](desktop-pet/guard-source/README.md)。
