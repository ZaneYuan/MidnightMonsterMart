using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>
    /// 商品定义 — 设计文档 §12.2。
    /// 原型阶段不实现保质期（文档 §2.4 明确「可以暂时不做保质期」）。
    /// </summary>
    [CreateAssetMenu(fileName = "ProductData", menuName = "MonsterStore/Product")]
    public class ProductData : ScriptableObject
    {
        [Header("标识")]
        public string productId;
        public string displayName;
        public ProductCategory category;

        [Header("价格")]
        public int purchasePrice;
        public int salePrice;

        [Header("怪物偏好")]
        public MonsterType preferredBy;
        public bool hasPreference;
        public MonsterType dislikedBy;
        public bool hasDislike;

        [Header("特殊属性")]
        [Tooltip("可用于清理史莱姆污渍（万能清洁剂）")]
        public bool isCleaningTool;

        [Tooltip("摆在货架上时会持续惹恼讨厌它的怪物（禁忌商品）")]
        public bool isTaboo;

        [Header("预约条线索")]
        [Tooltip("顾客想买它时留下的模糊描述")]
        [TextArea(1, 3)] public string wantClue;

        [Tooltip("顾客讨厌它时留下的警告")]
        [TextArea(1, 3)] public string avoidClue;

        [Header("表现")]
        [Tooltip("程序化生成占位图标时使用的主色")]
        public Color tintColor = Color.white;

        [Tooltip("程序化图标的形状编号 0-3")]
        public int iconShape;

        [System.NonSerialized] public Sprite runtimeIcon;

        public int Margin => salePrice - purchasePrice;

        public bool IsPreferredBy(MonsterType t) => hasPreference && preferredBy == t;
        public bool IsDislikedBy(MonsterType t) => hasDislike && dislikedBy == t;

        public override string ToString() => displayName;
    }
}
