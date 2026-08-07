using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Combat;
using MonsterMart.Core;

namespace MonsterMart.Expeditions
{
    /// <summary>
    /// 远征场上一切会动、会挨打的东西的共同部分：队长、怪物员工、敌人。
    ///
    /// 坐标一律是房间的局部格子坐标；节点挂在带偏移的房间根节点下，
    /// 世界坐标由父节点自动带上，所以这里不用到处加偏移。
    /// 碰撞和便利店一样走网格，不用 Collider2D。
    /// </summary>
    public abstract class ExpeditionActor : MonoBehaviour
    {
        public Health Health { get; protected set; }

        protected Vector2 _position;
        protected SpriteRenderer _sprite;
        protected Transform _barRoot;
        protected SpriteRenderer _barFill;

        public Vector2 Position => _position;
        public Vector2Int Cell => StoreGrid.WorldToCell(_position);
        public bool IsAlive => Health != null && !Health.IsDead;

        /// <summary>身体半径，用于网格碰撞。</summary>
        protected virtual float Radius => 0.32f;

        /// <summary>
        /// 血条挂在身体上方多高。角色贴图轴心在脚底附近（见 SpriteFactory.BuildCharacter），
        /// 比以前的圆点头像高不少，血条也得跟着抬上去，不然会卡在半身高度。
        /// </summary>
        protected const float BarHeight = 1.3f;

        protected static StoreGrid Grid =>
            Game.Expedition != null && Game.Expedition.World != null
                ? Game.Expedition.World.Grid
                : null;

        protected void BuildBody(Sprite sprite, float maxHealth)
        {
            Health = gameObject.AddComponent<Health>();
            Health.Initialize(maxHealth);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _sprite = bodyGo.AddComponent<SpriteRenderer>();
            _sprite.sprite = sprite;
            _sprite.sortingOrder = SortingLayers.Character;

            BuildHealthBar();
            Health.OnDamaged += _ => RefreshHealthBar();
            RefreshHealthBar();
        }

        void BuildHealthBar()
        {
            var rootGo = new GameObject("HealthBar");
            rootGo.transform.SetParent(transform, false);
            rootGo.transform.localPosition = new Vector3(0f, BarHeight, 0f);
            _barRoot = rootGo.transform;

            var back = new GameObject("Back");
            back.transform.SetParent(_barRoot, false);
            back.transform.localScale = new Vector3(0.9f, 0.12f, 1f);
            var backSr = back.AddComponent<SpriteRenderer>();
            backSr.sprite = SpriteFactory.Solid(new Color(0f, 0f, 0f, 0.65f));
            backSr.sortingOrder = SortingLayers.FixtureOverlay;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(_barRoot, false);
            _barFill = fill.AddComponent<SpriteRenderer>();
            _barFill.sprite = SpriteFactory.Solid(Color.white);
            _barFill.sortingOrder = SortingLayers.FixtureOverlay + 1;
        }

        protected void RefreshHealthBar()
        {
            if (_barFill == null || Health == null) return;

            float t = Health.Normalized;
            _barFill.transform.localScale = new Vector3(0.86f * t, 0.08f, 1f);
            _barFill.transform.localPosition = new Vector3(-0.43f * (1f - t), 0f, 0f);
            _barFill.color = t > 0.5f ? new Color(0.45f, 0.85f, 0.45f)
                           : t > 0.25f ? new Color(0.95f, 0.80f, 0.35f)
                                       : new Color(0.90f, 0.35f, 0.35f);
        }

        /// <summary>直接落到某个格子 —— 换房间、归队时用。</summary>
        public void TeleportTo(Vector2Int cell) => PlaceAtCell(cell);

        protected void PlaceAtCell(Vector2Int cell)
        {
            _position = StoreGrid.CellToWorld(cell);
            transform.localPosition = _position;
        }

        /// <summary>
        /// 按网格做连续碰撞的移动。分轴推进，撞墙时沿墙滑，
        /// 不会像整体回退那样在墙角卡死。
        /// </summary>
        protected void MoveBy(Vector2 delta)
        {
            var grid = Grid;
            if (grid == null || delta.sqrMagnitude <= 0f) return;

            var next = _position;

            var tryX = new Vector2(next.x + delta.x, next.y);
            if (!grid.CircleOverlapsBlocked(tryX, Radius)) next = tryX;

            var tryY = new Vector2(next.x, next.y + delta.y);
            if (!grid.CircleOverlapsBlocked(tryY, Radius)) next = tryY;

            _position = next;
            transform.localPosition = _position;

            if (_sprite != null)
                _sprite.sortingOrder = SortingLayers.Character - Mathf.RoundToInt(_position.y * 2f);
        }

        /// <summary>朝目标走一步。</summary>
        protected void StepToward(Vector2 target, float speed, float dt)
        {
            var to = target - _position;
            if (to.sqrMagnitude < 0.0001f) return;
            MoveBy(to.normalized * speed * dt);
        }

        public float DistanceTo(ExpeditionActor other)
            => other == null ? float.MaxValue : (other._position - _position).magnitude;
    }
}
