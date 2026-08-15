# 午夜怪物便利店 / Midnight Monster Mart

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

按《午夜怪物便利店 游戏原型设计文档》实现并持续迭代的 Unity 可玩原型。

2D 俯视角的**昼夜双循环**经营模拟：白天带着一支怪物小队去异世界的暮光森林打怪进货，
晚上回到便利店补货、收银、应付各路怪物顾客的规则和禁忌。没有固定天数的通关线——
店会一直开下去，员工和你自己都能在一趟趟远征里越打越强。

---

## 现在能玩到什么

- **无限连续经营**：不是三天定长的关卡，日子按内容循环下去，检查员照旧每逢第 3 天来一次
- **白天远征**：三人小队 + 玩家操作的队长，暮光森林六类房间（营地/资源/战斗/事件/精英/Boss）、
  精英护甲、区域 Boss 的孢子喷口机制、轻度肉鸽三选一强化
- **打怪升级**：员工和队长各有一条独立成长线——员工升级涨战斗数值，队长升级扩远征背包容量
- **队长手动技能**：冷却制的「拼死一击」，秒杀周围非 Boss 敌人，不吃资源
- **怪物员工双岗位**：同一只怪物可以白天出征、晚上再站收银/补货/安保，代价是疲劳翻倍累积
- **点击式收银**：不用再拖商品到判定区，点一下就扫描；收银台升级和收银岗位效率决定扫描间隔
- **营业倍速**：1x ~ 3x 可调，不想干等顾客上门时能加速
- **纯远征模式**：暂停菜单里可以直接跳过整套经营循环，只管打怪
- 四种怪物顾客各带一套喜好/禁忌规则，图鉴逐步解锁

## 运行

### 前置

- **Unity `6000.5.6f1`**（用 Unity Hub 打开时如提示升级，选同一大版本即可）
- Git LFS（二进制素材走 LFS；当前工程没有二进制素材，但配置已就位）

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

### 便利店（营业 / 闭店准备）

| 按键 | 作用 |
|---|---|
| `WASD` / 方向键 | 移动，双击方向键加速 |
| `E`（长按） | 交互：补货、扶货架、清污渍等 |
| 鼠标左键点击 | 收银时点击商品直接扫描 |
| `B` | 随时打开进货界面（营业中也能补货） |
| `Tab` | 怪物图鉴 |
| `Esc` | 暂停 / 关闭当前面板 |
| HUD 上的 `1x`~`3x` 按钮 | 营业倍速 |

### 远征（白天异世界）

| 按键 | 作用 |
|---|---|
| `WASD` | 移动队长，其余队员自动跟随并战斗 |
| `E` | 采集 / 关闭孢子喷口（脚下有喷口时优先关喷口） |
| `1` `2` `3` | 员工技能 |
| `空格` | 队长技能「拼死一击」 |
| `Q` | 轮换标记优先攻击目标 |
| `R` | 撤退，带着已获得的战利品返回 |
| `Tab` | 查看队员信息（等级 / HP / MP / 攻击力） |

---

## 一天怎么走

```
晨间排班（晨会） → 白天远征 → 闭店准备 → 午夜营业 → 日结 → 下一天的晨会……
```

1. **晨会**：看今晚的预约条线索和货架现状，决定谁出征、谁值夜班（同一只怪物可以两边都占，
   代价是连轴转、第二天效率大跌）
2. **远征**：一天只有一趟，路线固定为暮光森林；也可以在晨会选择「今天不出门」
3. **闭店准备**：把远征带回来的货和买来的货上架，无时间限制
4. **午夜营业**：顾客陆续上门，补货、收银、擦地、应付随机事件
5. **日结**：本日收支、顾客满意度、目标达成、员工疲劳一览
6. **循环**：日子按已有的三套内容模板循环，检查员固定每逢第 3 天来访；没有强制的「通关」结局

暂停菜单里另有一个**纯远征模式**入口，跳过整套经营循环，直接反复刷远征。

---

## 四种怪物顾客的规则

| 怪物 | 喜欢 | 讨厌 | 特殊规则 |
|---|---|---|---|
| 吸血鬼 | 血橙汽水 | 黑蒜面包、镜子 | 靠近装饰镜持续掉耐心；结账时可能要求黑色袋子 |
| 狼人 | 月光牛奶 | 银纸巧克力 | 耐心掉得最快；低于 20 会撞倒货架；满月夜入店即情绪警告 |
| 幽灵 | 灵魂薄荷糖 | 驱灵盐 | 拿不到实体商品，需要你送去**灵界包装台**处理再交给它；有时会忘记要买什么 |
| 史莱姆 | 发光果冻 | 干燥、被驱赶 | 走过留污渍；偶尔吞下两件商品；吃发光果冻可能分裂 |

第三天固定来访的**神秘检查员**会检查缺货、整洁度、服务事故和顾客满意度，评出 A/B/C 或停业警告。

---

## 工程结构

```
Assets/
├── Editor/SmokeTests.cs           无头冒烟测试套件（见「测试」一节）
├── Scenes/Boot.unity              空场景，仅作为构建入口
└── Scripts/
    ├── Art/SpriteFactory.cs       所有美术资源的程序化生成（顾客/怪物/商品图标等全部代码画像素）
    ├── Audio/AudioDirector.cs     所有音效与背景音的波形合成
    ├── Core/
    │   ├── GameBootstrap.cs       入口：搭出整个游戏
    │   ├── Game.cs                服务定位器
    │   ├── GameManager.cs         昼夜双循环总状态机 + 纯远征模式
    │   ├── DayManager.cs          营业日、波次、结算数据、DayPlan 循环复用
    │   ├── StaffRoster.cs         怪物员工排班（出征 / 夜班）与疲劳
    │   ├── CaptainProgress.cs     队长（玩家）成长线：经验/等级/背包容量
    │   ├── ExpeditionProgress.cs  远征侧的跨天进度（冷藏货架核心、地区解锁）
    │   ├── StoreMetrics.cs        金钱 / 声望 / 整洁度
    │   ├── StoreGrid.cs           24×16 逻辑网格与碰撞
    │   ├── Pathfinder.cs          八方向 A*
    │   └── InputReader.cs         输入抽象层
    ├── Customer/                  顾客有限状态机、怪物行为组合、波次生成
    ├── Combat/                    Health、EnemyController（伤害管线：护甲、Boss 护盾）
    ├── Staff/StaffFollower.cs     远征中的怪物员工：自动战斗、技能、打怪升级
    ├── Expedition/                远征世界、队长、房间、采集点、孢子喷口、战利品
    ├── Data/                      ScriptableObject 定义 + GameConfig + GameDatabase（运行时构造）
    ├── Events/RandomEventManager.cs   随机事件 + 检查员评级
    ├── Player/                    便利店内玩家的移动、碰撞、交互、携带
    ├── Save/SaveSystem.cs         persistentDataPath JSON 存档（纯新增字段，向后兼容旧存档）
    ├── Store/                     网格世界、货架、收银台、仓库、灵界台、镜子、污渍
    └── UI/                        程序化 uGUI：HUD / 晨会 / 进货 / 收银 / 远征 / 结算 / 图鉴 / 弹窗
```

数值调参集中在 [`Assets/Scripts/Data/GameConfig.cs`](Assets/Scripts/Data/GameConfig.cs)，
商品 / 怪物 / 员工 / 敌人 / 房间 / DayPlan 全部集中在
[`Assets/Scripts/Data/GameDatabase.cs`](Assets/Scripts/Data/GameDatabase.cs)。

---

## 测试

工程没有装 `com.unity.test-framework`，也没有 asmdef，测试走的是自建的无头方案：
[`Assets/Editor/SmokeTests.cs`](Assets/Editor/SmokeTests.cs) 用
`-executeMethod MonsterMart.EditorTools.SmokeTests.RunAll` 在批处理模式下跑完整套用例，
用退出码表示成败（0 = 全过，1 = 有失败）。

```bash
"<Unity 安装路径>/Editor/Unity.exe" \
  -batchmode -nographics \
  -projectPath "<工程路径>" \
  -executeMethod MonsterMart.EditorTools.SmokeTests.RunAll \
  -logFile "<日志路径>"
```

覆盖内容包括三天/无限循环流程、存档兼容性、远征战斗（精英护甲、Boss 区域机制、打怪升级）、
排班与疲劳、收银与货架交互、UI 面板搭建等。每次改动之后也会做变异验证
（故意把修复改回错误实现，确认对应用例真的失败），确保用例不是摆设。

---

## 与设计文档的偏离

详见 [DEVIATIONS.md](DEVIATIONS.md)，逐条记录了从早期技术选型偏离（内置渲染管线代替 URP、
旧版 Input Manager 代替 Input System 等）到本次玩法迭代（无限连续经营、远征战斗子系统、
点击式收银、员工双岗位与成长线）的所有对照说明。

---

## License

[MIT](LICENSE) © Zane Yuan
