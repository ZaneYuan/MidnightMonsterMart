using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Combat;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Staff;
using MonsterMart.UI;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 白天异世界进货 — 设计文档 §3。
    ///
    /// 覆盖 §18 第一、二阶段：三人小队、暮光森林六个房间、采集点、事件房、
    /// 精英与区域 Boss（含孢子喷口这套区域机制）、轻度肉鸽三选一。
    /// 战利品直接结算进便利店仓库 —— 这条链路正是 §22 说的那条最小闭环：
    /// 击败怪物获得血橙汽水 → 带回店里上架 → 吸血鬼购买。
    ///
    /// 尚未实现（属第四阶段及以后）：员工疲劳与信任、店内岗位分配、
    /// 多地区（灰烬火山等只是打了解锁标记，还没有对应路线）。
    /// </summary>
    public class ExpeditionManager : MonoBehaviour
    {
        public ExpeditionWorld World { get; private set; }
        public ExpeditionCaptain Captain { get; private set; }

        public bool IsRunning { get; private set; }
        public ExpeditionOutcome Outcome { get; private set; } = ExpeditionOutcome.None;

        readonly List<StaffFollower> _squad = new List<StaffFollower>();
        readonly List<EnemyController> _enemies = new List<EnemyController>();
        readonly List<LootPickup> _loot = new List<LootPickup>();
        readonly List<HarvestNode> _nodes = new List<HarvestNode>();
        readonly List<SporeVent> _vents = new List<SporeVent>();
        readonly List<ExpeditionActor> _allies = new List<ExpeditionActor>();
        readonly Dictionary<ProductData, int> _bag = new Dictionary<ProductData, int>();

        public IReadOnlyList<StaffFollower> Squad => _squad;
        public IReadOnlyList<EnemyController> Enemies => _enemies;

        /// <summary>队长 + 存活员工，敌人用它来选目标。</summary>
        public IReadOnlyList<ExpeditionActor> Allies => _allies;

        /// <summary>还躺在地上没被捡起来的战利品堆数。</summary>
        public int LootOnGround => _loot.Count;

        /// <summary>上阵人数 — 设计文档 §3.3「上阵 3 名怪物员工」。</summary>
        public const int SquadSize = 3;

        /// <summary>
        /// 玩家标记的优先攻击目标 — 设计文档 §3.2「目标标记：优先攻击指定敌人」。
        /// 目标死掉或远征结束会自动清空。
        /// </summary>
        public EnemyController MarkedTarget { get; private set; }

        Transform _root;        // 整趟远征都在，队长和员工挂这里
        Transform _roomRoot;    // 每换一个房间就拆掉重建

        readonly List<RoomData> _route = new List<RoomData>();
        int _roomIndex = -1;

        /// <summary>当前房间（没在远征时为 null）。</summary>
        public RoomData CurrentRoom =>
            _roomIndex >= 0 && _roomIndex < _route.Count ? _route[_roomIndex] : null;

        public int RoomIndex => _roomIndex;
        public int RoomCount => _route.Count;

        /// <summary>清场了吗 —— 清场后传送点才会亮。</summary>
        public bool RoomCleared => EnemiesRemaining == 0;

        public bool IsLastRoom => _roomIndex >= _route.Count - 1;

        /// <summary>队长站在传送点上了吗。</summary>
        public bool CaptainAtExit
        {
            get
            {
                if (Captain == null || World == null) return false;
                var exit = StoreGrid.CellToWorld(World.ExitCell);
                return (exit - Captain.Position).magnitude <= ExitRadius;
            }
        }

        const float ExitRadius = 0.9f;

        // ------------------------------------------------------------------
        // 查询
        // ------------------------------------------------------------------
        /// <summary>携带容量基础值，队长升级（CaptainProgress）会在这基础上往上加。</summary>
        public const int BaseBagCapacity = 20;

        /// <summary>
        /// 实际携带容量 — 设计文档 §3.2 / §12.2 都把它列为远征界面必须显示的信息，
        /// §3.3 的取舍「保留订单商品、畅销商品还是稀有升级材料」正建立在它之上。
        /// 打怪升级：队长每升一级多背 <see cref="CaptainProgress.CapacityPerLevel"/> 件。
        /// </summary>
        public static int BagCapacity => BaseBagCapacity + CaptainProgress.CapacityBonus;

        public int BagCount
        {
            get
            {
                int n = 0;
                foreach (var pair in _bag) n += pair.Value;
                return n;
            }
        }

        public int BagSpaceLeft => Mathf.Max(0, BagCapacity - BagCount);
        public bool BagFull => BagSpaceLeft <= 0;

        /// <summary>本房间还没采完的采集点。</summary>
        public IReadOnlyList<HarvestNode> HarvestNodes => _nodes;

        // ------------------------------------------------------------------
        // Boss 区域机制 — 设计文档 §3.3「Boss 通过区域机制、护送商品或关闭装置制造变化」
        // ------------------------------------------------------------------
        /// <summary>本房间的孢子喷口。只有 Boss 房有。</summary>
        public IReadOnlyList<SporeVent> Vents => _vents;

        public int OpenVentCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _vents.Count; i++)
                    if (_vents[i] != null && _vents[i].IsOpen) n++;
                return n;
            }
        }

        /// <summary>喷口全关掉了吗 —— 也就是 Boss 正处在破防窗口。</summary>
        public bool VentsAllClosed => _vents.Count > 0 && OpenVentCount == 0;

        /// <summary>距离喷口重新喷发还有多久（没在破防窗口里就是 0）。</summary>
        public float VentReopenCountdown => _ventReopenTimer;

        float _ventReopenTimer;
        EnemyController _boss;

        /// <summary>本房间的 Boss（不是 Boss 房就是 null）。</summary>
        public EnemyController Boss => _boss != null && _boss.IsAlive ? _boss : null;

        // ------------------------------------------------------------------
        // 轻度肉鸽三选一 — 设计文档 §3.6
        //
        // §3.6 明确「仅在本次远征生效」，所以强化只存在这个列表里：
        // 不进存档、不挂在员工数据上，Begin() 一清就真的没了。
        // ------------------------------------------------------------------
        readonly List<ExpeditionBoonData> _boons = new List<ExpeditionBoonData>();

        /// <summary>本次远征已经拿到的强化。</summary>
        public IReadOnlyList<ExpeditionBoonData> Boons => _boons;

        public bool HasBoon(string boonId)
        {
            for (int i = 0; i < _boons.Count; i++)
                if (_boons[i] != null && _boons[i].boonId == boonId) return true;
            return false;
        }

        /// <summary>普通敌人掉落倍率 —— 批发契约。</summary>
        public float NormalLootMultiplier => Product(b => b.normalLootMultiplier);

        /// <summary>Boss 掉落倍率 —— 批发契约的代价。</summary>
        public float BossLootMultiplier => Product(b => b.bossLootMultiplier);

        /// <summary>技能冷却倍率 —— 加班狂热。</summary>
        public float SkillCooldownMultiplier => Product(b => b.skillCooldownMultiplier);

        /// <summary>每次放技能的自损 —— 加班狂热的代价。</summary>
        public float SkillSelfDamage => Sum(b => b.skillSelfDamage);

        /// <summary>史莱姆员工的攻击倍率 —— 史莱姆快递的代价。</summary>
        public float SlimeAttackMultiplier => Product(b => b.slimeAttackMultiplier);

        /// <summary>队长移动速度倍率 —— 易碎品保险的代价。</summary>
        public float CaptainSpeedMultiplier => Product(b => b.captainSpeedMultiplier);

        /// <summary>拾取半径 —— 史莱姆快递把它撑大。</summary>
        public float PickupRadius =>
            ExpeditionCaptain.PickupRadius * Product(b => b.pickupRadiusMultiplier);

        /// <summary>被击退时保留多少战利品（§3.7 基线 0.5，易碎品保险往上抬）。</summary>
        public float FailKeepRatio =>
            Mathf.Clamp01(BaseFailKeepRatio + Sum(b => b.failKeepRatioBonus));

        /// <summary>§3.7「失败……损失部分易碎商品」的基线保留率。</summary>
        public const float BaseFailKeepRatio = 0.5f;

        float Product(System.Func<ExpeditionBoonData, float> pick)
        {
            float v = 1f;
            for (int i = 0; i < _boons.Count; i++)
                if (_boons[i] != null) v *= pick(_boons[i]);
            return v;
        }

        float Sum(System.Func<ExpeditionBoonData, float> pick)
        {
            float v = 0f;
            for (int i = 0; i < _boons.Count; i++)
                if (_boons[i] != null) v += pick(_boons[i]);
            return v;
        }

        /// <summary>拿下一个强化。返回是否真的加上了（重复的不再叠）。</summary>
        public bool TakeBoon(ExpeditionBoonData boon)
        {
            if (boon == null || HasBoon(boon.boonId)) return false;

            _boons.Add(boon);
            Game.UI?.Hud?.Flash($"获得强化「{boon.displayName}」");
            Game.Audio?.PlaySpirit();
            return true;
        }

        /// <summary>
        /// 抽一批三选一的候选 —— 已经拿过的不再出现，池子不够就有几个给几个。
        /// 抽签和弹窗分开，无头用例才能验证「候选互不重复」这件事本身。
        /// </summary>
        public List<ExpeditionBoonData> RollBoonChoices(int count = GameDatabase.BoonChoiceCount)
        {
            var pool = new List<ExpeditionBoonData>();
            var all = GameDatabase.Boons;
            for (int i = 0; i < all.Count; i++)
                if (!HasBoon(all[i].boonId)) pool.Add(all[i]);

            var picked = new List<ExpeditionBoonData>();
            while (picked.Count < count && pool.Count > 0)
            {
                int index = Random.Range(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }

        /// <summary>进到打了勾的房间时弹一次三选一（§3.6）。</summary>
        void OfferBoons(RoomData room)
        {
            if (room == null || !room.offersBoon) return;

            var choices = RollBoonChoices();
            if (choices.Count == 0) return;

            if (Game.UI == null) return;   // 无头环境下由测试直接调 TakeBoon

            var options = new ChoiceOption[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                var boon = choices[i];
                options[i] = new ChoiceOption(boon.displayName,
                                              $"{boon.benefit}\n代价：{boon.cost}",
                                              () => TakeBoon(boon));
            }

            Game.UI.ShowChoice("补给箱：挑一个带走",
                               "只在这一趟远征里生效。每一条都有代价，别只看收益。",
                               options);
        }

        public int EnemiesRemaining
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _enemies.Count; i++)
                    if (_enemies[i] != null && _enemies[i].IsAlive) n++;
                return n;
            }
        }

        public bool SquadWiped
        {
            get
            {
                if (Captain != null && Captain.IsAlive) return false;
                for (int i = 0; i < _squad.Count; i++)
                    if (_squad[i] != null && _squad[i].IsAlive) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------
        // 开始 / 结束
        // ------------------------------------------------------------------
        /// <summary>出发。staffIds 为空时按数据库默认队伍。</summary>
        public void Begin(params string[] staffIds)
        {
            if (IsRunning) return;

            Teardown();

            IsRunning = true;
            Outcome = ExpeditionOutcome.None;
            _bag.Clear();
            _boons.Clear();          // §3.6「仅在本次远征生效」
            _bossCoresThisRun = 0;
            _unlockedRegionThisRun = null;

            _route.Clear();
            _route.AddRange(GameDatabase.TwilightForest);

            var rootGo = new GameObject("ExpeditionRoot");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = ExpeditionWorld.WorldOffset;
            _root = rootGo.transform;

            // 队长和员工整趟都在，血量与技能冷却跨房间保留
            SpawnCaptain();
            SpawnSquad(staffIds);

            EnterRoom(0);

            Game.Manager.EnterExpedition();

            if (Game.Camera != null)
            {
                Game.Camera.SetBounds(World.BoundsMin, World.BoundsMax);
                Game.Camera.target = Captain.transform;
            }

            Game.UI?.ShowExpedition();
        }

        // ------------------------------------------------------------------
        // 房间 — 设计文档 §3.4
        // ------------------------------------------------------------------
        void EnterRoom(int index)
        {
            if (index < 0 || index >= _route.Count) return;

            ClearMarkedTarget();
            TeardownRoom();

            _roomIndex = index;
            var room = _route[index];

            var roomGo = new GameObject("Room_" + room.roomId);
            roomGo.transform.SetParent(_root, false);
            _roomRoot = roomGo.transform;

            World = roomGo.AddComponent<ExpeditionWorld>();
            World.Build(room);

            PlaceSquadAtEntrance();
            SpawnEnemies(room);
            SpawnVents(room);
            SpawnHarvestNodes(room);

            World.SetExitOpen(RoomCleared);

            Game.UI?.Hud?.Flash($"进入「{room.displayName}」（{index + 1}/{_route.Count}）");

            OfferBoons(room);
            TriggerRoomEvent(room);
        }

        /// <summary>
        /// 采集点之间必须隔开的格数。
        ///
        /// 只做「不在同一格」是不够的：采集半径是 1.2 格，两个采集点挨着的话
        /// 站在其中一个上面按 E 会随机收到另一个，玩家没法指定采哪一堆，
        /// 「路线与携带容量」的取舍（§3.4 资源房）也就没了。
        /// </summary>
        const int HarvestNodeSpacing = 3;

        void SpawnHarvestNodes(RoomData room)
        {
            if (room == null || !room.HasHarvest) return;

            var taken = new List<Vector2Int>();

            for (int i = 0; i < room.harvestProductIds.Count; i++)
            {
                var product = GameDatabase.GetProduct(room.harvestProductIds[i]);
                if (product == null)
                {
                    Debug.LogError($"[Expedition] 房间 {room.roomId} 的采集点引用了不存在的商品 {room.harvestProductIds[i]}");
                    continue;
                }

                Vector2Int cell = default;
                bool found = false;
                for (int attempt = 0; attempt < 48 && !found; attempt++)
                {
                    cell = World.RandomWalkableCell(4);
                    found = FarFromAll(cell, taken, HarvestNodeSpacing);
                }
                if (!found) continue;

                taken.Add(cell);

                var go = new GameObject("Harvest_" + product.productId);
                go.transform.SetParent(_roomRoot, false);

                var node = go.AddComponent<HarvestNode>();
                node.Initialize(product, room.harvestPerNode, StoreGrid.CellToWorld(cell));

                _nodes.Add(node);
            }
        }

        static bool FarFromAll(Vector2Int cell, List<Vector2Int> others, int spacing)
        {
            for (int i = 0; i < others.Count; i++)
                if ((others[i] - cell).sqrMagnitude < spacing * spacing) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // 事件房 — 设计文档 §3.4
        // ------------------------------------------------------------------
        void TriggerRoomEvent(RoomData room)
        {
            if (room == null || !room.HasEvent) return;

            var data = GameDatabase.GetExpeditionEvent(room.eventId);
            if (data == null)
            {
                Debug.LogError($"[Expedition] 房间 {room.roomId} 引用了不存在的事件 {room.eventId}");
                return;
            }

            if (Game.UI == null) return;   // 无头环境下由测试直接调 ApplyEventOption

            var options = new ChoiceOption[data.options.Count];
            for (int i = 0; i < data.options.Count; i++)
            {
                var option = data.options[i];
                options[i] = new ChoiceOption(option.label, option.detail,
                                              () => ApplyEventOption(option));
            }

            Game.UI.ShowChoice(data.title, data.body, options);
        }

        /// <summary>结算一个事件房选项。返回是否真的生效（交易钱不够会失败）。</summary>
        public bool ApplyEventOption(ExpeditionEventOption option)
        {
            if (option == null) return false;

            switch (option.effect)
            {
                case ExpeditionEventEffect.Trade:
                    if (Game.Economy == null || !Game.Economy.TrySpend(option.coinCost, true))
                    {
                        Game.UI?.Hud?.Flash("金币不够，行商摇了摇头");
                        Game.Audio?.PlayError();
                        return false;
                    }
                    GrantEventGoods(option);
                    return true;

                case ExpeditionEventEffect.Scavenge:
                    GrantEventGoods(option);
                    DamageSquad(option.squadDamage);
                    return true;

                default:
                    return true;
            }
        }

        void GrantEventGoods(ExpeditionEventOption option)
        {
            var product = GameDatabase.GetProduct(option.productId);
            if (product == null || option.productCount <= 0) return;

            int added = AddToBag(product, option.productCount);
            Game.UI?.Hud?.Flash(added < option.productCount
                ? $"背包只装得下 {added} 件 {product.displayName}"
                : $"获得 {product.displayName} ×{added}");
        }

        void DamageSquad(float amount)
        {
            if (amount <= 0f) return;

            for (int i = 0; i < _allies.Count; i++)
            {
                var actor = _allies[i];
                if (actor == null || !actor.IsAlive) continue;
                actor.Health.Damage(amount);
            }
        }

        void PlaceSquadAtEntrance()
        {
            if (Captain != null) Captain.TeleportTo(World.CampCell);

            for (int i = 0; i < _squad.Count; i++)
                if (_squad[i] != null) _squad[i].TeleportTo(World.CampCell);
        }

        /// <summary>踩上亮着的传送点 → 下一个房间；已经是最后一间就收队。</summary>
        public void AdvanceRoom()
        {
            if (!IsRunning || !RoomCleared) return;

            if (IsLastRoom) { Finish(ExpeditionOutcome.Cleared); return; }

            EnterRoom(_roomIndex + 1);
        }

        void TeardownRoom()
        {
            for (int i = 0; i < _enemies.Count; i++)
                if (_enemies[i] != null) Lifetime.Destroy(_enemies[i].gameObject);
            _enemies.Clear();

            for (int i = 0; i < _loot.Count; i++)
                if (_loot[i] != null) Lifetime.Destroy(_loot[i].gameObject);
            _loot.Clear();

            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i] != null) Lifetime.Destroy(_nodes[i].gameObject);
            _nodes.Clear();

            for (int i = 0; i < _vents.Count; i++)
                if (_vents[i] != null) Lifetime.Destroy(_vents[i].gameObject);
            _vents.Clear();
            _ventReopenTimer = 0f;
            _boss = null;

            if (_roomRoot != null) Lifetime.Destroy(_roomRoot.gameObject);
            _roomRoot = null;
            World = null;
        }

        void SpawnCaptain()
        {
            var go = new GameObject("Captain");
            go.transform.SetParent(_root, false);

            // 具体落点由 EnterRoom 决定，这里先给个安全格
            Captain = go.AddComponent<ExpeditionCaptain>();
            Captain.Initialize(Vector2Int.one * ExpeditionWorld.WallThickness);

            _allies.Add(Captain);
        }

        void SpawnSquad(string[] staffIds)
        {
            if (staffIds == null || staffIds.Length == 0)
                staffIds = GameDatabase.DefaultSquad;

            // §3.3：上阵 3 名，多给的忽略掉
            int count = Mathf.Min(staffIds.Length, SquadSize);

            for (int i = 0; i < count; i++)
            {
                var data = GameDatabase.GetStaff(staffIds[i]);
                if (data == null)
                {
                    Debug.LogError($"[Expedition] 找不到员工 {staffIds[i]}");
                    continue;
                }

                var go = new GameObject("Staff_" + data.staffId);
                go.transform.SetParent(_root, false);

                var follower = go.AddComponent<StaffFollower>();
                follower.Initialize(data, _squad.Count, Vector2Int.one * ExpeditionWorld.WallThickness);

                _squad.Add(follower);
                _allies.Add(follower);
            }
        }

        void SpawnEnemies(RoomData room)
        {
            if (room == null) return;

            // 刷新点去重：两个敌人叠在同一格会完全重合，
            // 既看不清也没法用目标标记区分。
            var taken = new HashSet<Vector2Int>();

            if (room.HasEnemies) SpawnEnemyGroup(room, room.enemyId, room.enemyCount, taken);
            if (room.HasMinions) SpawnEnemyGroup(room, room.minionEnemyId, room.minionCount, taken);
        }

        void SpawnEnemyGroup(RoomData room, string enemyId, int count, HashSet<Vector2Int> taken)
        {
            var data = GameDatabase.GetEnemy(enemyId);
            if (data == null)
            {
                Debug.LogError($"[Expedition] 房间 {room.roomId} 引用了不存在的敌人 {enemyId}");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2Int cell;

                if (data.tier == EnemyTier.Boss)
                {
                    // Boss 固定站在房间正中：孢子喷口是围着它摆的，
                    // 随机落点会让「绕圈关装置」的节奏时紧时松。
                    cell = World.Grid.NearestWalkable(World.CenterCell);
                    taken.Add(cell);
                }
                else
                {
                    bool found = false;
                    cell = default;

                    for (int attempt = 0; attempt < 24 && !found; attempt++)
                    {
                        cell = World.RandomWalkableCell();
                        found = taken.Add(cell);
                    }
                    if (!found) continue;
                }

                var go = new GameObject($"Enemy_{data.enemyId}_{i}");
                go.transform.SetParent(_roomRoot, false);

                var enemy = go.AddComponent<EnemyController>();
                enemy.Initialize(data, cell);

                _enemies.Add(enemy);

                if (enemy.IsBoss) _boss = enemy;
            }
        }

        // ------------------------------------------------------------------
        // 孢子喷口 — 设计文档 §3.3「Boss 通过……关闭装置制造变化」
        // ------------------------------------------------------------------
        /// <summary>
        /// Boss 一进场就把喷口摆开。位置是围着 Boss 均分的一圈固定点，
        /// 不用随机 —— 玩家要能一眼看清「要跑几趟」，随机分布只会制造噪声。
        /// </summary>
        void SpawnVents(RoomData room)
        {
            if (_boss == null || _boss.Data == null || !_boss.Data.UsesSporeVents) return;

            var data = _boss.Data;
            var center = StoreGrid.CellToWorld(World.CenterCell);
            float radius = Mathf.Min(ExpeditionWorld.RoomWidth, ExpeditionWorld.RoomHeight) * 0.28f;

            for (int i = 0; i < data.ventCount; i++)
            {
                float angle = Mathf.PI * 0.5f + i * (Mathf.PI * 2f / data.ventCount);
                var wanted = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                var cell = World.Grid.NearestWalkable(StoreGrid.WorldToCell(wanted));

                var go = new GameObject($"SporeVent_{i}");
                go.transform.SetParent(_roomRoot, false);

                var vent = go.AddComponent<SporeVent>();
                vent.Initialize(StoreGrid.CellToWorld(cell), data);

                _vents.Add(vent);
            }

            _ventReopenTimer = 0f;

            Game.UI?.Hud?.Flash(
                $"{data.displayName} 张开了 {data.ventCount} 个孢子喷口 —— 走到喷口上按 E 关掉它");
        }

        /// <summary>
        /// 推进区域机制：喷口灼伤 + 破防窗口倒计时。
        /// 从 Update 拆出来是为了让无头用例能驱动真实逻辑（见「可测试性抽取模式」）。
        /// </summary>
        public void TickBossArena(float dt)
        {
            if (_vents.Count == 0 || dt <= 0f) return;

            for (int i = 0; i < _vents.Count; i++)
                if (_vents[i] != null) _vents[i].TickPulse(dt);

            // Boss 死了就不再喷发，免得清完场还在挨伤害
            if (_boss == null || !_boss.IsAlive)
            {
                CloseAllVents();
                return;
            }

            if (OpenVentCount > 0) return;

            // 破防窗口：全关之后过一段时间重新喷发，逼玩家再跑一轮
            _ventReopenTimer -= dt;
            if (_ventReopenTimer > 0f) return;

            ReopenVents();
        }

        void ReopenVents()
        {
            for (int i = 0; i < _vents.Count; i++)
                if (_vents[i] != null) _vents[i].Open();

            _ventReopenTimer = 0f;
            Game.UI?.Hud?.Flash("孢子喷口重新喷发了 —— 再关一次！");
            Game.Audio?.PlayError();
        }

        void CloseAllVents()
        {
            for (int i = 0; i < _vents.Count; i++)
                if (_vents[i] != null) _vents[i].Close();

            _ventReopenTimer = 0f;
        }

        /// <summary>队长脚下还开着的喷口（没有就返回 null）。</summary>
        public SporeVent VentInReach()
        {
            if (Captain == null) return null;

            for (int i = 0; i < _vents.Count; i++)
            {
                var vent = _vents[i];
                if (vent == null || !vent.IsOpen) continue;
                if (vent.InRange(Captain.Position)) return vent;
            }
            return null;
        }

        /// <summary>关掉队长脚下的喷口。返回是否真的关掉了一个。</summary>
        public bool CloseVentInReach()
        {
            var vent = VentInReach();
            if (vent == null) return false;

            vent.Close();
            Game.Audio?.PlayPickup();

            int open = OpenVentCount;
            if (open > 0)
            {
                Game.UI?.Hud?.Flash($"关闭了一个孢子喷口，还剩 {open} 个");
                return true;
            }

            // 全关 → 进入破防窗口
            _ventReopenTimer = _boss != null && _boss.Data != null
                ? _boss.Data.ventReopenSeconds
                : 0f;

            string bossName = _boss != null && _boss.Data != null ? _boss.Data.displayName : "Boss";
            Game.UI?.Hud?.Flash(
                $"喷口全部关闭！{bossName} 护盾消失，{_ventReopenTimer:0} 秒内全力输出");
            Game.Audio?.PlaySpirit();
            return true;
        }

        /// <summary>
        /// E 键 — 设计文档 §3.2「交互键：采集、开箱、救援、进入传送点」。
        /// 喷口优先于采集：Boss 战里正被灼伤时，玩家按 E 想要的一定是关装置。
        /// </summary>
        public bool Interact()
        {
            if (CloseVentInReach()) return true;
            return HarvestInReach() > 0;
        }

        void Update()
        {
            if (!IsRunning) return;
            if (Game.Manager == null || Game.Manager.State != GameState.Expedition) return;

            HandleSkillHotkeys();
            TickBossArena(Time.deltaTime);

            if (InputReader.MarkTargetPressed) MarkNextTarget();
            if (InputReader.RetreatPressed) { Retreat(); return; }

            // 目标死了就撤掉标记，免得小队一直盯着尸体
            if (MarkedTarget != null && !MarkedTarget.IsAlive) ClearMarkedTarget();

            if (SquadWiped) { Finish(ExpeditionOutcome.Failed); return; }

            // 清场后传送点亮起，队长踩上去进下一间（最后一间就是收队）
            World?.SetExitOpen(RoomCleared);
            if (RoomCleared && CaptainAtExit) AdvanceRoom();
        }

        void HandleSkillHotkeys()
        {
            if (Game.UI != null && Game.UI.BlocksWorldInput) return;

            int index = InputReader.SkillHotkey;
            if (index < 0 || index >= _squad.Count) return;

            var member = _squad[index];
            if (member == null) return;

            if (!member.TryUseSkill())
                Game.UI?.Hud?.Flash($"{member.Data.displayName} 的技能还在冷却");
        }

        // ------------------------------------------------------------------
        // 目标标记 — 设计文档 §3.2
        // ------------------------------------------------------------------
        /// <summary>
        /// 在存活敌人之间轮换标记。按一次标下一个，转完一圈回到「不标记」，
        /// 这样同一个键既能指定目标也能取消。
        /// </summary>
        public EnemyController MarkNextTarget()
        {
            int start = MarkedTarget != null ? _enemies.IndexOf(MarkedTarget) + 1 : 0;

            for (int step = 0; step < _enemies.Count; step++)
            {
                var candidate = _enemies[(start + step) % _enemies.Count];
                if (candidate == null || !candidate.IsAlive) continue;
                if (candidate == MarkedTarget) continue;

                SetMarkedTarget(candidate);
                return candidate;
            }

            ClearMarkedTarget();
            return null;
        }

        public void SetMarkedTarget(EnemyController enemy)
        {
            if (MarkedTarget != null) MarkedTarget.SetMarked(false);

            MarkedTarget = enemy != null && enemy.IsAlive ? enemy : null;

            if (MarkedTarget != null)
            {
                MarkedTarget.SetMarked(true);
                Game.UI?.Hud?.Flash($"优先攻击：{MarkedTarget.Data.displayName}");
            }
        }

        public void ClearMarkedTarget()
        {
            if (MarkedTarget != null) MarkedTarget.SetMarked(false);
            MarkedTarget = null;
        }

        // ------------------------------------------------------------------
        // 战利品
        // ------------------------------------------------------------------
        public void OnEnemyDefeated(EnemyController enemy)
        {
            if (enemy == null || enemy.Data == null) return;

            if (enemy.IsBoss) GrantBossReward(enemy.Data);

            AwardExpeditionXp(enemy.Data);
            AwardExpeditionCoins(enemy.Data);

            var product = GameDatabase.GetProduct(enemy.Data.lootProductId);
            if (product == null) return;

            int count = Random.Range(enemy.Data.lootMin, enemy.Data.lootMax + 1);

            // §3.6 批发契约：普通掉落翻倍，代价是 Boss 奖励品质下降
            float multiplier = enemy.IsBoss ? BossLootMultiplier : NormalLootMultiplier;
            if (!Mathf.Approximately(multiplier, 1f))
                count = Mathf.Max(1, Mathf.RoundToInt(count * multiplier));

            if (count <= 0) return;

            var go = new GameObject("Loot_" + product.productId);
            go.transform.SetParent(_roomRoot, false);

            var loot = go.AddComponent<LootPickup>();
            loot.Initialize(product, count, enemy.Position);
            _loot.Add(loot);

            Game.UI?.Hud?.Flash($"击败了 {enemy.Data.displayName}，掉落 {product.displayName} ×{count}");
        }

        /// <summary>击败后直接掉金币，独立于商品掉落 —— 哪怕这只敌人没配商品也照掉。</summary>
        void AwardExpeditionCoins(EnemyData data)
        {
            if (data == null || data.coinMax <= 0) return;

            int coins = Random.Range(data.coinMin, data.coinMax + 1);
            if (coins <= 0) return;

            Game.Economy?.AddExpeditionCoins(coins);
            Game.UI?.Hud?.Flash($"捡到 {coins} 金币");
        }

        /// <summary>
        /// 打怪升级：经验按存活战斗单位均分（队长 + 存活员工），不看是谁补的最后一刀 ——
        /// 普攻大半是自动的，把经验系在「谁出征了」比系在「谁抢到了尾刀」更公平。
        /// 队长走的是 CaptainProgress 那条独立的线（升级扩背包，不是加战斗数值）。
        /// </summary>
        void AwardExpeditionXp(EnemyData data)
        {
            if (data == null || data.xpReward <= 0f) return;

            bool captainAlive = Captain != null && Captain.IsAlive;

            int alive = captainAlive ? 1 : 0;
            for (int i = 0; i < _squad.Count; i++)
                if (_squad[i] != null && _squad[i].IsAlive) alive++;
            if (alive == 0) return;

            float share = data.xpReward / alive;

            if (captainAlive && CaptainProgress.AddXp(share))
                Game.UI?.Hud?.Flash($"你升到了 Lv.{CaptainProgress.Level}！携带容量变成了 {BagCapacity}");

            for (int i = 0; i < _squad.Count; i++)
            {
                var follower = _squad[i];
                if (follower == null || !follower.IsAlive || follower.Data == null) continue;

                if (!StaffRoster.AddXp(follower.Data.staffId, share)) continue;

                var entry = StaffRoster.Get(follower.Data.staffId);
                int level = entry != null ? entry.level : 0;
                Game.UI?.Hud?.Flash($"{follower.Data.displayName} 升到了 Lv.{level}！");
                Game.Audio?.PlaySpirit();
            }
        }

        /// <summary>
        /// Boss 奖励 — 设计文档 §3.4「Boss 房：结算区域机制，掉落关键设施材料
        /// 并解锁下一地区」，§3.5「击败后获得冷藏货架核心」。
        ///
        /// 关键设施材料<b>不进背包</b>：它不是货，不受携带容量限制，
        /// 也不该在 §3.7 的失败折损里被砍掉一半 —— 打赢了就是打赢了。
        /// </summary>
        void GrantBossReward(EnemyData data)
        {
            if (data.coldShelfCores > 0)
            {
                ExpeditionProgress.AddColdShelfCores(data.coldShelfCores);
                _bossCoresThisRun += data.coldShelfCores;
                Game.UI?.Hud?.Flash($"获得冷藏货架核心 ×{data.coldShelfCores}");
            }

            if (ExpeditionProgress.UnlockRegion(data.unlocksRegionId))
            {
                _unlockedRegionThisRun = string.IsNullOrEmpty(data.unlocksRegionName)
                    ? data.unlocksRegionId
                    : data.unlocksRegionName;
                Game.UI?.Hud?.Flash($"解锁了新的供货区域：{_unlockedRegionThisRun}");
            }

            CloseAllVents();
            Game.Audio?.PlaySpirit();
        }

        /// <summary>本趟远征打下来的 Boss 奖励，用于收队弹窗的文案。</summary>
        int _bossCoresThisRun;
        string _unlockedRegionThisRun;

        /// <summary>队长走近时自动拾取。</summary>
        public void TryPickupNear(ExpeditionCaptain captain)
        {
            if (captain == null) return;

            for (int i = _loot.Count - 1; i >= 0; i--)
            {
                var loot = _loot[i];
                if (loot == null) { _loot.RemoveAt(i); continue; }

                // §3.6 史莱姆快递会把这个半径撑大
                if ((loot.Position - captain.Position).magnitude > PickupRadius)
                    continue;

                // 背包满了就先留在地上，等卸完货再来捡
                int added = AddToBag(loot.Product, loot.Count);
                if (added <= 0) continue;

                Game.Audio?.PlayPickup();
                Game.UI?.Hud?.Flash(
                    $"拾取 {loot.Product.displayName} ×{added}（背包 {BagCount}/{BagCapacity}）");

                _loot.RemoveAt(i);
                Lifetime.Destroy(loot.gameObject);
            }
        }

        /// <summary>往背包里放东西，受携带容量限制。返回实际放进去的数量。</summary>
        public int AddToBag(ProductData product, int count)
        {
            if (product == null || count <= 0) return 0;

            int added = Mathf.Min(count, BagSpaceLeft);
            if (added <= 0) return 0;

            _bag[product] = (_bag.TryGetValue(product, out int n) ? n : 0) + added;
            return added;
        }

        // ------------------------------------------------------------------
        // 采集 — 设计文档 §3.2「交互键：采集」
        // ------------------------------------------------------------------
        /// <summary>队长附近还能采的采集点（没有就返回 null）。</summary>
        public HarvestNode HarvestNodeInReach()
        {
            if (Captain == null) return null;

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node == null || node.IsEmpty) continue;
                if (node.InRange(Captain.Position)) return node;
            }
            return null;
        }

        /// <summary>采一次。返回实际采到的数量。</summary>
        public int HarvestInReach()
        {
            var node = HarvestNodeInReach();
            if (node == null) return 0;

            if (BagFull)
            {
                Game.UI?.Hud?.Flash($"背包满了（{BagCount}/{BagCapacity}），先撤回去卸货");
                Game.Audio?.PlayError();
                return 0;
            }

            int taken = node.Harvest(BagSpaceLeft);
            if (taken <= 0) return 0;

            AddToBag(node.Product, taken);
            Game.Audio?.PlayPickup();

            Game.UI?.Hud?.Flash(node.IsEmpty
                ? $"采到 {node.Product.displayName} ×{taken}（背包 {BagCount}/{BagCapacity}）"
                : $"采到 {node.Product.displayName} ×{taken}，背包装不下剩下的了");

            return taken;
        }

        public int BagCountOf(ProductData product)
            => product != null && _bag.TryGetValue(product, out int n) ? n : 0;

        // ------------------------------------------------------------------
        // 收尾
        // ------------------------------------------------------------------
        /// <summary>主动撤退 — 设计文档 §3.7「主动撤退则保留更多商品」。</summary>
        public void Retreat() => Finish(ExpeditionOutcome.Retreated);

        public void Finish(ExpeditionOutcome outcome)
        {
            if (!IsRunning) return;

            IsRunning = false;
            Outcome = outcome;

            int delivered = DepositBag(outcome);

            Game.UI?.CloseExpedition();
            Teardown();

            if (Game.Camera != null)
            {
                Game.Camera.ResetBoundsToStore();
                if (Game.Player != null) Game.Camera.target = Game.Player.transform;
            }

            Game.Manager.ReturnFromExpedition();

            Game.UI?.ShowChoice(
                OutcomeTitle(outcome),
                OutcomeBody(outcome, delivered),
                new ChoiceOption("回到备货", "商品已经进仓库", () => { }));
        }

        /// <summary>
        /// 战利品入库。失败会损失一部分（§3.7「损失部分易碎商品」），
        /// 主动撤退保留全部 —— 灰盒阶段先用一个系数表达这个差别。
        /// §3.6 的易碎品保险正是抬高失败时那个系数。
        /// </summary>
        int DepositBag(ExpeditionOutcome outcome)
        {
            if (Game.Store == null) return 0;

            float keepRatio = outcome == ExpeditionOutcome.Failed ? FailKeepRatio : 1f;
            int delivered = 0;

            foreach (var pair in _bag)
            {
                int kept = Mathf.FloorToInt(pair.Value * keepRatio);
                if (kept <= 0) continue;

                Game.Store.AddToWarehouse(pair.Key, kept);
                delivered += kept;
            }

            _bag.Clear();
            return delivered;
        }

        static string OutcomeTitle(ExpeditionOutcome outcome) =>
            outcome == ExpeditionOutcome.Cleared ? "远征完成" :
            outcome == ExpeditionOutcome.Retreated ? "已撤退" : "小队被击退";

        string OutcomeBody(ExpeditionOutcome outcome, int delivered)
        {
            string tail = delivered > 0
                ? $"\n\n带回 {delivered} 件商品，已经放进仓库，可以在备货界面上架了。"
                : "\n\n这一趟什么都没带回来。";

            // §3.4 Boss 房「掉落关键设施材料并解锁下一地区」
            if (_bossCoresThisRun > 0)
                tail += $"\n冷藏货架核心 ×{_bossCoresThisRun} 已收进后仓 " +
                        $"（累计 {ExpeditionProgress.ColdShelfCores}）。";

            if (!string.IsNullOrEmpty(_unlockedRegionThisRun))
                tail += $"\n地图上多了一条路：<b>{_unlockedRegionThisRun}</b> 已解锁。";

            switch (outcome)
            {
                case ExpeditionOutcome.Cleared:
                    return "房间清空了，暮光森林这一带暂时安全。" + tail;
                case ExpeditionOutcome.Retreated:
                    return "你带着已有的收获回到了传送点。" + tail;
                default:
                    return "小队全员倒下，一部分易碎商品在撤离途中损坏了。" + tail;
            }
        }

        void Teardown()
        {
            ClearMarkedTarget();
            TeardownRoom();

            _squad.Clear();
            _allies.Clear();
            _route.Clear();
            _roomIndex = -1;

            Captain = null;

            if (_root != null) Lifetime.Destroy(_root.gameObject);
            _root = null;
        }
    }
}
