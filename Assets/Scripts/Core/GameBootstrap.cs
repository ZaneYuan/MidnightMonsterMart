using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Events;
using MonsterMart.Player;
using MonsterMart.Store;
using MonsterMart.UI;

namespace MonsterMart.Core
{
    /// <summary>
    /// 游戏入口。用 RuntimeInitializeOnLoadMethod 在场景加载后自举，
    /// 因此打开工程后按 Play 就能玩 —— 场景里不需要任何预先摆好的对象。
    ///
    /// 整个店铺、玩家、UI、音频都在这里用代码搭出来（设计文档 §16 阶段一的
    /// 「创建便利店场景」在原型阶段用程序化生成实现，避免手工拖拽）。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        static GameBootstrap _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (_instance != null) return;

            var go = new GameObject("[MonsterMart]");
            _instance = go.AddComponent<GameBootstrap>();
            DontDestroyOnLoad(go);

            _instance.BootGame();
        }

        /// <summary>从暂停菜单或结局界面重开一局。</summary>
        public static void RestartGame()
        {
            if (_instance == null)
            {
                AutoBoot();
                return;
            }

            _instance.Teardown();
            _instance.BootGame();
        }

        // ------------------------------------------------------------------
        void BootGame()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            GameDatabase.EnsureBuilt();
            InteractableRegistry.Clear();
            CustomerRegistry.Clear();
            BestiaryTracker.Reset();

            var root = new GameObject("Runtime");
            root.transform.SetParent(transform, false);

            BuildCamera(root.transform);
            BuildAudio(root.transform);
            BuildManagers(root.transform);
            BuildStore(root.transform);
            BuildPlayer(root.transform);
            BuildUI(root.transform);

            AttachCameraTarget();

            // 玩家一定见过第一天会来的怪物，先解锁图鉴里对应的条目
            DiscoverDayOneMonsters();

            var save = SaveSystem.Load();
            if (save != null) SaveSystem.Apply(save);

            Game.Manager.StartNewRun(save != null ? save.currentDay : 1);
        }

        void Teardown()
        {
            Game.Clear();
            InteractableRegistry.Clear();
            CustomerRegistry.Clear();
            SpriteFactory.ClearCache();
            GameDatabase.Reset();

            var runtime = transform.Find("Runtime");
            if (runtime != null) Destroy(runtime.gameObject);

            _cameraRig = null;
            _cameraObject = null;
        }

        // ------------------------------------------------------------------
        CameraRig _cameraRig;
        GameObject _cameraObject;

        void BuildCamera(Transform parent)
        {
            _cameraObject = new GameObject("MainCamera");
            _cameraObject.transform.SetParent(parent, false);
            _cameraObject.tag = "MainCamera";

            var camera = _cameraObject.AddComponent<Camera>();
            _cameraRig = _cameraObject.AddComponent<CameraRig>();
            _cameraRig.Initialize(camera, null);
        }

        void BuildAudio(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);

            var audio = go.AddComponent<AudioDirector>();
            audio.Build();
            Game.Audio = audio;
        }

        void BuildManagers(Transform parent)
        {
            var go = new GameObject("Managers");
            go.transform.SetParent(parent, false);

            var economy = go.AddComponent<EconomyManager>();
            economy.Initialize(GameConfig.StartingMoney);
            Game.Economy = economy;

            var reputation = go.AddComponent<ReputationManager>();
            reputation.Initialize(GameConfig.StartingReputation);
            Game.Reputation = reputation;

            var cleanliness = go.AddComponent<CleanlinessManager>();
            cleanliness.Initialize(GameConfig.StartingCleanliness);
            Game.Cleanliness = cleanliness;

            Game.Day = go.AddComponent<DayManager>();
            Game.Events = go.AddComponent<RandomEventManager>();
            Game.Manager = go.AddComponent<GameManager>();
        }

        void BuildStore(Transform parent)
        {
            var go = new GameObject("Store");
            go.transform.SetParent(parent, false);

            var store = go.AddComponent<StoreWorld>();
            store.Build();
            Game.Store = store;

            var spawnerGo = new GameObject("Spawner");
            spawnerGo.transform.SetParent(parent, false);
            Game.Spawner = spawnerGo.AddComponent<CustomerSpawner>();
        }

        void BuildPlayer(Transform parent)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(parent, false);

            var player = go.AddComponent<PlayerController>();
            player.Initialize(Game.Store.PlayerStartCell);
            Game.Player = player;
        }

        void BuildUI(Transform parent)
        {
            var go = new GameObject("UI");
            go.transform.SetParent(parent, false);

            var ui = go.AddComponent<UIRoot>();
            ui.Build();
            Game.UI = ui;
        }

        void AttachCameraTarget()
        {
            if (_cameraRig != null && Game.Player != null)
                _cameraRig.target = Game.Player.transform;
        }

        static void DiscoverDayOneMonsters()
        {
            // 图鉴条目在怪物首次入店时解锁；第一天的两种先给出来当教学
            BestiaryTracker.Discover(MonsterType.Slime);
            BestiaryTracker.Discover(MonsterType.Vampire);
        }
    }
}
