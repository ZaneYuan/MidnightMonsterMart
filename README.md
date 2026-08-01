# 午夜怪物便利店 / Midnight Monster Mart

按《午夜怪物便利店 游戏原型设计文档》实现的 Unity 可玩原型。

2D 俯视角经营模拟：你在一家只在午夜营业的便利店里补货、收银、应付怪物顾客的规则和禁忌，
撑过三个营业日，等待神秘检查员的评级。

---

## 运行

### 前置

- **Unity 6**（工程锁定 `6000.0.32f1`；用 Unity Hub 打开时如提示升级，选同一大版本即可）
- Git LFS（二进制素材走 LFS；当前原型没有二进制素材，但配置已就位）

### 步骤

```bash
git clone https://github.com/ZaneYuan/MidnightMonsterMart.git
```

1. Unity Hub → Add → 选择工程根目录
2. 打开 `Assets/Scenes/Boot.unity`（其实打开任何场景都行，见下）
3. 按 **Play**

**不需要任何手动配置。** 场景、货架、玩家、UI、美术、音效全部由代码在运行时生成，
入口是 `GameBootstrap` 的 `[RuntimeInitializeOnLoadMethod]` —— 场景里没有需要拖拽的对象，
也没有需要绑定的预制体或资产引用。

---

## 操作

| 按键 | 作用 |
|---|---|
| `WASD` / 方向键 | 移动 |
| `Shift` | 加速 |
| `E` | 交互（补货、扶货架、清污渍等需要**长按**） |
| 鼠标左键拖拽 | 收银时把商品拖过扫描区 |
| `Tab` | 怪物图鉴 |
| `Esc` | 暂停 / 关闭当前面板 |
| `1` `2` `3` | 事件弹窗的快捷选择 |

---

## 一个营业日怎么玩

1. **营业前准备**（无时间限制）— 用有限的钱进货、把镜子处理掉或留着、够钱就升级收银台
2. **午夜营业** — 从仓库取货 → 走到货架长按 `E` 补货 → 顾客排队后到收银台扫码
3. **结算** — 收支、顾客满意度、目标达成情况、新解锁内容
4. **下一天**

三天流程：

| | 主题 | 新增内容 | 目标 |
|---|---|---|---|
| 第一天 | 基础教学 | 吸血鬼、史莱姆 | 服务 4 名顾客，利润 > 0 |
| 第二天 | 压力增加 | 狼人（满月）、幽灵、排队、停电 | 服务 5 名，声望 ≥ 40 |
| 第三天 | 综合测试 | 全部怪物 + 神秘检查员 | 整洁度 ≥ 60，声望 ≥ 60，完成检查 |

结局按声望 / 累计利润 / 检查评级判定为 优秀 / 普通 / 失败。

---

## 四种怪物的规则

| 怪物 | 喜欢 | 讨厌 | 特殊规则 |
|---|---|---|---|
| 吸血鬼 | 血橙汽水 | 黑蒜面包、镜子 | 靠近装饰镜持续掉耐心；结账时可能要求黑色袋子 |
| 狼人 | 月光牛奶 | 银纸巧克力 | 耐心掉得最快；低于 20 会撞倒货架；满月夜入店即情绪警告 |
| 幽灵 | 灵魂薄荷糖 | 驱灵盐 | 拿不到实体商品，需要你送去**灵界包装台**处理再交给它；有时会忘记要买什么 |
| 史莱姆 | 发光果冻 | 干燥、被驱赶 | 走过留污渍；偶尔吞下两件商品；吃发光果冻可能分裂 |

---

## 工程结构

```
Assets/
├── Editor/
│   └── DataAssetGenerator.cs      导出 .asset / 删存档 / 打开存档目录
├── Scenes/Boot.unity              空场景，仅作为构建入口
└── Scripts/
    ├── Art/SpriteFactory.cs       所有占位美术的程序化生成
    ├── Audio/AudioDirector.cs     所有音效与背景音的波形合成
    ├── Core/
    │   ├── GameBootstrap.cs       入口：搭出整个游戏
    │   ├── Game.cs                服务定位器
    │   ├── GameManager.cs         四阶段总状态机
    │   ├── DayManager.cs          营业日、波次、结算数据
    │   ├── StoreMetrics.cs        金钱 / 声望 / 整洁度
    │   ├── StoreGrid.cs           24×16 逻辑网格与碰撞
    │   ├── Pathfinder.cs          八方向 A*
    │   ├── CameraRig.cs           跟随 + 边界钳制
    │   ├── InputReader.cs         输入抽象层
    │   └── BestiaryTracker.cs     图鉴解锁
    ├── Customer/
    │   ├── CustomerController.cs  10 状态有限状态机
    │   ├── IMonsterBehaviour.cs   组合模式接口
    │   ├── MonsterBehaviours.cs   吸血鬼 / 狼人 / 幽灵 / 史莱姆 / 检查员
    │   ├── CustomerSpawner.cs     波次生成
    │   └── CustomerBubble.cs      头顶需求 / 耐心 / 情绪
    ├── Data/                      ScriptableObject 定义 + GameConfig + GameDatabase
    ├── Events/RandomEventManager.cs   五个随机事件 + 检查员评级
    ├── Player/                    移动、碰撞、交互、携带
    ├── Save/SaveSystem.cs         persistentDataPath JSON 存档
    ├── Store/                     网格世界、货架、收银台、仓库、灵界台、镜子、污渍
    └── UI/                        程序化 uGUI：HUD / 进货 / 收银 / 结算 / 图鉴 / 弹窗
```

数值调参集中在 [`Assets/Scripts/Data/GameConfig.cs`](Assets/Scripts/Data/GameConfig.cs)，
商品 / 怪物 / 三天配置集中在 [`Assets/Scripts/Data/GameDatabase.cs`](Assets/Scripts/Data/GameDatabase.cs)。

---

## 与设计文档的偏离

详见 [DEVIATIONS.md](DEVIATIONS.md)。摘要：

| 文档规格 | 实现 | 原因 |
|---|---|---|
| URP | 内置渲染管线 | 2D 色块原型无收益；停电用全屏遮罩实现 |
| Unity Input System | 旧版 `Input` + `InputReader` 抽象层 | 零配置；换回只需改一个文件 |
| Tilemap 渲染 | SpriteRenderer（**逻辑网格仍为 24×16 / 32px**） | 少一层包依赖；寻路与布局完全按文档 |
| TextMeshPro | 旧版 `Text` + 系统中文字体 | TMP 中文需预烘焙字体资产 |
| 物理碰撞 | 自写网格碰撞，无 Rigidbody2D / Collider2D | 玩家不可能卡进墙；行为完全确定 |
| 单日 5~8 分钟 | 200 / 260 / 320 秒 | 与文档「10~20 分钟完整体验」对齐 |

---

## 验证状态

**诚实说明：这份代码没有在 Unity 编辑器里跑过。**

开发机上没有安装 Unity，因此采用的验证手段是：手写一套 Unity API 桩，
用 .NET 编译器对全部约 4000 行游戏代码做**真实编译校验**（0 error / 0 warning）。
这能覆盖类型错误、拼写错误、签名不匹配等绝大多数静态问题，
但**不能**覆盖运行时行为 —— 数值手感、UI 布局像素级位置、寻路死角等仍需实机验证。

首次运行时如遇问题，最可能的位置：UI 锚点数值、中文字体回退、`Sprite.Create` 的轴心。

---

## 第一版明确不做

严格遵守设计文档 §18：多人联机、开放世界、员工系统、昼夜循环、超过 4 种怪物、
超过 8 种商品、云存档、Steam 成就、随机生成地图、完整配音、战斗系统、手机适配、程序生成剧情。
