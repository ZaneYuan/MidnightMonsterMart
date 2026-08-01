using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>顾客定义 — 设计文档 §12.3。</summary>
    [CreateAssetMenu(fileName = "CustomerData", menuName = "MonsterStore/Customer")]
    public class CustomerData : ScriptableObject
    {
        [Header("标识")]
        public string customerId;
        public string displayName;
        public MonsterType monsterType;

        [Header("行为参数")]
        public float moveSpeed = 2.2f;
        public float maxPatience = 100f;

        [Tooltip("每秒基础耐心衰减")]
        public float patienceDecayRate = 1.6f;

        [Tooltip("找不到商品 / 排队时的额外衰减倍率")]
        public float frustrationMultiplier = 2.5f;

        [Header("预算与购物")]
        public int minBudget = 12;
        public int maxBudget = 30;
        public int minItems = 1;
        public int maxItems = 3;

        [Header("表现")]
        public Color bodyColor = Color.white;
        public Color accentColor = Color.white;

        [Tooltip("程序化角色贴图的外形编号 0-4")]
        public int silhouette;

        [Header("图鉴")]
        [TextArea(2, 5)] public string bestiaryLikes;
        [TextArea(2, 5)] public string bestiaryDislikes;
        [TextArea(2, 5)] public string bestiaryRule;

        [System.NonSerialized] public Sprite runtimeSprite;

        public int RollBudget() => Random.Range(minBudget, maxBudget + 1);
        public int RollItemCount() => Random.Range(minItems, maxItems + 1);

        public override string ToString() => displayName;
    }
}
