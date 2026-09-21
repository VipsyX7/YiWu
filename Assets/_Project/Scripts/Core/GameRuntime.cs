using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 单局运行时协调器。负责：建局、建层、玩家生成、层间切换、结算、
    /// 以及白盒验收所需的遥测日志。
    ///
    /// 白盒验收链路：木/火元素法术打元素地形 → 反应 → 区域效果 → 伤怪/减速/回血。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameRuntime : MonoBehaviour
    {
        public static GameRuntime I { get; private set; }

        /// <summary>决策 Q1 落地开关：法术是否始终附带玩家当前元素。</summary>
        public static bool AttachPlayerElement = true;

        public ElementGrid Elements { get; private set; }
        public FloorRuntime Floor { get; private set; }
        public PlayerController Player { get; private set; }
        public RoomController CurrentRoom { get; private set; }

        public Transform TerrainRoot { get; private set; }
        public Transform EffectsRoot { get; private set; }
        public Transform PlayerRoot { get; private set; }

        public bool GameOver { get; private set; }
        public bool Victory { get; private set; }
        public int FloorIndex { get; private set; } = 1;
        public int FloorsCleared { get; private set; }
        public int RunSeed { get; private set; }

        public int CastCount;
        public int ReactionCount;
        public int RefractCount;
        public int KillCount;
        public int PurchaseCount;

        public readonly Dictionary<ReactionKind, int> ReactionTally = new Dictionary<ReactionKind, int>();
        public readonly List<string> EventLog = new List<string>();
        private const int MaxLog = 12;

        public GameConfig Cfg => GameDatabase.I != null ? GameDatabase.I.Config : null;

        private void Awake()
        {
            if (I != null && I != this) { Destroy(this); return; }
            I = this;

            Elements = new ElementGrid();
            TerrainRoot = NewChild("Terrain");
            EffectsRoot = NewChild("Effects");
            PlayerRoot = NewChild("PlayerRoot");
            EnemyHealth.LootDropper = LootDropper.Drop;
        }

        private Transform NewChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void Start()
        {
            BuildRun();
        }

        private void Update()
        {
            // 地形反应次数自动回充：每块地形每 RechargeInterval 秒 +1 次，封顶上限
            if (!GameOver && Elements != null) Elements.Tick(Time.deltaTime);

            var input = KleinInput.I;
            if (input == null) return;

            // 调试：F1 切换玩家当前元素（用来遍历 3×3 反应矩阵）
            if (input.DebugPressed() && Player != null && Player.Stats != null)
            {
                var e = Player.Stats.Element;
                var next = e == ElementType.Water ? ElementType.Fire
                         : e == ElementType.Fire ? ElementType.Wood
                         : ElementType.Water;
                Player.Stats.Element = next;
                Log("玩家元素 → " + next.ToCn());
            }

            // 调试：R 重建当前层
            if (input.ReloadPressed() && !GameOver)
            {
                Log("重建第 " + FloorIndex + " 层");
                BuildFloor(FloorIndex);
            }
        }

        // ------------------------------------------------------------ 建局

        public void BuildRun()
        {
            GameOver = false;
            Victory = false;
            FloorIndex = 1;
            FloorsCleared = 0;
            RunSeed = Random.Range(1, 999999);
            CastCount = ReactionCount = RefractCount = KillCount = PurchaseCount = 0;
            ReactionTally.Clear();
            EventLog.Clear();

            Log("新的一局开始，seed=" + RunSeed);

            SpawnPlayer();
            BuildFloor(FloorIndex);
        }

        private void SpawnPlayer()
        {
            if (Player != null) Destroy(Player.gameObject);

            var go = new GameObject("Player");
            go.transform.SetParent(PlayerRoot, false);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var stats = go.AddComponent<PlayerStats>();
            stats.ApplyConfig(Cfg);
            // 本命元素来自选人场景（直接打开游戏场景时 GameSession 里的默认值是水）
            stats.Element = GameSession.SelectedElement;

            var health = go.AddComponent<PlayerHealth>();
            var controller = go.AddComponent<PlayerController>();
            var caster = go.AddComponent<SpellCaster>();
            var items = go.AddComponent<PlayerItems>();

            // 依次补上需要的组件顺序：Controller.Awake 会找 Stats/Health/Caster/Items
            controller.Bind(stats, health, caster);
            caster.Bind(stats, health);
            _ = items;

            health.Died += OnPlayerDied;

            // 初始法术：魔弹 + 防护罩
            var db = GameDatabase.I;
            var bullet = db.Spells.Find(s => s.DisplayName == "魔弹");
            var barrier = db.Spells.Find(s => s.DisplayName == "防护罩");
            if (bullet != null) caster.SetSlot(0, bullet);
            if (barrier != null) caster.SetSlot(1, barrier);

            // 其余法术进背包，方便在法术背包里换装
            foreach (var s in db.Spells)
                if (s != bullet && s != barrier) caster.Grant(s);

            Player = controller;
        }

        public void BuildFloor(int index)
        {
            ClearElements();
            if (Floor != null)
            {
                Destroy(Floor.gameObject);
                Floor = null;
            }
            ClearChildren(EffectsRoot);

            Elements.InitialCharges = Cfg.TerrainInitialCharges;
            Elements.MaxCharges = Cfg.TerrainMaxCharges;
            Elements.ReactCooldownFrames = Cfg.ReactionCooldownFrames;
            Elements.RechargeInterval = Cfg.TerrainRechargeInterval;

            Projectile.ResetPools();
            Pickup.ResetPool();

            Floor = FloorRuntime.Build(index, Cfg, RunSeed + index * 7919, transform);
            FloorIndex = index;

            var start = Floor.Layout.StartRoom;
            if (start != null && Player != null)
                Player.Teleport(start.Center);

            Log("第 " + index + " 层生成完毕：" + Floor.Layout.Rooms.Count + " 房间 / "
                + Floor.Layout.Doors.Count + " 门 / " + Elements.BlobCount + " 块地形("
                + Elements.CellCount + " 格)");
        }

        private void ClearElements()
        {
            if (Elements == null) return;
            Elements.Clear();
        }

        private static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        public void NextFloor()
        {
            if (GameOver) return;
            FloorsCleared++;
            int next = FloorIndex + 1;
            if (next > Cfg.FloorCount)
            {
                Victory = true;
                GameOver = true;
                Log("通关！层数 = " + FloorsCleared);
                return;
            }
            Log("进入第 " + next + " 层");
            BuildFloor(next);
        }

        public void OnPlayerDied()
        {
            if (GameOver) return;
            GameOver = true;
            Log("单局失败（血量归零）→ 结算");
        }

        public void Restart() => BuildRun();

        /// <summary>回到开始场景。</summary>
        public void ReturnToTitle()
        {
            GameOver = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Title);
        }

        // ------------------------------------------------------------ 事件上报

        public void OnRoomEntered(RoomController room)
        {
            CurrentRoom = room;
            if (room != null && room.Node != null)
                Log("进入 " + room.Node.Type.ToCn());
        }

        public void OnRoomExited(RoomController room)
        {
            if (CurrentRoom == room) CurrentRoom = null;
        }

        public void ReportCast(int slot, SpellCard card)
        {
            CastCount++;
        }

        public void ReportRefract()
        {
            RefractCount++;
            Log("折射镜触发（累计 " + RefractCount + "）");
        }

        public void ReportReaction(Vector2Int cell, ReactionEntry e, ProjectileTeam team)
        {
            ReactionCount++;
            if (!ReactionTally.ContainsKey(e.Kind)) ReactionTally[e.Kind] = 0;
            ReactionTally[e.Kind]++;

            if (e.Kind != ReactionKind.Recharge)
            {
                Log($"{e.SpellElement.ToCn()}法术 × {e.TerrainElement.ToCn()}地形 → {e.Kind} (x{ReactionTally[e.Kind]})");
            }
        }

        public void ReportKill()
        {
            KillCount++;
        }

        public void ReportPickup(PickupKind kind, float value)
        {
            // 拾取较频繁，不写日志，只累计
        }

        public void ReportPurchase(ItemDefinition item, int price)
        {
            PurchaseCount++;
            Log("购买 " + (item != null ? item.DisplayName : "?") + " -" + price + "G");
        }

        public void ReportChestOpened(ItemDefinition item)
        {
            Log("宝箱开出 " + (item != null ? item.DisplayName : "?"));
        }

        public void ReportSpellPicked(SpellCard card)
        {
            Log("拾取法术：" + (card != null ? card.Label() : "?"));
        }

        public void Log(string line)
        {
            EventLog.Add("[" + Time.frameCount + "] " + line);
            while (EventLog.Count > MaxLog) EventLog.RemoveAt(0);
        }

        public void RequestRestart() => Restart();
    }
}
