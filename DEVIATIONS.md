# 与设计文档的偏离说明

对照《午夜怪物便利店 游戏原型设计文档》，逐条记录实现与文档不一致的地方、原因，以及改回去要动什么。

---

## 1. 渲染管线：URP → 内置渲染管线

**文档**：§11.1 推荐技术栈列出 Universal Render Pipeline。

**实现**：使用 Unity 内置渲染管线，`Packages/manifest.json` 里没有 URP。

**原因**：原型的美术全是程序化生成的色块 Sprite，URP 在这个阶段不提供任何视觉收益，
但配置错误（未创建/未分配 Pipeline Asset）会直接导致全屏粉色，是最常见的"打开就跑不起来"故障源。
文档里唯一真正需要光照的地方是停电事件，已用全屏遮罩（`HudView.SetBlackout`）实现，效果等价。

**改回去**：装 `com.unity.render-pipelines.universal` → 创建 URP 2D Renderer + Pipeline Asset →
在 Graphics Settings 里分配 → 把 `SpriteFactory` 生成的 Sprite 材质换成 URP 2D 的 Sprite-Lit-Default，
然后把停电遮罩换成 Global Light 2D 的强度动画。

---

## 2. 输入：Input System 包 → 旧版 Input Manager

**文档**：§11.1 列出 Unity Input System。

**实现**：`Assets/Scripts/Core/InputReader.cs` 封装了旧版 `UnityEngine.Input`。
`ProjectSettings.asset` 中 `activeInputHandler: 0`（Old）。

**原因**：Input System 需要包安装 + 切换后端 + 编辑器重启，任一步没做对都会在运行时抛异常。
旧版 Input 在 Unity 默认设置下开箱即用。

**改回去**：游戏其余部分只访问 `InputReader` 暴露的属性，不直接碰任何输入 API。
装包 → 把 `activeInputHandler` 改为 `1` 或 `2` → 只重写 `InputReader.cs` 的内部实现即可，
其它文件一行都不用改。

---

## 3. 地图渲染：Tilemap → SpriteRenderer

**文档**：§9.2「建议使用 Tilemap：地图大小 24 × 16 格，单格 32 × 32 像素」。

**实现**：**逻辑网格严格是 24×16 格 / 32px**（`GameConfig.GridWidth/Height/PixelsPerUnit`，
`StoreGrid`），寻路、碰撞、设施布局、通道宽度全部按文档执行。
只有**渲染**改成了每格一个 `SpriteRenderer`。

**原因**：减少一层包与 API 依赖。渲染方式对玩法零影响。

**改回去**：`StoreWorld.BuildFloorVisual()` 是唯一需要改的方法。

---

## 4. 文本：TextMeshPro → 旧版 uGUI Text

**文档**：未明确指定，但 Unity 现代做法是 TMP。

**实现**：`UnityEngine.UI.Text` + `Font.CreateDynamicFontFromOSFont`，
字体回退链为 微软雅黑 → SimHei → SimSun → Noto Sans CJK → Arial Unicode。

**原因**：TMP 渲染中文需要预先烘焙包含数千汉字的字体资产，无法用代码在运行时可靠生成。
动态 OS 字体开箱即用，目标平台是 Windows PC（文档 §1.4），字体一定存在。

**风险**：如果系统缺少上述所有字体，中文会显示为方块。Windows 上不会发生。

**改回去**：生成 TMP 中文字体资产 → 把 `UIFactory.Label` 换成 TMP 版本。

---

## 5. 碰撞：Rigidbody2D / Collider2D → 自写网格碰撞

**文档**：未明确指定物理方案。

**实现**：工程里**没有任何** `Rigidbody2D`、`Collider2D`、`Physics2D` 调用。
- 玩家移动：分轴 + 圆形 vs 格子的最近点判定（`StoreGrid.CircleOverlapsBlocked`），自然沿墙滑动
- 顾客移动：A* 路径点插值
- 交互查询：`InteractableRegistry` 距离查询，替代 `Physics2D.OverlapCircle`

**原因**：确定性。物理引擎在低帧率或高速移动下可能把角色挤进墙里、卡在货架缝隙中，
而文档 §19 的技术标准明确要求「顾客不会长期卡住」。网格方案下这类问题在结构上不可能发生。
副作用是不需要配置 Layer、Tag、碰撞矩阵。

---

## 6. 营业时长：单日 5~8 分钟 → 200 / 260 / 320 秒

**文档**：§2.1「营业时间约为 5～8 分钟」，但 §1.5 同时要求「10～20 分钟完整体验」。
两者互相矛盾（5 分钟 × 3 天 = 15 分钟营业，加上准备和结算会超过 20 分钟）。

**实现**：取 3.3 / 4.3 / 5.3 分钟，三天合计约 13 分钟营业时间，
加准备与结算约 16~18 分钟，落在 §1.5 的区间内。同时避免顾客都走完了还在空转。

**改回去**：`GameDatabase.BuildDays()` 里的 `businessSeconds`。

---

## 7. 数据资产：ScriptableObject 资产 → 运行时构造

**文档**：§12.2 / §12.3 用 `[CreateAssetMenu]` 定义 ScriptableObject。

**实现**：`ProductData` / `CustomerData` / `DayPlan` **完全按文档定义**（含 `CreateAssetMenu`），
但实例由 `GameDatabase` 在运行时用 `ScriptableObject.CreateInstance` 构造。

**原因**：`.asset` 文件需要 Unity 分配 GUID 并与脚本 `.meta` 的 GUID 对应，
手工编写极易出错且不可验证。运行时构造保证克隆下来按 Play 就能跑。

**改回去**：菜单 `Tools/MonsterStore/生成数据资产` 会把当前数据导出成规范的 `.asset`
（GUID 由 Unity 自己分配），之后改 `GameDatabase` 的加载逻辑为读取资产即可。

---

## 8. 角色尺寸：48×64 → 32×48

**文档**：§14.2「人物 Sprite：48×64 或 64×64」。

**实现**：32×48 像素（1 × 1.5 世界单位）。

**原因**：在 20×12 格的店内，48×64（1.5×2 格）的角色在 2 格宽的通道里会明显互相穿模。
32×48 在同样的通道宽度下可读性更好。

**改回去**：`SpriteFactory.CharW/CharH`。

---

## 9. 摄像机：全店可见 vs 跟随

**文档**：§16 阶段一要求「摄像机跟随」。

**实现**：`CameraRig` 实现了跟随 + 边界钳制，正交尺寸 7.6（可见约 15 格高），
因此纵向有实际跟随、横向基本铺满。既满足文档，又保证玩家能看到大部分店面。

**改回去**：`CameraRig.orthographicSize`。设为 8.5 以上则等价于固定全景。

---

## 10. 保质期

**文档**：§2.4 列出商品状态含「已过期」，但同段明确写「原型阶段可以暂时不做保质期，避免复杂度过高」。

**实现**：按文档建议**不做**。`ProductData` 里也没有保留无用字段。

---

## 11. 小史莱姆的"引导回主史莱姆"

**文档**：§7 事件四「玩家需要把它们引导回主史莱姆」。

**实现**：小史莱姆会随机游荡、持续留污渍，28 秒后自行消失；
玩家可以走过去按 `E` 提前把它赶走（声望 +1）。

**原因**：文档没有说明"引导"的具体交互形式（推？吸引？围堵？）。
选择了最直接、最容易理解的一种。真正的引导玩法（比如用发光果冻当诱饵）值得后续单独设计。
