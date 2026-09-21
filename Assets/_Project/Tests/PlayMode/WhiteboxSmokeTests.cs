using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Klein.Tests
{
    /// <summary>
    /// 白盒冒烟测试。用真实 GameObject / 物理 / 逐帧运行来验证
    /// 「数据 → 关卡 → 法术 → 元素反应 → 区域效果 → 伤怪」整条链路，
    /// 而不是只测单个函数。可在批处理下跑：
    ///   Unity.exe -batchmode -nographics -projectPath &lt;proj&gt;
    ///     -runTests -testPlatform PlayMode -testResults results.xml
    /// </summary>
    public class WhiteboxSmokeTests
    {
        private GameObject _boot;

        private IEnumerator Boot(bool resetSession = true)
        {
            // 清掉上一个测试留下的静态池（敌人/弹体/掉落物都挂在静态根节点上）
            EnemyFactory.Reset();
            Projectile.ResetPools();
            Pickup.ResetPool();

            // 选人结果默认复位，避免测试互相影响
            if (resetSession) GameSession.Clear();

            // 每次都从代码默认值起步，避免上一次生成的资产影响结果
            GameDatabase.LoadOrCreate(forceCodeDefaults: true);

            _boot = new GameObject("TestBoot");
            _boot.AddComponent<GameBootstrap>();
            yield return null;
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_boot != null) Object.Destroy(_boot);
            _boot = null;

            EnemyFactory.Reset();
            Projectile.ResetPools();
            Pickup.ResetPool();
            GameSession.Clear();

            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------ 建局

        [UnityTest]
        public IEnumerator T01_Bootstrap_BuildsPlayableRun()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            Assert.IsNotNull(rt, "GameRuntime 未创建");
            Assert.IsNotNull(rt.Floor, "楼层未生成");
            Assert.IsNotNull(rt.Player, "玩家未生成");
            Assert.IsNotNull(rt.Cfg, "GameConfig 缺失");

            var layout = rt.Floor.Layout;
            Assert.GreaterOrEqual(layout.Rooms.Count, rt.Cfg.MinRoomsPerFloor, "房间数少于下限");
            Assert.LessOrEqual(layout.Rooms.Count, rt.Cfg.MaxRoomsPerFloor, "房间数超过上限");

            int shop = 0, spell = 0, chest = 0, boss = 0, start = 0;
            foreach (var r in layout.Rooms)
            {
                if (r.Type == RoomType.Shop) shop++;
                else if (r.Type == RoomType.Spell) spell++;
                else if (r.Type == RoomType.Chest) chest++;
                else if (r.Type == RoomType.Boss) boss++;
                else if (r.Type == RoomType.Start) start++;
            }

            Assert.AreEqual(1, start, "起点房数量不为 1");
            Assert.AreEqual(1, boss, "BOSS 房数量不为 1");
            Assert.LessOrEqual(shop, 1, "商店房超过 1 个");
            Assert.LessOrEqual(spell, 1, "法术房超过 1 个");
            Assert.LessOrEqual(chest, 1, "宝箱房超过 1 个");

            Assert.Greater(rt.Elements.CellCount, 0, "没有生成任何元素地形");

            Debug.Log($"[T01] 房间={layout.Rooms.Count} 门={layout.Doors.Count} "
                      + $"地形格={rt.Elements.CellCount} 墙段={rt.Floor.WallRunCount}");
        }

        [UnityTest]
        public IEnumerator T02_Floor_StartCanReachBoss()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var layout = rt.Floor.Layout;
            var grid = layout.Grid;
            var start = layout.StartRoom;
            RoomNode boss = null;
            foreach (var r in layout.Rooms)
                if (r.Type == RoomType.Boss) { boss = r; break; }

            Assert.IsNotNull(boss, "没有 BOSS 房");

            // (a) 房间图连通性：所有房间都应能从起点 BFS 到达
            var seen = new HashSet<Vector2Int> { start.GridIndex };
            var q = new Queue<RoomNode>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var d in LevelGrid.Dirs)
                {
                    var nb = layout.RoomAtGrid(cur.GridIndex + d);
                    if (nb == null || seen.Contains(nb.GridIndex)) continue;
                    seen.Add(nb.GridIndex);
                    q.Enqueue(nb);
                }
            }
            Assert.AreEqual(layout.Rooms.Count, seen.Count, "存在从起点不可达的房间");

            // (b) 网格连通性：清空全层后门会打开，此时应能 A* 走到 BOSS 房
            foreach (var r in layout.Rooms) r.Cleared = true;
            yield return null;
            yield return null;

            var from = GridPathfinder.NearestPassable(grid, LevelGrid.WorldToCell(start.Center));
            var to = GridPathfinder.NearestPassable(grid, LevelGrid.WorldToCell(boss.Center));
            var path = GridPathfinder.Find(grid, from, to);

            Assert.Greater(path.Count, 0, "清空全层后起点到 BOSS 房仍不可达");
            Debug.Log($"[T02] 房间图连通 {seen.Count}/{layout.Rooms.Count}，"
                      + $"起点→BOSS 路径 {path.Count} 格");
        }

        // ------------------------------------------------------------ 纯逻辑：反应次数

        [UnityTest]
        public IEnumerator T03_TerrainCharges_ConsumeThenStop()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var eg = rt.Elements;
            eg.ReactCooldownFrames = 0;   // 测试里绕过 60 帧防抖
            eg.RechargeInterval = 9999f;  // 关掉自动回充，保证次数断言确定性
            eg.Clear();

            var cell = LevelGrid.WorldToCell(rt.Floor.Layout.StartRoom.Center);
            var tc = eg.Add(cell, ElementType.Water, rt.TerrainRoot);
            Assert.AreEqual(rt.Cfg.TerrainInitialCharges, tc.Charges, "初始次数应为 3");

            // 火法术 × 水地形 → 蒸汽，连续 3 次把次数打光
            for (int i = 0; i < 3; i++)
            {
                var res = ElementReactionResolver.Resolve(eg, tc, ElementType.Fire, ElementType.None);
                Assert.IsTrue(res.Triggered, $"第 {i + 1} 次反应未触发");
                Assert.AreEqual(ReactionKind.SteamArea, res.Entry.Kind, "应为蒸汽区域");
            }
            Assert.AreEqual(0, tc.Charges, "3 次后次数应为 0");

            // 第 4 次：次数耗尽，不再反应
            var after = ElementReactionResolver.Resolve(eg, tc, ElementType.Fire, ElementType.None);
            Assert.IsFalse(after.Triggered, "次数耗尽后仍触发了反应");

            Debug.Log($"[T03] 地形次数耗尽行为符合假设 A3（保留、不再反应），最终 {tc.Charges}");
        }

        [UnityTest]
        public IEnumerator T04_SameElement_RechargesTerrain()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var eg = rt.Elements;
            eg.ReactCooldownFrames = 0;
            eg.RechargeInterval = 9999f;
            eg.Clear();

            var cell = LevelGrid.WorldToCell(rt.Floor.Layout.StartRoom.Center);
            var tc = eg.Add(cell, ElementType.Fire, rt.TerrainRoot);
            int before = tc.Charges;

            // 同元素（火法术 × 火地形）→ 次数 +1，不消耗
            var res = ElementReactionResolver.Resolve(eg, tc, ElementType.Fire, ElementType.None);
            Assert.IsTrue(res.Triggered, "同元素未触发回充");
            Assert.AreEqual(ReactionKind.Recharge, res.Entry.Kind);
            Assert.AreEqual(before + 1, tc.Charges, "同元素未回充");

            // 触及上限后不再增长
            for (int i = 0; i < 10; i++)
                ElementReactionResolver.Resolve(eg, tc, ElementType.Fire, ElementType.None);
            Assert.AreEqual(eg.MaxCharges, tc.Charges, "回充超过上限");

            Debug.Log($"[T04] 同元素回充正常，上限 = {eg.MaxCharges}");
        }

        [UnityTest]
        public IEnumerator T05_ReactionCooldown_BlocksRapidRepeat()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var eg = rt.Elements;
            eg.ReactCooldownFrames = 60;   // 策划案：单次法术与同一地形每 60 帧最多 1 次
            eg.RechargeInterval = 9999f;
            eg.Clear();

            var cell = LevelGrid.WorldToCell(rt.Floor.Layout.StartRoom.Center);
            var tc = eg.Add(cell, ElementType.Water, rt.TerrainRoot);

            var first = ElementReactionResolver.Resolve(eg, tc, ElementType.Fire, ElementType.None);
            Assert.IsTrue(first.Triggered, "首次反应未触发");

            var second = ElementReactionResolver.Resolve(eg, tc, ElementType.Fire, ElementType.None);
            Assert.IsFalse(second.Triggered, "同帧重复反应未被冷却拦住");

            Assert.AreEqual(rt.Cfg.TerrainInitialCharges - 1, tc.Charges, "冷却期内不应再次消耗次数");
            Debug.Log("[T05] 60 帧反应冷却生效");
        }

        // ------------------------------------------------------------ 实物链路：火法术打水地形

        [UnityTest]
        public IEnumerator T06_FireSpellOnWaterTerrain_CreatesSteamArea()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var grid = rt.Floor.Layout.Grid;
            var eg = rt.Elements;
            eg.RechargeInterval = 9999f;
            eg.Clear();

            var player = rt.Player;
            Vector2 origin = player.transform.position;

            // 在玩家右侧 3 格放一格水地形
            var cell = GridPathfinder.NearestPassable(grid, LevelGrid.WorldToCell(origin + new Vector2(3f, 0f)));
            eg.Add(cell, ElementType.Water, rt.TerrainRoot);

            // 玩家当前元素 = 火（法术固有元素为 None，靠附加元素触发 → 验证决策 Q1）
            player.Stats.Element = ElementType.Fire;

            var spec = new ProjectileSpec
            {
                Mask = ElementMask.Fire,
                Innate = ElementType.None,
                PlayerElement = ElementType.Fire,
                Damage = 12f,
                Speed = 12f,
                Range = 8f,
                Radius = 0.25f,
                Knockback = 0f,
                FromPlayer = true,
                Pierce = 0,
                Color = Color.white,
            };
            Projectile.Spawn(origin, (LevelGrid.CellCenter2(cell) - origin).normalized, spec, ProjectileTeam.Player);

            float t = 0f;
            while (t < 3f && !rt.ReactionTally.ContainsKey(ReactionKind.SteamArea))
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(rt.ReactionTally.ContainsKey(ReactionKind.SteamArea),
                          "火法术撞水地形没有产生蒸汽区域");
            Assert.AreEqual(1, rt.ReactionTally[ReactionKind.SteamArea]);

            var areas = Object.FindObjectsByType<AreaEffect>(FindObjectsSortMode.None);
            Assert.Greater(areas.Length, 0, "蒸汽区域 GameObjects 未生成");

            Debug.Log($"[T06] 火×水 → 蒸汽区域 ✓ （耗时 {t:0.00}s，地形剩余次数 "
                      + $"{eg.GetBlob(cell).Charges}）");
        }

        [UnityTest]
        public IEnumerator T07_SteamArea_DamagesEnemy()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var room = rt.Floor.Layout.StartRoom;

            var def = GameDatabase.I.PickTrashEnemy();
            Assert.IsNotNull(def, "没有可用怪物定义");

            // 放在起点房正中央（可玩区内）
            var enemy = EnemyFactory.Spawn(def, room.Center, room.RuntimeRoom);
            Assert.IsNotNull(enemy, "怪物生成失败");

            float hp0 = enemy.Health;
            Assert.Greater(hp0, 0f);

            var entry = GameDatabase.I.Reactions.Find(ElementType.Fire, ElementType.Water);
            Assert.IsNotNull(entry, "反应表里没有 火→水 条目");

            AreaEffect.Create(rt.EffectsRoot, room.Center, entry, AreaKind.Steam);

            float t = 0f;
            while (t < 2.0f && enemy.Health >= hp0)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Assert.Less(enemy.Health, hp0, "蒸汽区域没有对怪物造成伤害");
            Debug.Log($"[T07] 蒸汽伤怪 ✓ {hp0:0.0} → {enemy.Health:0.0}（{t:0.00}s）");
        }

        // ------------------------------------------------------------ 实物链路：木法术穿火地形 + 灼伤

        [UnityTest]
        public IEnumerator T08_WoodSpellThroughFireTerrain_BurnsEnemy()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var grid = rt.Floor.Layout.Grid;
            var eg = rt.Elements;
            eg.RechargeInterval = 9999f;
            eg.Clear();

            var player = rt.Player;
            Vector2 origin = player.transform.position;
            var startCell = LevelGrid.WorldToCell(origin);

            // 沿同一行向右：+2 放火地形，+5 放怪物，保证弹道是一条直线
            var terrainCell = new Vector2Int(startCell.x + 2, startCell.y);
            var enemyCell = new Vector2Int(startCell.x + 5, startCell.y);
            Assert.IsTrue(grid.IsWalkable(terrainCell) && !grid.DoorCells.Contains(terrainCell),
                          "测试用火地形格不可走");
            Assert.IsTrue(grid.IsWalkable(enemyCell) && !grid.DoorCells.Contains(enemyCell),
                          "测试用怪物格不可走");

            eg.Add(terrainCell, ElementType.Fire, rt.TerrainRoot);

            // 用「不移动」的怪物原型，避免它自己走出弹道造成偶发失败
            EnemyDefinition def = GameDatabase.I.Enemies.Find(e => e.MoveMode == MoveMode.Stationary && !e.IsBoss);
            Assert.IsNotNull(def, "缺少「不移动」的怪物原型");

            var enemy = EnemyFactory.Spawn(def, LevelGrid.CellCenter2(enemyCell),
                                           rt.Floor.Layout.StartRoom.RuntimeRoom);
            Assert.IsNotNull(enemy);
            float hp0 = enemy.Health;

            var spec = new ProjectileSpec
            {
                Mask = ElementMask.Wood,
                Innate = ElementType.Wood,
                PlayerElement = ElementType.Wood,
                Damage = 1f,          // 压低直伤，让掉血主要来自灼伤
                Speed = 12f,
                Range = 12f,
                Radius = 0.3f,
                FromPlayer = true,
                Pierce = 0,
                Color = Color.white,
            };
            Projectile.Spawn(origin, Vector2.right, spec, ProjectileTeam.Player);

            // (1) 等反应登记（撞地形是瞬间的）
            float t = 0f;
            while (t < 3f && !rt.ReactionTally.ContainsKey(ReactionKind.PierceBurn))
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(rt.ReactionTally.ContainsKey(ReactionKind.PierceBurn),
                          "木法术撞火地形没有触发穿透+灼伤");

            // (2) 再等弹体穿过地形飞到怪物身上、挂上灼伤状态
            float t2 = 0f;
            while (t2 < 3f && enemy.GetComponent<BurnStatus>() == null)
            {
                t2 += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(enemy != null && enemy.IsAlive, "怪物在挂上灼伤前就死了");
            var burn = enemy.GetComponent<BurnStatus>();
            Assert.IsNotNull(burn, "敌人身上没有挂上灼伤状态");

            // (3) 记录直伤之后的血量，再等灼伤 tick，确认掉血来自灼伤而非直伤
            float hpAfterHit = enemy.Health;

            float t3 = 0f;
            while (t3 < 3f && enemy.Health >= hpAfterHit)
            {
                t3 += Time.deltaTime;
                yield return null;
            }
            Assert.Less(enemy.Health, hpAfterHit, "灼伤没有造成持续掉血");

            // 一次弹体对同一格地形只应产生一次反应（防止高帧率下重复消耗）
            Assert.AreEqual(1, rt.ReactionTally[ReactionKind.PierceBurn],
                            "同一弹体对同一格地形重复触发了穿透+灼伤");
            Assert.AreEqual(rt.Cfg.TerrainInitialCharges - 1, eg.GetBlob(terrainCell).Charges,
                            "同一弹体应当只消耗 1 次地形反应次数");

            Debug.Log($"[T08] 木×火 → 穿透+灼伤 ✓ 直伤后 {hpAfterHit:0.00} → "
                      + $"{enemy.Health:0.00}（灼伤 tick {t3:0.00}s，原始 {hp0:0.0}，"
                      + $"地形 {rt.Cfg.TerrainInitialCharges}→{eg.GetBlob(terrainCell).Charges}，"
                      + $"本弹体反应次数 {rt.ReactionTally[ReactionKind.PierceBurn]}）");
        }

        // ------------------------------------------------------------ 法术与修正器

        [UnityTest]
        public IEnumerator T09_SpellSlots_And_RefractModifier()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var caster = rt.Player.Caster;
            Assert.IsNotNull(caster, "SpellCaster 缺失");

            Assert.IsFalse(caster.Slots[0].IsEmpty, "槽 0 应有初始法术（魔弹）");
            Assert.IsFalse(caster.Slots[1].IsEmpty, "槽 1 应有初始法术（防护罩）");
            Assert.AreEqual(SpellCaster.SlotCount, KleinInput.SlotKeyNames.Length,
                            "6 个槽应对应 6 个按键 QWEASD");
            Assert.AreEqual(6, KleinInput.SlotKeyNames.Length);

            // 魔弹可施放，施放后进冷却
            bool ok = caster.TryCast(0, (Vector2)rt.Player.transform.position + Vector2.right * 5f);
            Assert.IsTrue(ok, "魔弹施放失败");
            Assert.Greater(caster.CooldownRemaining(0), 0f, "施放后未进冷却");
            Assert.IsFalse(caster.CanCast(0), "冷却中不应可施放");

            // 折射镜修正器链路
            var refract = GameDatabase.I.Modifiers.Find(m => m.Kind == ModifierKind.Refract);
            Assert.IsNotNull(refract, "缺少折射镜修正器");

            var bullet = GameDatabase.I.Spells.Find(s => s.DisplayName == "魔弹");
            caster.SetSlot(2, bullet);
            caster.Slots[2].Modifiers.Add(refract);

            yield return new WaitForSeconds(0.7f);   // 等冷却

            bool ok2 = caster.TryCast(2, (Vector2)rt.Player.transform.position + Vector2.right * 5f);
            Assert.IsTrue(ok2, "带折射镜的魔弹施放失败");

            Debug.Log("[T09] 6 槽 / 初始法术 / 冷却 / 修正器装配 ✓");
        }

        // ------------------------------------------------------------ 生成器回归

        [UnityTest]
        public IEnumerator T11_FloorGenerator_AlwaysProducesConnectedGrids()
        {
            yield return Boot();

            var cfg = GameRuntime.I.Cfg;
            int badRooms = 0;
            string firstBad = "";
            int totalRooms = 0;

            for (int seed = 1; seed <= 40; seed++)
            {
                var layout = FloorGenerator.Generate(1, cfg, seed);
                var grid = layout.Grid;
                var start = layout.StartRoom;
                Assert.IsNotNull(start, "seed " + seed + " 没有起点房");

                // 洪水填充：只用结构可通行性（把门当作已开），检查房间是否真的连得上
                var seen = new HashSet<Vector2Int>();
                var q = new Queue<Vector2Int>();
                var s0 = GridPathfinder.NearestPassable(grid, LevelGrid.WorldToCell(start.Center));
                q.Enqueue(s0);
                seen.Add(s0);

                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var d in LevelGrid.Dirs)
                    {
                        var n = c + d;
                        if (!grid.IsWalkable(n) || seen.Contains(n)) continue;
                        seen.Add(n);
                        q.Enqueue(n);
                    }
                }

                foreach (var r in layout.Rooms)
                {
                    totalRooms++;
                    bool reachable = false;
                    for (int y = r.InnerBounds.yMin; y < r.InnerBounds.yMax && !reachable; y++)
                    for (int x = r.InnerBounds.xMin; x < r.InnerBounds.xMax && !reachable; x++)
                        if (seen.Contains(new Vector2Int(x, y))) reachable = true;

                    if (!reachable)
                    {
                        badRooms++;
                        if (firstBad.Length == 0)
                            firstBad = $"seed={seed} 房间{r.GridIndex}({r.Type.ToCn()}) 不可达";
                    }
                }

                yield return null;
            }

            Assert.AreEqual(0, badRooms, $"存在不可达房间（共 {badRooms} 个）：{firstBad}");
            Debug.Log($"[T11] 40 个随机楼层 / {totalRooms} 个房间的网格连通性全部通过");
        }

        // ------------------------------------------------------------ 移动逻辑

        [UnityTest]
        public IEnumerator T13_LineOfSight_DetectsWalls()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var grid = rt.Floor.Layout.Grid;
            var start = rt.Floor.Layout.StartRoom;
            Vector2 c = start.Center;
            float r = rt.Cfg.PlayerRadius;

            // 房间内部：横向、纵向都应判定为通畅
            Assert.IsTrue(NavUtil.HasLineOfSight(grid, c, c + new Vector2(5f, 0f), r),
                          "房间内横向直线应判定为无遮挡");
            Assert.IsTrue(NavUtil.HasLineOfSight(grid, c, c + new Vector2(0f, 2.5f), r),
                          "房间内纵向直线应判定为无遮挡");

            // 注意：房间中轴正对门洞，而起点房已清空→门是开的，
            // 所以"往隔壁房间看"很可能是【穿门而过】的通畅直线。
            // 要测遮挡必须挑一个"不是门"的实心墙格。
            int wallX = start.InnerBounds.xMax;
            int wallY = int.MinValue;
            for (int y = start.InnerBounds.yMin; y < start.InnerBounds.yMax; y++)
            {
                if (!grid.IsWalkable(new Vector2Int(wallX, y))) { wallY = y; break; }
            }
            Assert.AreNotEqual(int.MinValue, wallY, "右墙列上找不到实心墙格");
            var solidWall = new Vector2Int(wallX, wallY);
            Assert.IsFalse(grid.IsPassable(solidWall), "该格应为实心墙");

            // 与墙格同一行的水平视线：必须被挡住
            Vector2 rowY = new Vector2(0f, wallY + 0.5f);
            Vector2 from = new Vector2(start.InnerBounds.xMin + 2.5f, rowY.y);
            Vector2 beforeWall = new Vector2(wallX - 2.5f, rowY.y);
            Vector2 behindWall = new Vector2(wallX + 2.5f, rowY.y);

            Assert.IsTrue(NavUtil.HasLineOfSight(grid, from, beforeWall, 0f),
                          "墙前的房内直线应判定为通畅");
            Assert.IsFalse(NavUtil.HasLineOfSight(grid, from, behindWall, 0f),
                           "水平穿过实心墙不应判定为无遮挡");

            Debug.Log($"[T13] 直线判定 ✓ 房内通 / 实心墙({solidWall})不通");
        }

        [UnityTest]
        public IEnumerator T14_RightClickMove_StraightOrPathfind()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var pc = rt.Player;
            var grid = rt.Floor.Layout.Grid;
            var start = rt.Floor.Layout.StartRoom;
            var ib = start.InnerBounds;

            // (a) 房内无遮挡 → 直行
            Vector2 straightGoal = start.Center + new Vector2(4f, 0f);
            pc.SetDestination(straightGoal);
            Assert.IsTrue(pc.IsStraightMoving, "房内无遮挡的目标应当直行");
            Assert.IsFalse(pc.IsPathFollowing, "无遮挡时不该走 A*");

            // (b) 真的径直走过去了
            Vector2 p0 = pc.transform.position;
            float t = 0f;
            while (t < 3f && Vector2.Distance(pc.transform.position, straightGoal) > 0.4f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            float travelled = Vector2.Distance(p0, pc.transform.position);
            Assert.Greater(travelled, 2f, "直行移动没有生效（位移 " + travelled.ToString("0.00") + "）");
            float residual = Vector2.Distance(pc.transform.position, straightGoal);
            Assert.Less(residual, 0.6f, "没有走到目标点附近（残差 " + residual.ToString("0.00") + "）");

            // (c) 房间中间人为放一根柱子，目标在柱子另一侧 → 必须改为 A* 绕行
            var pillar = new Vector2Int(ib.xMin + 4, ib.yMin + 4);
            var destCell = new Vector2Int(ib.xMin + 1, ib.yMin + 4);
            Assert.IsTrue(grid.IsWalkable(pillar), "柱子格原本应可走");
            grid.SetWalkable(pillar, false);
            try
            {
                pc.SetDestination(LevelGrid.CellCenter2(destCell));
                Assert.IsFalse(pc.IsStraightMoving, "有柱子挡着时不该直行");
                Assert.IsTrue(pc.IsPathFollowing, "有障碍且能绕过去时应切换到 A* 寻路");
            }
            finally
            {
                grid.SetWalkable(pillar, true);
            }

            // (d) 目标落在墙里 → 不能直行
            var wallCell = new Vector2Int(ib.xMax, ib.yMin + 2);
            Assert.IsFalse(grid.IsPassable(wallCell), "测试用格子应为墙");
            pc.SetDestination(LevelGrid.CellCenter2(wallCell));
            Assert.IsFalse(pc.IsStraightMoving, "点墙里不应直行");

            // (e) 指令可以被新指令覆盖
            pc.SetDestination(start.Center + new Vector2(-3f, 0f));
            Assert.IsTrue(pc.IsStraightMoving, "新指令未被接受");

            Debug.Log($"[T14] 右键移动 ✓ 无障碍直行 {travelled:0.00} 格（残差 {residual:0.00}）"
                      + " / 绕柱子改走 A* ✓ / 点墙不直行 ✓ / 可被新指令覆盖 ✓");
        }

        // ------------------------------------------------------------ 整块地形与自动回充

        [UnityTest]
        public IEnumerator T15_TerrainBlob_SharesChargesAcrossCells()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var eg = rt.Elements;
            eg.ReactCooldownFrames = 0;
            eg.RechargeInterval = 9999f;
            eg.Clear();

            var ib = rt.Floor.Layout.StartRoom.InnerBounds;
            var cells = new List<Vector2Int>
            {
                new Vector2Int(ib.xMin + 3, ib.yMin + 3),
                new Vector2Int(ib.xMin + 4, ib.yMin + 3),
                new Vector2Int(ib.xMin + 4, ib.yMin + 4),
            };

            var blob = eg.CreateBlob(cells, ElementType.Water, rt.TerrainRoot);
            Assert.IsNotNull(blob, "创建地块失败");
            Assert.AreEqual(3, blob.CellCount, "三格应为一块地形");
            Assert.AreEqual(1, eg.BlobCount, "三格应当合并成 1 块地形，而不是 3 块");
            Assert.AreEqual(rt.Cfg.TerrainInitialCharges, blob.Charges, "整块初始应有 3 次");

            // 逐格打，但共用同一份次数
            for (int i = 0; i < cells.Count; i++)
            {
                var hit = eg.GetBlob(cells[i]);
                Assert.AreSame(blob, hit, "同一块地形的格子应解析到同一个 blob");

                var res = ElementReactionResolver.Resolve(eg, hit, ElementType.Fire, ElementType.None);
                Assert.IsTrue(res.Triggered, $"第 {i + 1} 格未触发反应");
                Assert.AreEqual(2 - i, blob.Charges, $"第 {i + 1} 格后次数应为 {2 - i}");
            }

            // 耗尽后不再反应
            Assert.AreEqual(0, blob.Charges);
            var after = ElementReactionResolver.Resolve(eg, blob, ElementType.Fire, ElementType.None);
            Assert.IsFalse(after.Triggered, "次数耗尽后仍触发了反应");

            Debug.Log($"[T15] 整块地形共享次数 ✓ {blob.CellCount} 格共用一份：3→2→1→0");
        }

        [UnityTest]
        public IEnumerator T16_TerrainBlob_AutoRecharge()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var eg = rt.Elements;
            eg.Clear();

            Assert.AreEqual(5f, rt.Cfg.TerrainRechargeInterval, 0.001f, "自动回充间隔应为 5 秒");
            Assert.AreEqual(5, eg.MaxCharges, "地块次数上限应为 5");

            var cell = LevelGrid.WorldToCell(rt.Floor.Layout.StartRoom.Center);
            var blob = eg.Add(cell, ElementType.Water, rt.TerrainRoot);
            Assert.AreEqual(rt.Cfg.TerrainRechargeInterval, blob.RechargeInterval, 0.001f,
                            "地块应继承配置里的回充间隔");

            // 打光后从 0 开始恢复
            blob.Charges = 0;
            eg.RefreshViews(blob);

            eg.Tick(rt.Cfg.TerrainRechargeInterval * 0.5f);
            Assert.AreEqual(0, blob.Charges, "不到间隔不该回充");

            eg.Tick(rt.Cfg.TerrainRechargeInterval * 0.51f);
            Assert.AreEqual(1, blob.Charges, "满 5 秒应 +1 次");

            // 连续推进，确认封顶在上限
            for (int i = 0; i < 20; i++) eg.Tick(rt.Cfg.TerrainRechargeInterval);
            Assert.AreEqual(eg.MaxCharges, blob.Charges, "回充超过了上限");

            // 到上限后计时器应停摆，不会"攒"出超额次数
            eg.Tick(rt.Cfg.TerrainRechargeInterval * 100f);
            Assert.AreEqual(eg.MaxCharges, blob.Charges, "满值后仍在增长");

            Debug.Log($"[T16] 自动回充 ✓ 每 {blob.RechargeInterval:0.#}s +1 次，封顶 {eg.MaxCharges}（耗尽后能自己回到满）");
        }

        [UnityTest]
        public IEnumerator T17_OneProjectile_ReactsOncePerWholeBlob()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var grid = rt.Floor.Layout.Grid;
            var eg = rt.Elements;
            eg.ReactCooldownFrames = 0;   // 关掉冷却，单独验证「一个弹体对一块地形只反应一次」
            eg.RechargeInterval = 9999f;
            eg.Clear();

            var player = rt.Player;
            var startCell = LevelGrid.WorldToCell(player.transform.position);
            var ib = rt.Floor.Layout.StartRoom.InnerBounds;

            // 沿玩家所在行造一条长地形，弹体要从头穿到尾。
            // 长度必须裁到房间内部，否则会落在墙格上。
            int x0 = startCell.x + 2;
            int x1 = Mathf.Min(ib.xMax - 1, x0 + 7);
            var cells = new List<Vector2Int>();
            for (int x = x0; x <= x1; x++) cells.Add(new Vector2Int(x, startCell.y));

            Assert.GreaterOrEqual(cells.Count, 4, "房间内放不下足够长的测试地形");
            foreach (var c in cells)
                Assert.IsTrue(grid.IsWalkable(c) && !grid.DoorCells.Contains(c), "测试格不可走：" + c);

            var blob = eg.CreateBlob(cells, ElementType.Fire, rt.TerrainRoot);
            Assert.IsNotNull(blob);
            Assert.AreEqual(cells.Count, blob.CellCount, "长条地形应合成为一块");

            int charges0 = blob.Charges;

            // 木法术 × 火地形 = 穿透 + 灼伤 → 弹体会横穿整块
            var spec = new ProjectileSpec
            {
                Mask = ElementMask.Wood,
                Innate = ElementType.Wood,
                PlayerElement = ElementType.Wood,
                Damage = 1f,
                Speed = 12f,
                Range = 16f,
                Radius = 0.25f,
                FromPlayer = true,
                Pierce = 0,
                Color = Color.white,
            };
            Projectile.Spawn(player.transform.position, Vector2.right, spec, ProjectileTeam.Player);

            float t = 0f;
            while (t < 3f && rt.ReactionCount == 0) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(rt.ReactionTally.ContainsKey(ReactionKind.PierceBurn), "木法术撞火地形没有触发穿透+灼伤");

            // 再等弹体飞完剩余路程
            yield return new WaitForSeconds(1.2f);

            Assert.AreEqual(1, rt.ReactionTally[ReactionKind.PierceBurn],
                            "一个弹体横穿整块地形只应产生一次反应");
            Assert.AreEqual(charges0 - 1, blob.Charges,
                            "整块地形只应被消耗 1 次次数（而不是每格一次）");

            Debug.Log($"[T17] 整块地形视作整体 ✓ 弹体横穿 {blob.CellCount} 格只触发 1 次，"
                      + $"次数 {charges0}→{blob.Charges}");
        }

        [UnityTest]
        public IEnumerator T18_RotArea_SlowsEnemiesOnly()
        {
            yield return Boot();

            var rt = GameRuntime.I;
            var room = rt.Floor.Layout.StartRoom;
            var pc = rt.Player;

            var entry = GameDatabase.I.Reactions.Find(ElementType.Water, ElementType.Wood);
            Assert.IsNotNull(entry, "反应表里没有 水→木 条目");
            Assert.AreEqual(ReactionKind.RotArea, entry.Kind, "水×木 应为腐烂区域");

            // 不动的怪物原型，保证它待在区域里
            var def = GameDatabase.I.Enemies.Find(e => e.MoveMode == MoveMode.Stationary && !e.IsBoss);
            Assert.IsNotNull(def, "缺少「不移动」的怪物原型");

            var enemy = EnemyFactory.Spawn(def, room.Center + new Vector2(2f, 0f), room.RuntimeRoom);
            Assert.IsNotNull(enemy);
            var brain = enemy.GetComponent<EnemyBrain>();
            Assert.IsNotNull(brain);

            pc.Teleport(room.Center);
            pc.Stop();
            Assert.AreEqual(1f, pc.SlowFactor, 0.001f, "玩家初始不应处于减速状态");
            Assert.AreEqual(1f, brain.SlowFactor, 0.001f, "怪物初始不应处于减速状态");

            AreaEffect.Create(rt.EffectsRoot, room.Center, entry, AreaKind.Rot);
            yield return null;
            yield return new WaitForSeconds(0.4f);

            Assert.Less(brain.SlowFactor, 0.99f, "腐烂区域应当减速敌方单位");
            Assert.AreEqual(1f, pc.SlowFactor, 0.001f, "腐烂区域【不应当】减速玩家");

            Debug.Log($"[T18] 腐烂区域只减速敌方 ✓ 怪物移速 ×{brain.SlowFactor:0.##}，玩家 ×{pc.SlowFactor:0.##}");
        }

        // ------------------------------------------------------------ 开始 / 选人 / 游戏 流程

        [UnityTest]
        public IEnumerator T19_SelectedElement_IsUsedByTheRun()
        {
            GameSession.Select(ElementType.Wood);
            yield return Boot(false);

            var rt = GameRuntime.I;
            Assert.IsTrue(rt != null, "GameRuntime 未创建");
            Assert.IsTrue(rt.Player != null, "玩家未生成");
            Assert.IsTrue(rt.Player.Stats != null, "玩家属性缺失");
            Assert.AreEqual(ElementType.Wood, rt.Player.Stats.Element,
                            "游戏里没有使用选人界面选中的元素");
            Assert.IsTrue(GameSession.HasSelection);

            Debug.Log("[T19] 选人结果带入游戏 ✓ 选「木」→ 玩家本命元素为木");
        }

        [UnityTest]
        public IEnumerator T20_SceneFlow_TitleToSelectToGame()
        {
            if (SceneUtility.GetBuildIndexByScenePath("Assets/_Project/Scenes/Title.unity") < 0)
                Assert.Ignore("尚未生成场景（先执行菜单 Klein/生成全部场景（开始/选人/游戏））");

            GameSession.Clear();

            // ---- 1) 开始场景 ----
            SceneManager.LoadScene(SceneNames.Title);
            yield return null;
            yield return null;

            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsTrue(title != null, "Title 场景里没有 TitleScreen");

            title.BeginGame();          // 与"点击开始游戏"走同一条代码路径
            yield return null;
            yield return null;

            // ---- 2) 选人场景 ----
            var select = Object.FindFirstObjectByType<ElementSelectScreen>();
            Assert.IsTrue(select != null, "Select 场景里没有 ElementSelectScreen");
            Assert.IsFalse(GameSession.HasSelection, "还没选就不该有选择结果");

            select.Choose(ElementType.Wood);   // 与"点击木元素卡"走同一条代码路径
            Assert.IsTrue(select.HasChosen, "Choose 之后应处于已选择状态");
            Assert.AreEqual(ElementType.Wood, GameSession.SelectedElement, "选择结果没有写进会话");

            // ---- 3) 游戏场景（选人界面延迟 0.4s 后切场景）----
            float t = 0f;
            while (t < 5f && GameRuntime.I == null) { t += Time.deltaTime; yield return null; }
            yield return null;
            yield return null;

            var rt = GameRuntime.I;
            Assert.IsTrue(rt != null, "选人后没有进入游戏场景");
            Assert.IsTrue(rt.Player != null, "游戏场景没有生成玩家");
            Assert.AreEqual(ElementType.Wood, rt.Player.Stats.Element, "游戏没有使用选中的元素");
            Assert.IsTrue(rt.Floor != null, "游戏场景没有生成楼层");

            Debug.Log($"[T20] 场景流程 ✓ Title → Select → Game（选木，切场景耗时 {t:0.00}s）");
        }

        [UnityTest]
        public IEnumerator T21_UiHitTest_MatchesPointerAndSubmit()
        {
            var rect = new Rect(100f, 100f, 200f, 60f);

            Assert.IsTrue(KleinUi.Hit(rect, new Vector2(200f, 130f), true), "矩形内 + 按下 应判定为点击");
            Assert.IsFalse(KleinUi.Hit(rect, new Vector2(200f, 130f), false), "没按下不应判定为点击");
            Assert.IsFalse(KleinUi.Hit(rect, new Vector2(50f, 130f), true), "矩形左侧外不应判定为点击");
            Assert.IsFalse(KleinUi.Hit(rect, new Vector2(200f, 400f), true), "矩形下方外不应判定为点击");
            Assert.IsTrue(KleinUi.Hit(rect, new Vector2(100f, 100f), true), "左上角边界应算命中");

            // 屏幕坐标（原点左下）→ GUI 坐标（原点左上）必须翻转 y，
            // 否则所有按钮的点击位置会上下镜像 —— 这是这类 UI 最容易出的错。
            var gui = KleinUi.ScreenToGui(new Vector2(120f, 50f), 600f);
            Assert.AreEqual(120f, gui.x, 0.001f);
            Assert.AreEqual(550f, gui.y, 0.001f, "屏幕转 GUI 坐标必须做上下翻转");

            // 屏幕底部的点（y=50 / 屏高 600）换算后应落在 GUI 底部，命中底部按钮条
            var bottomBar = new Rect(0f, 520f, 400f, 60f);
            Assert.IsTrue(KleinUi.Hit(bottomBar, gui, true), "坐标翻转后没有命中底部按钮条");
            Assert.IsFalse(KleinUi.Hit(new Rect(0f, 20f, 400f, 60f), gui, true),
                           "坐标翻转后错误地命中了顶部按钮条");

            Debug.Log("[T21] UI 命中测试 ✓ 内外判定正确 / 屏幕→GUI 坐标翻转正确（不会上下镜像）");
            yield return null;
        }

        // ------------------------------------------------------------ 贴图替换

        [UnityTest]
        public IEnumerator T22_Visuals_PlaceholderSizesAndSpriteOverride()
        {
            var root = new GameObject("VisualTest").transform;

            // ---- 占位图尺寸必须归一化 ----
            // 占位图内部尺寸并不统一：方块 16px、圆形 32px、圆环 64px（PPU 都是 16），
            // 分别是 1×1 / 2×2 / 4×4 单位。不按 sprite.bounds 反算 localScale 的话，
            // 圆形会渲染成直径的 2 倍、区域圆环会渲染成半径的 4 倍。
            Assert.AreEqual(2f, SpriteFactory.Circle(Color.white).bounds.size.x, 0.001f,
                            "圆形占位图本身应是 2×2 单位（32px / PPU16）");

            var disc = Make.Disc("d", root, Vector3.zero, Color.white, 3f, 0);
            Assert.AreEqual(3f, RenderedSize(disc).x, 0.02f, "圆形占位图直径没有归一化到 3");
            Assert.AreEqual(3f, RenderedSize(disc).y, 0.02f, "圆形占位图直径没有归一化到 3");

            var ring = Make.Ring("r", root, Vector3.zero, Color.white, 4f, 0);
            Assert.AreEqual(4f, RenderedSize(ring).x, 0.02f, "圆环占位图外径没有归一化到 4");
            Assert.AreEqual(4f, RenderedSize(ring).y, 0.02f, "圆环占位图外径没有归一化到 4");

            var square = Make.Square("s", root, Vector3.zero, Color.white, new Vector2(4f, 1f), 0);
            Assert.AreEqual(4f, RenderedSize(square).x, 0.02f, "方块占位图宽度不对");
            Assert.AreEqual(1f, RenderedSize(square).y, 0.02f, "方块占位图高度不对");

            // ---- 自定义贴图按 key 生效，且不再被占位色染 ----
            var set = ScriptableObject.CreateInstance<SpriteSet>();
            var custom = SpriteFactory.Solid(Color.white);   // 1×1 单位
            set.Set(VisualKey.Player_Body, custom, true);
            Visuals.OverrideForTest(set);
            try
            {
                Assert.AreSame(custom, Visuals.Get(VisualKey.Player_Body, SpriteFactory.Circle(Color.white)),
                               "配了图的 key 应返回自定义贴图");
                Assert.IsTrue(Visuals.Has(VisualKey.Player_Body));
                Assert.IsFalse(Visuals.Has(VisualKey.Player_Aim), "没配图的 key 不应报告为有图");

                var tint = Visuals.Tint(VisualKey.Player_Body, new Color(1f, 0.2f, 0.2f, 1f));
                Assert.AreEqual(1f, tint.r, 0.001f, "有自定义贴图后仍被占位色染了");
                Assert.AreEqual(1f, tint.g, 0.001f);
                Assert.AreEqual(1f, tint.b, 0.001f);

                var untouched = Visuals.Tint(VisualKey.Player_Aim, new Color(1f, 0.2f, 0.2f, 1f));
                Assert.AreEqual(0.2f, untouched.g, 0.001f, "没配图的 key 不应改变占位色");

                var big = Make.Visual("big", root, Vector3.zero, VisualKey.Player_Body,
                                      SpriteFactory.Circle(Color.white), Color.white, new Vector2(2f, 2f), 0);
                Assert.AreEqual(2f, RenderedSize(big).x, 0.02f, "自定义贴图没有按目标尺寸缩放");
            }
            finally
            {
                Visuals.OverrideForTest(null);
                Object.Destroy(set);
                Object.Destroy(root.gameObject);
            }

            Debug.Log("[T22] 贴图替换 ✓ 占位图尺寸归一化正确 / 自定义贴图按 key 生效且不再染色");
            yield return null;
        }

        /// <summary>SpriteRenderer 的实际渲染尺寸（世界单位）。</summary>
        private static Vector2 RenderedSize(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return Vector2.zero;
            var b = sr.sprite.bounds.size;
            var s = sr.transform.lossyScale;
            return new Vector2(b.x * Mathf.Abs(s.x), b.y * Mathf.Abs(s.y));
        }

        // ------------------------------------------------------------ 数据资产

        [UnityTest]
        public IEnumerator T12_DataAssets_LoadFromResources()
        {
            var probe = Resources.Load<GameConfig>(GameDatabase.ResourceRoot + "/GameConfig");
            if (probe == null)
            {
                Assert.Ignore("尚未生成数据资产（先执行菜单 Klein/生成数据资产 或 RunBatch）");
            }

            var db = GameDatabase.LoadOrCreate(forceCodeDefaults: false);
            Assert.IsTrue(db.LoadedFromAssets, "Resources 下的数据资产没有被加载");
            Assert.IsNotNull(db.Config);
            Assert.IsNotNull(db.Reactions);
            Assert.GreaterOrEqual(db.Enemies.Count, 4, "资产里的怪物少于 4 个");
            Assert.GreaterOrEqual(db.Spells.Count, 5, "资产里的法术少于 5 个");
            Assert.GreaterOrEqual(db.Modifiers.Count, 5, "资产里的修正器少于 5 个");
            Assert.GreaterOrEqual(db.Items.Count, 7, "资产里的道具少于 7 个");
            Assert.IsNotNull(db.BossEnemy, "资产里没有 BOSS");

            // 反应表 6 条定向规则齐全（对角同元素由代码规则处理）
            int directed = 0;
            foreach (var e in db.Reactions.Entries)
                if (e.Kind != ReactionKind.None && e.Kind != ReactionKind.Recharge) directed++;
            Assert.AreEqual(6, directed, "反应表的定向规则应为 6 条");

            Debug.Log($"[T12] 数据资产加载 ✓ 怪物{db.Enemies.Count} 法术{db.Spells.Count} "
                      + $"修正器{db.Modifiers.Count} 道具{db.Items.Count} 定向反应{directed}");

            yield return null;
        }

        // ------------------------------------------------------------ 敌人编辑器字段

        [UnityTest]
        public IEnumerator T10_EnemyDefinitions_CoverScriptFields()
        {
            yield return Boot();

            var db = GameDatabase.I;
            Assert.GreaterOrEqual(db.Enemies.Count, 4, "怪物原型少于 4 个（策划案列了 4 种画像）");

            foreach (var e in db.Enemies)
            {
                Assert.Greater(e.Health, 0f, e.name + " 血量未配置");
                Assert.Greater(e.MaxInRoom, 0, e.name + " 最大存在数未配置");
                Assert.AreEqual(e.DropTypes.Count, e.DropWeights.Count,
                                e.name + " 掉落种类与权重数量不一致");
                Assert.IsTrue(Mathf.Approximately(e.TotalDropWeight, 100f) || e.DropTypes.Count == 0,
                              e.name + " 掉落权重总和应为 100，实际 " + e.TotalDropWeight);
            }

            var modes = new HashSet<MoveMode>();
            foreach (var e in db.Enemies) modes.Add(e.MoveMode);
            Assert.IsTrue(modes.Contains(MoveMode.Stationary), "缺少「不移动」的怪物");
            Assert.IsTrue(modes.Contains(MoveMode.Continuous), "缺少「持续移动」的怪物");

            Assert.IsNotNull(db.BossEnemy, "缺少 BOSS 定义");
            Assert.IsTrue(db.BossEnemy.IsBoss);

            Debug.Log($"[T10] 怪物编辑器字段完整（{db.Enemies.Count} 个定义，"
                      + $"移动方式 {modes.Count} 种）");
        }
    }
}
