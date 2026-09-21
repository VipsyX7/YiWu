using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 房间运行时。负责：玩家首次进入时按类型生成内容、清怪判定、超时加刷。
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        public RoomNode Node { get; private set; }
        public FloorRuntime Floor { get; private set; }

        private readonly List<EnemyHealth> _alive = new List<EnemyHealth>();
        private float _timer;
        private int _spawnedTotal;
        private bool _extraWaveDone;
        private bool _playerInside;

        public int AliveCount => _alive.Count;
        public bool IsCleared => Node != null && Node.Cleared;

        public static RoomController Create(Transform parent, RoomNode node, FloorRuntime floor)
        {
            var go = new GameObject("Room_" + node.GridIndex.x + "_" + node.GridIndex.y + "_" + node.Type);
            go.transform.SetParent(parent, false);
            var rc = go.AddComponent<RoomController>();
            rc.Node = node;
            rc.Floor = floor;
            go.transform.position = new Vector3(node.Center.x, node.Center.y, 0f);
            return rc;
        }

        // ---------------------------------------------------------------- 进入

        public void OnPlayerEntered()
        {
            if (_playerInside) return;
            _playerInside = true;
            Node.Visited = true;

            if (!Node.Populated)
            {
                Node.Populated = true;
                Populate();
            }
            RefreshCleared();
        }

        public void OnPlayerExited() { _playerInside = false; }

        // ---------------------------------------------------------------- 生成

        private void Populate()
        {
            var db = GameDatabase.I;
            switch (Node.Type)
            {
                case RoomType.Start:
                    Node.Cleared = true;
                    break;

                case RoomType.Trash:
                    SpawnWave(1f);
                    break;

                case RoomType.Boss:
                    SpawnBoss();
                    break;

                case RoomType.Shop:
                    PopulateShop(db);
                    Node.Cleared = true;
                    break;

                case RoomType.Spell:
                    PopulateSpellRoom(db);
                    Node.Cleared = true;
                    break;

                case RoomType.Chest:
                    PopulateChestRoom();
                    Node.Cleared = true;
                    break;
            }
        }

        private Vector2 RandomPoint(float pad = 2.5f)
        {
            var b = Node.InnerBounds;
            float x = Random.Range(b.xMin + pad, b.xMax - pad);
            float y = Random.Range(b.yMin + pad, b.yMax - pad);
            return new Vector2(x, y);
        }

        private void SpawnWave(float budgetScale)
        {
            if (GameRuntime.I == null) return;
            int budget = Mathf.RoundToInt(4f * budgetScale);
            int guard = 0;
            var used = new Dictionary<EnemyDefinition, int>();

            while (budget > 0 && guard++ < 40 && _spawnedTotal < GameRuntime.I.Cfg.RoomSpawnCap)
            {
                var def = GameDatabase.I.PickTrashEnemy();
                if (def == null) break;
                used.TryGetValue(def, out int n);
                if (n >= def.MaxInRoom) { budget -= 1; continue; }

                used[def] = n + 1;
                SpawnEnemy(def, RandomPoint());
                budget -= Mathf.Max(1, Mathf.RoundToInt(def.CostWeight));
            }
        }

        private void SpawnBoss()
        {
            var def = GameDatabase.I.BossEnemy;
            if (def == null) { Node.Cleared = true; return; }
            SpawnEnemy(def, Node.Center + new Vector2(0f, 2.5f));
        }

        private void SpawnEnemy(EnemyDefinition def, Vector2 pos)
        {
            var e = EnemyFactory.Spawn(def, pos, this);
            if (e == null) return;
            _alive.Add(e);
            _spawnedTotal++;
        }

        private void PopulateShop(GameDatabase db)
        {
            var items = db.PickShopItems(3);
            float spacing = 3.5f;
            for (int i = 0; i < items.Count; i++)
            {
                float off = (i - (items.Count - 1) * 0.5f) * spacing;
                ItemPedestal.Create(transform, Node.Center + new Vector2(off, 1.5f),
                                    items[i], Random.Range(GameRuntime.I.Cfg.ShopPriceMin,
                                                           GameRuntime.I.Cfg.ShopPriceMax + 1));
            }
        }

        private void PopulateSpellRoom(GameDatabase db)
        {
            var card = db.PickSpellCard();
            if (card == null) return;
            SpellCardPickup.Create(transform, Node.Center, card);
        }

        private void PopulateChestRoom()
        {
            Chest.Create(transform, Node.Center, GameDatabase.I.PickChestItem());
        }

        // ---------------------------------------------------------------- 清怪

        public void OnEnemyKilled(EnemyHealth e)
        {
            _alive.Remove(e);
            if (GameRuntime.I != null) GameRuntime.I.ReportKill();
            RefreshCleared();
        }

        public void ForceClear()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            }
            _alive.Clear();
            Node.Cleared = true;
            if (Floor != null) Floor.OnRoomStateChanged();
        }

        private void RefreshCleared()
        {
            if (Node == null || Node.Cleared) return;
            if (Node.Type == RoomType.Trash || Node.Type == RoomType.Boss)
            {
                if (_alive.Count == 0 && _spawnedTotal > 0)
                {
                    Node.Cleared = true;
                    if (Floor != null) Floor.OnRoomStateChanged();
                }
            }
        }

        private void Update()
        {
            if (Node == null || Node.Cleared) return;
            if (!_playerInside) return;

            _timer += Time.deltaTime;
            if (!_extraWaveDone && _timer >= GameRuntime.I.Cfg.RoomClearTimeout)
            {
                _extraWaveDone = true;
                SpawnWave(1.5f);
            }
        }

        /// <summary>玩家在本房间首次造成伤害 → 全房怪物转为追踪。</summary>
        public void AggroAll()
        {
            for (int i = 0; i < _alive.Count; i++)
                if (_alive[i] != null) _alive[i].SetAggro(true);
        }

        /// <summary>BOSS 房清空后生成传送门。</summary>
        public void SpawnPortal()
        {
            Portal.Create(transform, Node.Center + new Vector2(0f, 1f), Floor.FloorIndex + 1);
        }

        private void OnDrawGizmosSelected()
        {
            if (Node == null) return;
            var b = Node.InnerBounds;
            Gizmos.color = Node.Type.ToColor();
            var c = new Vector3(b.xMin + b.width * 0.5f, b.yMin + b.height * 0.5f, 0f);
            Gizmos.DrawWireCube(c, new Vector3(b.width, b.height, 0.1f));
        }
    }
}
