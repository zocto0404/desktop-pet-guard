# 守护事件动作源文件

参考仓库：[zocto0404/desktop-pet-guard](https://github.com/zocto0404/desktop-pet-guard)，提交 `db31ddc7bd8c3d7c350cfb816b86a0bfa005ec64`。
接入目标：[mutuxiang/redcowork](https://github.com/mutuxiang/redcowork)，基线 `00516972a4d60b21330f5912fcd33d13034894e0`。

`dist/engine.js` 和 `dist/art.js` 保留源事件参考实现，供核对部件顺序和动作布局；桌面程序实际运行父目录的 `GuardGame.cs` / `GuardScene.cs`，不执行 JavaScript。该原生版本将来源事件的持续生成小人改为倒计时原有的 60 人连续交接、围攻和补位。

`tools/motion.mjs` / `tools/frames.mjs` 来自源仓库，已改为 redcowork 三种小人的原始 SVG 类型顺序、髋部坐标和走路动作，并重新制作无腿击飞与史莱姆融化帧。起飞后不保留腿，原始身体轮廓连续形变为实色软团，五官陷入软团，再以软团收拢和高度塌陷到零面积完成消失；没有渐隐和轮廓交叉淡化。`dist/assets/shapes.json` 的小人路径与 `../Assets/countdown/source.json` 完全一致。

在本目录执行：

```powershell
node tools/frames.mjs
node tools/test.mjs
```

生成器会把可独立查看的 SVG 帧条写入忽略的 `dist/assets/frames-v2/`，并同步播放器使用的 `../Assets/guard/sprites.json` 与动画清单。部件数据 `../Assets/guard/parts.json` 来自上述源提交的 `PARTS`、`SCATTER`、`BODY_INDICES`。
