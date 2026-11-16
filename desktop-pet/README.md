# 粉色小人 · Windows 桌宠

透明悬浮的 Windows 桌宠，当前版本 v11。按住 L / R 控制左右脚，S 召唤倒计时小队，并接入 60 秒踩踏守护事件。

| 输入 | 动作 |
| --- | --- |
| L + R | 按原稿双脚站稳，偶尔眨眼、晃杯子和挥手 |
| 只按 L | 左脚支撑，持续播放指定增强版挣扎动画 |
| 只按 R | 右脚支撑，持续播放指定增强版挣扎动画 |
| 都松开 | 粉色身体以不透明实色轮廓融化成蔫掉的史莱姆，四肢、衣服和配饰独立散落 |
| 按一次 S | 60 只小宠物组成 5→4→3→2→1，原地散开 2.2 秒后直接聚拢攻击桌宠 |
| 守护时 F / H | 左 / 右脚踩踏；脚落地后才判定击中，长按不重复触发 |
| 守护时 WASD / IJKL | 移动左 / 右脚；移动鼠标也可瞄准光标所在一侧的脚 |
| 守护时 Esc | 暂停 / 继续 |
| Ctrl + Alt + Q | 立即退出桌宠，关闭倒计时 / 守护窗口 |
| Ctrl + Alt + R | 重置桌宠，清空当前事件，恢复初始大小、置顶和位置 |

左右脚动画各有 48 个挣扎姿势，入场只播放一次，之后循环。重新按键时从胶团与散落部件恢复。倒计时背景透明、鼠标可穿透，入场约 3.4 秒。数字 1 后的 60 名小人保持可见，散开后由同一批角色直接接入守护事件，保留三种角色各 20 只。拖动或缩放桌宠时，小人保留桌面位置，并追向桌宠的新位置。

守护流程移植自 [desktop-pet-guard](https://github.com/zocto0404/desktop-pet-guard)：逐步拆取 19 个部件、双人搬帽子/外套、踩中搬运者救回部件，60 秒后撤退归位。右键“小队散场 / 结束守护并归位”可提前结束。全程使用原 WPF 透明窗口与原生渲染，不启动另一个网页或 Electron 桌宠。日常 L/R 动作在事件结束后恢复。

小人行走采用 redcowork 的 32° 双腿交替、4° 摇摆和原始 SVG；腿围绕实际髋部旋转，向左时连同脚尖一起镜像。三种击飞动作各重新生成 96 帧：受击压扁、起飞后去掉双腿、落地后身体轮廓连续流动并摊成实色史莱姆，五官陷入软团，最后软团收拢、塌陷到零面积。身体与飞溅均保持实色，消失完全通过几何形变完成，无渐隐或轮廓交叉淡化。

可拖动桌宠，右键菜单提供大小、置顶、倒计时、提前结束、重置和退出。L/R/S 及守护操作按键采用全局检测。攻击小队覆盖所有显示器，保持透明、鼠标穿透且不抢焦点。

右键菜单也提供“重置桌宠 · Ctrl+Alt+R”和“退出桌宠 · Ctrl+Alt+Q”。这两个组合键注册为全局快捷键，可在倒计时、守护、暂停和日常状态使用。重置会立即清空小队和损坏状态，再次按 S 可重新开始。

脚踏已替换为用户提供的透明拖鞋图，左脚 / 右脚分别取原图对应拖鞋，保留图案与朝向。原图完整存于 `Assets/guard/slippers.png`；播放器裁切显示，沿用下落、踩下压缩和回弹节奏，落地命中时机保持一致。

## 构建运行

需要 Windows 10 / 11 x64 和 .NET 9 SDK。运行所需 PNG 与动作数据都在 `Assets/`，会嵌入可执行文件，无需联网或重新生成素材。

在仓库根目录运行：

```powershell
dotnet build desktop-pet/DesktopPet.csproj -c Release
dotnet run --project desktop-pet/DesktopPet.csproj -c Release
```

生成本机运行目录（需要 .NET 9 Windows Desktop Runtime）：

```powershell
dotnet publish desktop-pet/DesktopPet.csproj -c Release -r win-x64 --self-contained false -o publish/desktop-pet
```

双击输出目录中的 `WobblyPet.exe`，保留同目录 DLL、deps 与 runtimeconfig 文件。本机运行目录已完成启动验证。重复打开不会产生多只桌宠；更新版本前先退出旧版。程序不注册开机启动。

## 验证与导出

以下命令通过程序内的同一渲染器验证状态并导出 PNG 帧，不使用屏幕捕获：

```powershell
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-enhanced output/enhanced
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-slime output/slime
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-blink output/blink
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-countdown output/countdown
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-guard output/guard-qa
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --smoke-guard output/guard-qa
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --smoke-flow output/guard-qa
dotnet run --project desktop-pet/DesktopPet.csproj -c Release -- --test-controls output/controls-qa
```

各目录的 `result.txt` 保存验证结果。覆盖原始增强帧与循环节奏、长按不重放正常片段、固定粉色和不透明度、散落部件、三个恢复目标、快速切换、错峰眨眼、透明倒计时和离场。

`--test-guard` 另外覆盖 60 人连续交接、三种类型顺序、屏幕比例、完整 19 件拆取、落脚判定、双人货物救回、暂停、提前归位，以及所有预生成 SVG 帧的原生解析。输出事件截图、步态朝向图、击飞接触表和 96 张 PNG 动画帧。`--smoke-guard` 在屏幕外创建实际窗口，验证原生样式、交接、拖动缩放和提前结束。

`--smoke-flow` 通过实际桌宠的菜单入口完整运行约 95 秒：验证倒计时、散开、攻击交接、踩踏命中、暂停/继续、自然结束、归位、日常显示恢复、再次开始及提前散场。测试窗口在屏幕外运行，不读取全局按键，结果写入 `flow-result.txt`，过程写入 `flow-progress.txt`。靠近屏幕底边时，脚踏、等待小人和倒地部件堆会约束在可见区域内。

`--test-controls` 验证透明拖鞋的左右分配、脚踏尺寸与底边范围，并通过窗口快捷键消息调用实际重置 / 退出菜单；覆盖暂停受损状态重置、倒计时重置、再次开始与退出清理，输出 `controls-result.txt` 和 `slipper-poses.png`。

重新生成并同步守护帧（普通构建不需要）：

```powershell
cd desktop-pet/guard-source
node tools/frames.mjs
node tools/test.mjs
```

## 源码与素材

- `Program.cs`：全局按键、悬浮窗口、主状态与菜单。
- `OriginalDesign.cs`：原稿、待机小动作、实色史莱姆、散落物理与恢复过渡。
- `ApprovedAnimation.cs`：增强版左右脚原帧、原始毫秒节奏与透明背景播放。
- `Countdown.cs`：三种 SVG 小宠物、60 只双排编队、走路与眨眼、透明倒计时窗口。
- `GuardGame.cs` / `GuardInput.cs`：原生守护状态、同一批小人、脚踏和全局输入。
- `GuardArtwork.cs` / `GuardScene.cs`：共享 SVG 几何、预生成帧播放、部件遮罩及透明场景。
- `GuardTests.cs`：逻辑、动画和实际窗口检查。
- `guard-source/`：事件参考源码、修改后的动作生成器和校验；来源记录见其 README。
- `Assets/reference/`：用户原始角色和白领。
- `Assets/enhanced/`：用户指定 `wobbly-mascot-v2-enhanced` 的 105 张原始帧及节奏/哈希记录。
- `Assets/countdown/`：原始 SVG 路径和五个数字的最终编队。
- `Assets/guard/`：嵌入的 90 段 / 2,788 帧守护动作、部件数据和清单。

根目录的 Python / Node 辅助脚本来自本地制作流程，仅用于重新导入原始素材或编码预览；部分脚本需要工作区原始 `output/` 素材与 Python Pillow。普通构建与运行不依赖它们。
