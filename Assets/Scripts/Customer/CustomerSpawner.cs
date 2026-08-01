using System.Collections.Generic;
using UnityEngine;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.Customers
{
    /// <summary>
    /// 按当日波次表把顾客放进店里 — 设计文档 §8 的三天顾客配置。
    /// </summary>
    public class CustomerSpawner : MonoBehaviour
    {
        readonly List<SpawnEntry> _pending = new List<SpawnEntry>();
        Transform _root;
        float _elapsed;
        bool _running;

        public int Spawned { get; private set; }
        public int Remaining => _pending.Count;

        void Awake()
        {
            var rootGo = new GameObject("Customers");
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.transform;
        }

        public void BeginDay(DayPlan plan)
        {
            _pending.Clear();
            _elapsed = 0f;
            Spawned = 0;
            _running = true;

            if (plan == null) return;
            for (int i = 0; i < plan.spawns.Count; i++)
                _pending.Add(plan.spawns[i]);

            // 保证按时间排序
            _pending.Sort((a, b) => a.atSeconds.CompareTo(b.atSeconds));
        }

        public void StopDay()
        {
            _running = false;
            _pending.Clear();
        }

        void Update()
        {
            if (!_running) return;
            if (Game.Manager == null || Game.Manager.State != GameState.Open) return;

            _elapsed += Time.deltaTime;

            while (_pending.Count > 0 && _pending[0].atSeconds <= _elapsed)
            {
                var entry = _pending[0];
                _pending.RemoveAt(0);
                Spawn(entry.monsterType);
            }
        }

        public CustomerController Spawn(MonsterType type)
        {
            var data = GameDatabase.GetCustomer(type);
            if (data == null)
            {
                Debug.LogError($"[CustomerSpawner] 找不到怪物数据 {type}");
                return null;
            }

            var go = new GameObject("Customer_" + type);
            go.transform.SetParent(_root, false);

            var controller = go.AddComponent<CustomerController>();
            controller.Initialize(data, MonsterBehaviourFactory.Create(type), Game.Store.DoorCell);

            Spawned++;
            Game.Audio?.PlayDoorBell();
            return controller;
        }

        /// <summary>史莱姆分裂用：在指定格子放一只小史莱姆。</summary>
        public CustomerController SpawnMinion(MonsterType type, Vector2Int cell, float scale)
        {
            var data = GameDatabase.GetCustomer(type);
            if (data == null) return null;

            var go = new GameObject("Minion_" + type);
            go.transform.SetParent(_root, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var controller = go.AddComponent<CustomerController>();
            controller.Initialize(data, MonsterBehaviourFactory.Create(type), cell);
            return controller;
        }

        /// <summary>营业结束：让所有还在店里的顾客离开。</summary>
        public void ForceEveryoneOut()
        {
            var all = CustomerRegistry.All;
            for (int i = all.Count - 1; i >= 0; i--)
                all[i]?.ForceLeave();
        }
    }
}
