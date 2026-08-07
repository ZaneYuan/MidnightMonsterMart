using UnityEngine;
using MonsterMart.Art;
using MonsterMart.Customers;
using MonsterMart.Data;
using MonsterMart.Events;
using MonsterMart.Expeditions;
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

        /// <summary>下一次 BootGame 强制开新局（玩家主动点了「重新开始」）。一次性。</summary>
        static bool _forceFreshRun;

        /// <summary>
        /// 从暂停菜单或结局界面重开一局。
        ///
        /// <paramref name="freshRun"/> = true 表示玩家主动要求重开（暂停菜单的
        /// 「重新开始」）：哪怕存档还没打完也要丢弃本局进度，从第一天来过。
        /// 结局界面的「再开一局」不用传 —— 那份存档已经带 runCompleted 了。
        /// 两条路径都只丢本局进度，图鉴和音量走 SaveSystem.Apply 的跨局分支保留。
        /// </summary>
        public static void RestartGame(bool freshRun = false)
        {
            _forceFreshRun = freshRun;

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

            // 上一局如果是在营业倍速中被「重新开始」打断的，Time.timeScale 不会经过
            // GameManager.CloseStore 归位 —— 新的一局必须从正常速度开始。
            Time.timeScale = 1f;

            GameDatabase.EnsureBuilt();
            InteractableRegistry.Clear();
            CustomerRegistry.Clear();
            BestiaryTracker.Reset();
            InputReader.Reset();

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

            // 上一局已经通关的存档只还原图鉴和音量，进度从第一天重来。
            // 否则终局存档里的 currentDay 还是最后一天，重进（以及结局界面的
            // 「再开一局」，走的是同一条 BootGame 路径）都会被丢回去重打。
            bool resume = SaveSystem.ShouldResume(save, _forceFreshRun);
            _forceFreshRun = false;

            // 本局进度先归零，读档再填回来 —— 没有存档时（首次开局 / 存档被删）
            // 也不能带着上一局残留的冷藏货架核心和疲劳进新局。
            ExpeditionProgress.Reset();
            StaffRoster.Reset();
            CaptainProgress.Reset();

            if (save != null) SaveSystem.Apply(save, resume);

            Game.Manager.StartNewRun(resume ? SaveSystem.ResumeDay(save) : 1,
                                     resume ? save.totalProfit : 0);

            // 存档停在「今天已经跑完远征」那一段的话，跳过晨会直接进闭店准备 ——
            // 否则玩家要么白跑一趟，要么带着到手的战利品再去一趟。
            if (resume && SaveSystem.ShouldResumeAfterExpedition(save))
                Game.Manager.ResumeAfterExpedition();
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
            // 场景里可能已经有摄像机（Unity 新建场景自带 Main Camera + Directional Light）。
            // 只关掉 Camera 组件本身，不动 GameObject —— 否则会连它身上的
            // AudioListener 一起关掉，导致整个游戏没有声音。
            var existing = FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null) continue;
                existing[i].enabled = false;
            }

            _cameraObject = new GameObject("GameCamera");
            _cameraObject.transform.SetParent(parent, false);

            var camera = _cameraObject.AddComponent<Camera>();

            // 场景里没有 AudioListener 时（比如空的 Boot 场景）补一个
            if (FindAnyObjectByType<AudioListener>() == null)
                _cameraObject.AddComponent<AudioListener>();

            _cameraRig = _cameraObject.AddComponent<CameraRig>();
            _cameraRig.Initialize(camera, null);
            Game.Camera = _cameraRig;
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

            var expeditionGo = new GameObject("Expedition");
            expeditionGo.transform.SetParent(parent, false);
            Game.Expedition = expeditionGo.AddComponent<ExpeditionManager>();
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
