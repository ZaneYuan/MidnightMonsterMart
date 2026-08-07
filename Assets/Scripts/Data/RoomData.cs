using System.Collections.Generic;
using UnityEngine;

namespace MonsterMart.Data
{
    /// <summary>
    /// 远征地图上的一个房间 — 设计文档 §3.4「地图房间结构」，
    /// 具体到暮光森林那条线是 §11.1 的六个区域。
    ///
    /// 灰盒阶段所有房间共用同一张矩形地形，靠 kind、敌人数量和文案区分；
    /// 真正的地形差异（毒雾、藤蔓门、隐藏树洞）留到美术与关卡阶段。
    /// </summary>
    [CreateAssetMenu(fileName = "RoomData", menuName = "MonsterStore/Room")]
    public class RoomData : ScriptableObject
    {
        [Header("标识")]
        public string roomId;
        public string displayName;
        public RoomKind kind = RoomKind.Battle;

        [TextArea(2, 4)] public string briefing;

        [Header("敌人")]
        public string enemyId;
        public int enemyCount;

        [Header("杂兵 — §3.4 精英房「风险较高」")]
        /// <summary>
        /// 陪着主敌人一起刷的普通敌人。精英空地光放一只精英会变成纯单挑，
        /// 加上杂兵才有「先清场还是先集火精英」的取舍。
        /// </summary>
        public string minionEnemyId;
        public int minionCount;

        public bool HasMinions => minionCount > 0 && !string.IsNullOrEmpty(minionEnemyId);

        /// <summary>这间房一共会刷出多少敌人（主敌人 + 杂兵）。</summary>
        public int TotalEnemyCount =>
            (HasEnemies ? enemyCount : 0) + (HasMinions ? minionCount : 0);

        [Header("采集点 — §3.4 资源房「低压力采集，强调路线和携带容量」")]
        /// <summary>每个条目生成一个采集点，产出对应商品。</summary>
        public List<string> harvestProductIds = new List<string>();

        /// <summary>单个采集点的产量。</summary>
        public int harvestPerNode = 3;

        [Header("事件 — §3.4 事件房")]
        public string eventId;

        [Header("肉鸽强化 — §3.6「每次远征出现 2～3 次临时强化」")]
        /// <summary>进这间房时弹一次三选一。整条路线上打勾的房间数就是「2～3 次」。</summary>
        public bool offersBoon;

        /// <summary>营地和事件房没有敌人，进门就能往下走。</summary>
        public bool HasEnemies => enemyCount > 0 && !string.IsNullOrEmpty(enemyId);

        public bool HasHarvest => harvestProductIds != null && harvestProductIds.Count > 0;
        public bool HasEvent => !string.IsNullOrEmpty(eventId);
    }
}
