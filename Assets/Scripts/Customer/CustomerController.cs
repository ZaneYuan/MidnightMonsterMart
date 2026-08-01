using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Player;
using MonsterMart.Store;

namespace MonsterMart.Customers
{
    /// <summary>
    /// 顾客有限状态机 — 设计文档 §3.4 与 §12.4。
    /// 怪物差异全部通过 IMonsterBehaviour 组合进来，
    /// 这个类本身不写任何「如果是吸血鬼就……」的分支。
    /// </summary>
    public class CustomerController : Interactable
    {
        public CustomerData Data { get; private set; }
        public IMonsterBehaviour Behaviour { get; private set; }
        public CustomerState State { get; private set; } = CustomerState.Entering;

        public float Patience { get; private set; }
        public float MaxPatience => Data != null ? Data.maxPatience : 100f;
        public float PatienceNormalized => MaxPatience <= 0f ? 0f : Patience / MaxPatience;

        /// <summary>满意度 0~100，结算界面统计用。</summary>
        public float Satisfaction { get; private set; } = 100f;

        public int Budget { get; private set; }

        public readonly List<ProductData> ShoppingList = new List<ProductData>();
        public readonly List<ProductData> Basket = new List<ProductData>();

        /// <summary>额外掉落的耐心倍率（怪物行为可以临时调高）。</summary>
        public float ExtraDecayMultiplier { get; set; } = 1f;

        /// <summary>正在等待玩家送来的、已完成灵界处理的商品（幽灵专用）。</summary>
        public ProductData PendingSpiritProduct { get; private set; }

        /// <summary>幽灵失忆事件进行中。</summary>
        public bool AmnesiaActive { get; set; }

        /// <summary>史莱姆吞下的额外商品数量（结账时决定怎么收费）。</summary>
        public int SwallowedExtra { get; set; }

        /// <summary>吸血鬼的黑袋子请求。</summary>
        public bool WantsDiscreetBag { get; set; }

        /// <summary>这名顾客是否已经完成结账。</summary>
        public bool Served { get; private set; }

        /// <summary>只游荡不购物（史莱姆分裂出的小史莱姆）。</summary>
        public bool WanderOnly { get; set; }

        /// <summary>游荡剩余时间，归零后自行离店。</summary>
        public float WanderSeconds { get; set; }

        public bool LeftAngry { get; private set; }

        public CustomerBubble Bubble { get; private set; }

        public Vector2 Position => transform.position;
        public Vector2Int Cell => StoreGrid.WorldToCell(Position);

        public bool IsWaitingAtCheckout =>
            State == CustomerState.WaitingInQueue && _atQueueSlot && Game.Store.Checkout.IndexOf(this) == 0;

        // ---- 内部状态 ----
        SpriteRenderer _sprite;
        readonly List<Vector2Int> _path = new List<Vector2Int>();
        int _pathIndex;
        Vector2 _position;

        ProductData _targetProduct;
        Shelf _targetShelf;
        float _browseTimer;
        float _outOfStockTimer;
        bool _atQueueSlot;
        bool _talkedTo;
        float _stateTimer;
        int _spiritDeliveries;

        // ------------------------------------------------------------------
        // 初始化
        // ------------------------------------------------------------------
        public void Initialize(CustomerData data, IMonsterBehaviour behaviour, Vector2Int spawnCell)
        {
            Data = data;
            Behaviour = behaviour;

            Patience = data.maxPatience;
            Budget = data.RollBudget();

            _position = StoreGrid.CellToWorld(spawnCell);
            transform.position = _position;

            BuildVisuals();
            BuildShoppingList();

            CustomerRegistry.Register(this);
            BestiaryTracker.Discover(data.monsterType);
            Behaviour?.OnEnterStore(this);

            State = CustomerState.Entering;
            // 先走到店内一个随机位置，再开始挑东西
            SetDestination(Game.Store.RandomWalkableCell());
        }

        void BuildVisuals()
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _sprite = bodyGo.AddComponent<SpriteRenderer>();
            _sprite.sprite = SpriteFactory.Character(Data);
            _sprite.sortingOrder = SortingLayers.Character;

            var bubbleGo = new GameObject("Bubble");
            Bubble = bubbleGo.AddComponent<CustomerBubble>();
            Bubble.Build(transform);
        }

        void BuildShoppingList()
        {
            ShoppingList.Clear();

            var preferred = GameDatabase.PreferredProducts(Data.monsterType);
            int want = Data.RollItemCount();

            // 至少买一件自己喜欢的
            if (preferred.Count > 0)
                ShoppingList.Add(preferred[Random.Range(0, preferred.Count)]);

            var pool = new List<ProductData>();
            for (int i = 0; i < GameDatabase.Products.Count; i++)
            {
                var p = GameDatabase.Products[i];
                if (p.IsDislikedBy(Data.monsterType)) continue;   // 不会主动买自己讨厌的
                if (p.isCleaningTool) continue;                   // 清洁剂是给玩家用的
                pool.Add(p);
            }

            int guard = 0;
            while (ShoppingList.Count < want && pool.Count > 0 && guard++ < 32)
            {
                var pick = pool[Random.Range(0, pool.Count)];
                if (!ShoppingList.Contains(pick)) ShoppingList.Add(pick);
            }
        }

        protected override void OnEnable() => base.OnEnable();

        protected override void OnDisable()
        {
            base.OnDisable();
            CustomerRegistry.Unregister(this);
        }

        // ------------------------------------------------------------------
        // 主循环
        // ------------------------------------------------------------------
        void Update()
        {
            if (Game.Manager == null || Data == null) return;
            if (Game.Manager.State == GameState.Paused) return;

            float dt = Time.deltaTime;
            _stateTimer += dt;

            if (State != CustomerState.Leaving && State != CustomerState.CheckingOut)
                TickPatience(dt);

            Behaviour?.OnUpdate(this);

            switch (State)
            {
                case CustomerState.Entering: TickEntering(); break;
                case CustomerState.ChoosingProduct: TickChoosing(); break;
                case CustomerState.MovingToShelf: TickMovingToShelf(); break;
                case CustomerState.TakingProduct: TickTakingProduct(dt); break;
                case CustomerState.MovingToCheckout: TickMovingToCheckout(); break;
                case CustomerState.WaitingInQueue: TickWaitingInQueue(); break;
                case CustomerState.CheckingOut: break;   // 由 CheckoutView 驱动
                case CustomerState.SpecialEvent: TickSpecialEvent(); break;
                case CustomerState.Leaving: TickLeaving(); break;
                case CustomerState.Angry: TickLeaving(); break;
            }

            MoveAlongPath(dt);
            UpdateBubble();
            UpdateSorting();
        }

        void UpdateSorting()
        {
            if (_sprite != null)
                _sprite.sortingOrder = SortingLayers.Character - Mathf.RoundToInt(_position.y * 2f);
        }

        // ------------------------------------------------------------------
        // 耐心 — 设计文档 §3.4
        // ------------------------------------------------------------------
        void TickPatience(float dt)
        {
            if (State == CustomerState.Leaving) return;

            float decay = Data.patienceDecayRate * ExtraDecayMultiplier;

            // 排队等待额外掉耐心
            if (State == CustomerState.WaitingInQueue)
            {
                decay += GameConfig.QueuePatiencePenaltyPerSecond *
                         Game.Store.Checkout.QueuePatienceMultiplier;
            }

            // 找不到商品
            if (_outOfStockTimer > 0f)
                decay *= Data.frustrationMultiplier;

            // 店太脏，所有顾客一起掉得更快（文档 §6.3）
            if (Game.Cleanliness != null && Game.Cleanliness.Value < GameConfig.CleanlinessDirtyThreshold)
                decay *= GameConfig.DirtyStoreDecayMultiplier;

            ApplyPatience(-decay * dt);

            // 店铺脏到 20 以下，顾客可能直接走人
            if (Game.Cleanliness != null &&
                Game.Cleanliness.Value < GameConfig.CleanlinessFilthyThreshold &&
                State != CustomerState.CheckingOut &&
                Random.value < GameConfig.FilthyStoreLeaveChancePerSecond * dt)
            {
                LeaveAngry("店里太脏了");
            }
        }

        public void ApplyPatience(float delta)
        {
            if (Patience <= 0f && delta < 0f) return;

            Patience = Mathf.Clamp(Patience + delta, 0f, MaxPatience);

            if (delta < 0f)
                Satisfaction = Mathf.Clamp(Satisfaction + delta * 0.5f, 0f, 100f);

            if (Patience <= 0f && State != CustomerState.Leaving && State != CustomerState.CheckingOut)
                LeaveAngry("等太久了");
        }

        public void AddSatisfaction(float delta)
            => Satisfaction = Mathf.Clamp(Satisfaction + delta, 0f, 100f);

        public PatienceTier Tier
        {
            get
            {
                if (Patience >= GameConfig.PatienceCalmThreshold) return PatienceTier.Calm;
                if (Patience >= GameConfig.PatienceImpatientThreshold) return PatienceTier.Impatient;
                if (Patience >= GameConfig.PatienceComplainThreshold) return PatienceTier.Complaining;
                return PatienceTier.Exhausted;
            }
        }

        // ------------------------------------------------------------------
        // 状态处理
        // ------------------------------------------------------------------
        void TickEntering()
        {
            if (HasArrived()) SwitchState(CustomerState.ChoosingProduct);
        }

        void TickChoosing()
        {
            // 小史莱姆只到处乱跑，不买东西
            if (WanderOnly)
            {
                TickWander();
                return;
            }

            // 忘记要买什么了，只能等玩家来帮忙回忆
            if (AmnesiaActive) return;

            // 买够了 / 钱不够了 → 去结账
            if (Basket.Count >= ShoppingList.Count)
            {
                GoToCheckout();
                return;
            }

            _targetProduct = NextWantedProduct();
            if (_targetProduct == null)
            {
                GoToCheckout();
                return;
            }

            _targetShelf = Game.Store.FindShelf(_targetProduct);

            if (_targetShelf == null || !_targetShelf.Usable)
            {
                // 缺货 — 设计文档 §3.4「找不到商品 → 等待 → 抱怨 → 降低耐心」
                _outOfStockTimer += Time.deltaTime;
                if (_outOfStockTimer >= GameConfig.OutOfStockWaitSeconds)
                {
                    Game.Reputation?.Add(GameConfig.RepOutOfStock, "顾客没买到想要的东西");
                    ShoppingList.Remove(_targetProduct);
                    _outOfStockTimer = 0f;

                    if (Basket.Count == 0 && ShoppingList.Count == 0)
                    {
                        LeaveAngry($"没有{_targetProduct.displayName}");
                        return;
                    }
                }
                return;
            }

            _outOfStockTimer = 0f;
            var access = Game.Store.AccessCellNear(_targetShelf.cells, _position);
            SetDestination(access);
            SwitchState(CustomerState.MovingToShelf);
        }

        /// <summary>游荡：随机走点，时间到了自己离开。</summary>
        void TickWander()
        {
            WanderSeconds -= Time.deltaTime;

            if (WanderSeconds <= 0f)
            {
                StartLeaving();
                return;
            }

            if (HasArrived())
                SetDestination(Game.Store.RandomWalkableCell());
        }

        ProductData NextWantedProduct()
        {
            for (int i = 0; i < ShoppingList.Count; i++)
            {
                var p = ShoppingList[i];
                if (Basket.Contains(p)) continue;
                if (p.salePrice > RemainingBudget) continue;
                return p;
            }
            return null;
        }

        public int RemainingBudget
        {
            get
            {
                int spent = 0;
                for (int i = 0; i < Basket.Count; i++) spent += Basket[i].salePrice;
                return Budget - spent;
            }
        }

        void TickMovingToShelf()
        {
            if (!HasArrived()) return;

            if (_targetShelf == null || !_targetShelf.Usable)
            {
                SwitchState(CustomerState.ChoosingProduct);
                return;
            }

            _browseTimer = GameConfig.BrowseSeconds;
            SwitchState(CustomerState.TakingProduct);
        }

        void TickTakingProduct(float dt)
        {
            _browseTimer -= dt;
            if (_browseTimer > 0f) return;

            // 幽灵拿不到实体商品 —— 转交给玩家处理（文档 §4.3）
            if (Behaviour != null && Behaviour.RequiresSpiritPacking)
            {
                PendingSpiritProduct = _targetProduct;
                SwitchState(CustomerState.SpecialEvent);
                Game.UI?.Hud?.Flash($"{Data.displayName} 需要你帮忙拿 {_targetProduct.displayName} 去灵界包装台");
                return;
            }

            if (_targetShelf != null && _targetShelf.TakeOne())
            {
                Basket.Add(_targetProduct);
                Behaviour?.OnTookProduct(this, _targetProduct);
                Game.Audio?.PlayPickup();
            }

            SwitchState(CustomerState.ChoosingProduct);
        }

        void TickSpecialEvent()
        {
            // 等待玩家把灵界处理好的商品送过来；期间不移动
            if (PendingSpiritProduct == null && !AmnesiaActive)
                SwitchState(CustomerState.ChoosingProduct);
        }

        void GoToCheckout()
        {
            if (Basket.Count == 0)
            {
                // 什么都没买到，直接走
                StartLeaving();
                return;
            }

            var checkout = Game.Store.Checkout;
            int index = checkout.Enqueue(this);
            _atQueueSlot = false;
            SetDestination(StoreGrid.WorldToCell(checkout.QueueWorldPosition(index)));
            SwitchState(CustomerState.MovingToCheckout);
        }

        void TickMovingToCheckout()
        {
            if (HasArrived())
            {
                _atQueueSlot = true;
                SwitchState(CustomerState.WaitingInQueue);
            }
        }

        void TickWaitingInQueue()
        {
            // 前面的人走了就往前挪
            int index = Game.Store.Checkout.IndexOf(this);
            if (index < 0)
            {
                StartLeaving();
                return;
            }

            Vector2 slot = Game.Store.Checkout.QueueWorldPosition(index);
            if ((slot - _position).sqrMagnitude > 0.09f)
            {
                _atQueueSlot = false;
                SetDestination(StoreGrid.WorldToCell(slot));
            }
            else if (_path.Count == 0)
            {
                _atQueueSlot = true;
            }
        }

        void TickLeaving()
        {
            if (HasArrived())
            {
                Behaviour?.OnLeaveStore(this);
                CustomerRegistry.Unregister(this);
                Destroy(gameObject);
            }
        }

        void SwitchState(CustomerState next)
        {
            State = next;
            _stateTimer = 0f;
        }

        // ------------------------------------------------------------------
        // 对外事件
        // ------------------------------------------------------------------
        public void BeginCheckout()
        {
            SwitchState(CustomerState.CheckingOut);
            Behaviour?.OnCheckout(this);
        }

        /// <summary>结账成功。</summary>
        public void CompleteCheckout(int revenue, float satisfactionDelta)
        {
            Served = true;
            AddSatisfaction(satisfactionDelta);

            Game.Economy?.RecordSale(revenue);
            Game.Store.Checkout.Dequeue(this);

            int repDelta = Satisfaction >= 70f
                ? GameConfig.RepHappyCustomer
                : Satisfaction >= 40f ? 1 : GameConfig.RepAngryCustomer / 2;
            Game.Reputation?.Add(repDelta, $"{Data.displayName} 结账完成");

            Game.Day?.RecordServed(this);
            Game.Audio?.PlayCash();
            StartLeaving();
        }

        /// <summary>顾客生气离店 — 设计文档 §6.2。</summary>
        public void LeaveAngry(string reason)
        {
            if (State == CustomerState.Leaving) return;

            LeftAngry = true;
            Satisfaction = Mathf.Min(Satisfaction, 15f);

            Game.Store.Checkout.Dequeue(this);
            Game.Reputation?.Add(GameConfig.RepAngryCustomer, $"{Data.displayName} 生气离开：{reason}");
            Game.Day?.RecordLeftAngry(this);
            Game.UI?.Hud?.Flash($"{Data.displayName} 生气离开了（{reason}）");
            Game.Audio?.PlayAngry();

            SwitchState(CustomerState.Angry);
            SetDestination(Game.Store.DoorCell);
        }

        void StartLeaving()
        {
            Game.Store.Checkout.Dequeue(this);
            SwitchState(CustomerState.Leaving);
            SetDestination(Game.Store.DoorCell);
        }

        /// <summary>营业时间结束，把还在店里的人赶走。</summary>
        public void ForceLeave()
        {
            if (State == CustomerState.Leaving || State == CustomerState.Angry) return;
            if (!Served && !WanderOnly) Game.Day?.RecordLeftUnserved(this);
            StartLeaving();
        }

        // ------------------------------------------------------------------
        // 幽灵：灵界处理
        // ------------------------------------------------------------------
        public bool NeedsSpiritPacking(ProductData product)
            => PendingSpiritProduct != null && PendingSpiritProduct == product;

        public void ReceivePackedProduct(ProductData product)
        {
            if (product == null) return;

            Basket.Add(product);
            PendingSpiritProduct = null;
            _spiritDeliveries++;

            ApplyPatience(12f);
            AddSatisfaction(10f);
            Game.Reputation?.Add(GameConfig.RepPerfectSpecialRequest, "满足了幽灵的特殊需求");
            Game.UI?.Hud?.Flash($"{Data.displayName} 收下了 {product.displayName}");
            Game.Audio?.PlaySpirit();

            SwitchState(CustomerState.ChoosingProduct);
        }

        // ------------------------------------------------------------------
        // 移动
        // ------------------------------------------------------------------
        public void SetDestination(Vector2Int cell)
        {
            _path.Clear();
            _pathIndex = 0;
            Game.Store.Pathfinder.TryFindPath(Cell, cell, _path);
        }

        bool HasArrived() => _pathIndex >= _path.Count;

        void MoveAlongPath(float dt)
        {
            if (_pathIndex >= _path.Count) return;

            Vector2 target = StoreGrid.CellToWorld(_path[_pathIndex]);
            float speed = Data.moveSpeed;

            if (Game.Events != null && Game.Events.BlackoutActive)
                speed *= GameConfig.BlackoutMoveSpeedMultiplier;

            _position = Vector2.MoveTowards(_position, target, speed * dt);
            transform.position = _position;

            if ((target - _position).sqrMagnitude <= GameConfig.CustomerArriveDistance *
                                                     GameConfig.CustomerArriveDistance)
            {
                _position = target;
                _pathIndex++;
            }
        }

        void UpdateBubble()
        {
            if (Bubble == null) return;

            bool showWant =
                State == CustomerState.ChoosingProduct ||
                State == CustomerState.MovingToShelf ||
                State == CustomerState.TakingProduct ||
                State == CustomerState.SpecialEvent;

            Bubble.SetWant(showWant ? _targetProduct : null);
            Bubble.SetPatience(PatienceNormalized, Tier);
            Bubble.SetVisible(State != CustomerState.Leaving);

            bool special = PendingSpiritProduct != null || AmnesiaActive || WantsDiscreetBag;
            Bubble.SetStatus(new Color(0.75f, 0.6f, 1f), special);
        }

        // ------------------------------------------------------------------
        // 玩家交互 — 设计文档 §3.1「[E] 与顾客交谈」
        // ------------------------------------------------------------------
        public override Vector2 InteractAnchor => _position;

        public override bool IsAvailable(PlayerController player)
        {
            if (player == null || Data == null) return false;
            if (State == CustomerState.Leaving) return false;

            if (PendingSpiritProduct != null)
                return player.Carry.Packed && player.Carry.Product == PendingSpiritProduct;

            if (AmnesiaActive) return true;
            if (WanderOnly) return true;

            return !_talkedTo && State != CustomerState.CheckingOut;
        }

        public override string GetPrompt(PlayerController player)
        {
            if (PendingSpiritProduct != null) return $"[E] 交给 {Data.displayName}";
            if (AmnesiaActive) return "[E] 帮它回忆要买什么";
            if (WanderOnly) return "[E] 把小史莱姆赶回去";
            return "[E] 与顾客交谈";
        }

        public override void OnInteract(PlayerController player)
        {
            if (PendingSpiritProduct != null)
            {
                var product = player.Carry.Product;
                player.Carry.Remove(1);
                ReceivePackedProduct(product);
                return;
            }

            if (AmnesiaActive)
            {
                Game.Events?.OpenGhostAmnesiaPuzzle(this);
                return;
            }

            if (WanderOnly)
            {
                // 引导回主史莱姆 — 设计文档 §7 事件四
                Game.Reputation?.Add(1, "把小史莱姆赶回去了");
                Game.UI?.Hud?.Flash("小史莱姆被赶回去了");
                Game.Audio?.PlayHappy();
                StartLeaving();
                return;
            }

            _talkedTo = true;
            ApplyPatience(8f);
            AddSatisfaction(4f);
            Game.Audio?.PlayUiClick();
            Game.UI?.Hud?.Flash($"{Data.displayName}：「今晚的店还挺安静。」");
        }
    }
}
