using System.Collections.Generic;

namespace MonsterMart.Customers
{
    /// <summary>当前店内所有顾客的登记表。</summary>
    public static class CustomerRegistry
    {
        static readonly List<CustomerController> _all = new List<CustomerController>(16);

        public static IReadOnlyList<CustomerController> All => _all;
        public static int Count => _all.Count;

        public static void Register(CustomerController c)
        {
            if (c != null && !_all.Contains(c)) _all.Add(c);
        }

        public static void Unregister(CustomerController c) => _all.Remove(c);

        public static void Clear() => _all.Clear();

        /// <summary>店内是否还有没结完账的顾客。</summary>
        public static bool AnyStillShopping()
        {
            for (int i = 0; i < _all.Count; i++)
            {
                var c = _all[i];
                if (c == null) continue;
                if (c.State != Data.CustomerState.Leaving) return true;
            }
            return false;
        }
    }
}
