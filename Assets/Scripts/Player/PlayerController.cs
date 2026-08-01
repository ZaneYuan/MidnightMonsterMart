using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Core;
using MonsterMart.Data;
using MonsterMart.Store;

namespace MonsterMart.Player
{
    /// <summary>
    /// 玩家 — 设计文档 §3.1。
    /// WASD 移动、Shift 加速、E 交互（含长按）。
    /// 碰撞完全走 StoreGrid，没有 Rigidbody2D，因此永远不会被挤进墙里。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public PlayerCarry Carry { get; private set; } = new PlayerCarry();

        /// <summary>当前正对着的可交互对象（可能为 null）。</summary>
        public Interactable Focus { get; private set; }

        /// <summary>长按进度 0~1；没有在长按时为 0。</summary>
        public float HoldProgress { get; private set; }

        public bool IsBusy => _holding;      // 补货中无法移动（文档 §3.3）

        Vector2 _position;
        Vector2 _facing = Vector2.down;

        SpriteRenderer _sprite;
        SpriteRenderer _carryIcon;
        Transform _carryRoot;

        bool _holding;
        float _holdTimer;
        float _holdDuration;
        Interactable _holdTarget;

        StoreGrid Grid => Game.Store != null ? Game.Store.Grid : null;

        public Vector2 Position => _position;
        public Vector2Int Cell => StoreGrid.WorldToCell(_position);

        public void Initialize(Vector2Int startCell)
        {
            _position = StoreGrid.CellToWorld(startCell);
            transform.position = _position;
            BuildVisuals();
        }

        void BuildVisuals()
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _sprite = bodyGo.AddComponent<SpriteRenderer>();
            _sprite.sprite = SpriteFactory.PlayerSprite();
            _sprite.sortingOrder = SortingLayers.Character;

            var carryGo = new GameObject("CarryRoot");
            carryGo.transform.SetParent(transform, false);
            carryGo.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            _carryRoot = carryGo.transform;

            var iconGo = new GameObject("CarryIcon");
            iconGo.transform.SetParent(_carryRoot, false);
            _carryIcon = iconGo.AddComponent<SpriteRenderer>();
            _carryIcon.sortingOrder = SortingLayers.CarryItem;
            _carryIcon.enabled = false;
        }

        void Update()
        {
            if (Game.Manager == null) return;

            bool canAct = Game.Manager.State == GameState.Open ||
                          Game.Manager.State == GameState.Preparation;

            bool uiBlocking = Game.UI != null && Game.UI.BlocksWorldInput;

            if (!canAct || uiBlocking)
            {
                CancelHold();
                Focus = null;
                UpdateCarryVisual();
                return;
            }

            UpdateMovement();
            UpdateFocus();
            UpdateInteraction();
            UpdateCarryVisual();
            UpdateSorting();
        }

        // ------------------------------------------------------------------
        // 移动
        // ------------------------------------------------------------------
        void UpdateMovement()
        {
            if (_holding) return;    // 补货过程中玩家无法移动（文档 §3.3）

            Vector2 axis = InputReader.MoveAxis;
            if (axis.sqrMagnitude < 0.0001f) return;

            _facing = axis;

            float speed = InputReader.Sprint
                ? GameConfig.PlayerSprintSpeed
                : GameConfig.PlayerWalkSpeed;

            // 踩到污渍会变慢（文档 §4.4）
            if (Game.Store != null && Game.Store.HasStainAt(Cell))
                speed *= GameConfig.StainSlowMultiplier;

            if (Game.Events != null && Game.Events.BlackoutActive)
                speed *= GameConfig.BlackoutMoveSpeedMultiplier;

            Vector2 delta = axis * speed * Time.deltaTime;
            MoveWithCollision(delta);

            transform.position = _position;
        }

        /// <summary>分轴移动 + 圆形碰撞判定，撞墙时会自然沿墙滑动。</summary>
        void MoveWithCollision(Vector2 delta)
        {
            var grid = Grid;
            if (grid == null)
            {
                _position += delta;
                return;
            }

            float r = GameConfig.PlayerRadius;

            var tryX = new Vector2(_position.x + delta.x, _position.y);
            if (!grid.CircleOverlapsBlocked(tryX, r)) _position.x = tryX.x;

            var tryY = new Vector2(_position.x, _position.y + delta.y);
            if (!grid.CircleOverlapsBlocked(tryY, r)) _position.y = tryY.y;
        }

        void UpdateSorting()
        {
            // y 越小越靠前，制造简单的前后遮挡
            if (_sprite != null)
                _sprite.sortingOrder = SortingLayers.Character - Mathf.RoundToInt(_position.y * 2f);
        }

        // ------------------------------------------------------------------
        // 交互
        // ------------------------------------------------------------------
        void UpdateFocus()
        {
            if (_holding) return;
            Focus = InteractableRegistry.FindNearest(this, _position, GameConfig.InteractRange);
        }

        void UpdateInteraction()
        {
            if (_holding)
            {
                if (!InputReader.InteractHeld || _holdTarget == null || !_holdTarget.IsAvailable(this))
                {
                    CancelHold();
                    return;
                }

                _holdTimer += Time.deltaTime;
                HoldProgress = _holdDuration <= 0f ? 1f : Mathf.Clamp01(_holdTimer / _holdDuration);
                _holdTarget.OnHoldProgress(this, HoldProgress);

                if (HoldProgress >= 1f)
                {
                    var target = _holdTarget;
                    EndHold();
                    target.OnInteract(this);
                }
                return;
            }

            if (Focus == null) return;

            if (Focus.Kind == InteractionKind.Hold)
            {
                if (InputReader.InteractHeld) BeginHold(Focus);
            }
            else if (InputReader.InteractPressed)
            {
                Focus.OnInteract(this);
            }
        }

        void BeginHold(Interactable target)
        {
            _holding = true;
            _holdTarget = target;
            _holdTimer = 0f;
            _holdDuration = Mathf.Max(0.01f, target.HoldSeconds(this));
            HoldProgress = 0f;
        }

        void CancelHold()
        {
            if (!_holding) return;
            _holdTarget?.OnHoldCancelled(this);
            EndHold();
        }

        void EndHold()
        {
            _holding = false;
            _holdTarget = null;
            _holdTimer = 0f;
            _holdDuration = 0f;
            HoldProgress = 0f;
        }

        // ------------------------------------------------------------------
        // 携带表现
        // ------------------------------------------------------------------
        void UpdateCarryVisual()
        {
            if (_carryIcon == null) return;

            if (Carry.IsEmpty)
            {
                _carryIcon.enabled = false;
                return;
            }

            _carryIcon.enabled = true;
            _carryIcon.sprite = SpriteFactory.ProductIcon(Carry.Product);
            _carryIcon.sortingOrder = SortingLayers.CarryItem;
            _carryIcon.transform.localScale = Vector3.one * 0.75f;
        }

        /// <summary>从仓库取货（由仓库 UI 调用）。</summary>
        public int TakeFromWarehouse(ProductData product)
        {
            if (Game.Store == null || product == null) return 0;

            // 换商品时先把手上的退回仓库，避免玩家凭空丢货
            if (!Carry.IsEmpty && Carry.Product != product)
            {
                Game.Store.AddToWarehouse(Carry.Product, Carry.Count);
                Carry.Clear();
            }

            int want = Carry.FreeSpace;
            if (want <= 0) return 0;

            int taken = Game.Store.TakeFromWarehouse(product, want);
            if (taken > 0)
            {
                Carry.Take(product, taken);
                Game.Audio?.PlayPickup();
            }
            return taken;
        }

        public void TeleportToCell(Vector2Int cell)
        {
            _position = StoreGrid.CellToWorld(cell);
            transform.position = _position;
        }
    }
}
