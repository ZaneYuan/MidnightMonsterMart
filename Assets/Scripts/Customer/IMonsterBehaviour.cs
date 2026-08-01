using MonsterMart.Data;

namespace MonsterMart.Customers
{
    /// <summary>
    /// 怪物差异化行为 — 设计文档 §12.4 推荐的组合模式。
    /// 「推荐不要在一个类中写完所有怪物特殊逻辑。」
    /// </summary>
    public interface IMonsterBehaviour
    {
        void OnEnterStore(CustomerController customer);
        void OnUpdate(CustomerController customer);
        void OnCheckout(CustomerController customer);
        void OnLeaveStore(CustomerController customer);

        /// <summary>该怪物是否无法自己拿实体商品（幽灵）。</summary>
        bool RequiresSpiritPacking { get; }

        /// <summary>顾客成功从货架取走一件商品时回调。</summary>
        void OnTookProduct(CustomerController customer, ProductData product);
    }

    /// <summary>提供空实现，具体怪物只重写自己关心的部分。</summary>
    public abstract class MonsterBehaviourBase : IMonsterBehaviour
    {
        public virtual void OnEnterStore(CustomerController customer) { }
        public virtual void OnUpdate(CustomerController customer) { }
        public virtual void OnCheckout(CustomerController customer) { }
        public virtual void OnLeaveStore(CustomerController customer) { }
        public virtual bool RequiresSpiritPacking => false;
        public virtual void OnTookProduct(CustomerController customer, ProductData product) { }
    }

    public static class MonsterBehaviourFactory
    {
        public static IMonsterBehaviour Create(MonsterType type)
        {
            switch (type)
            {
                case MonsterType.Vampire: return new VampireBehaviour();
                case MonsterType.Werewolf: return new WerewolfBehaviour();
                case MonsterType.Ghost: return new GhostBehaviour();
                case MonsterType.Slime: return new SlimeBehaviour();
                case MonsterType.Inspector: return new InspectorBehaviour();
                default: return new SlimeBehaviour();
            }
        }
    }
}
