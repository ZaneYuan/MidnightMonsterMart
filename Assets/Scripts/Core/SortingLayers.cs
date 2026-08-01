namespace MonsterMart.Core
{
    /// <summary>
    /// 排序序号常量。工程不新增 Sorting Layer（那需要改 TagManager 资产），
    /// 全部用 Default 层的 sortingOrder 排序，行为完全可预测。
    /// </summary>
    public static class SortingLayers
    {
        public const int Floor = -100;
        public const int FloorDecal = -90;   // 污渍
        public const int Wall = -50;
        public const int Fixture = 0;
        public const int FixtureOverlay = 10;
        public const int Character = 100;    // 运行时再叠加 -y 做前后遮挡
        public const int CarryItem = 200;
        public const int Bubble = 300;
        public const int Vignette = 500;     // 停电遮罩
    }
}
