using System.Collections.Generic;
using UnityEngine;

namespace Klein
{
    /// <summary>
    /// 玩家的道具槽与背包。
    /// 策划案：「初始拥有一个道具槽（只能拥有一个主动道具，后续可增加）」，
    /// 空格使用、Shift 切换。
    /// </summary>
    public class PlayerItems : MonoBehaviour
    {
        public ItemDefinition ActiveItem { get; private set; }

        private readonly List<ItemDefinition> _activeOwned = new List<ItemDefinition>();
        private int _activeIndex = -1;
        private float _cooldown;

        private PlayerStats _stats;
        private SpellCaster _caster;

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
            _caster = GetComponent<SpellCaster>();
        }

        private void Update()
        {
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
        }

        public void Acquire(ItemDefinition item)
        {
            if (item == null) return;

            if (item.Kind == ItemKind.Passive)
            {
                _stats.ApplyItem(item);   // 被动立即生效
                return;
            }

            _activeOwned.Add(item);
            if (ActiveItem == null) EquipActive(_activeOwned.Count - 1);
        }

        private void EquipActive(int index)
        {
            if (index < 0 || index >= _activeOwned.Count) return;
            _activeIndex = index;
            ActiveItem = _activeOwned[index];
        }

        public void SwitchActive()
        {
            if (_activeOwned.Count <= 1) return;
            EquipActive((_activeIndex + 1) % _activeOwned.Count);
        }

        public void UseActive()
        {
            if (ActiveItem == null || _cooldown > 0f) return;

            switch (ActiveItem.Effect)
            {
                case ItemEffect.HealPotion:
                    _stats.AddHealth(40f);
                    break;

                case ItemEffect.FlameSpit:
                    if (_stats.Mana < ActiveItem.ManaCost) return;
                    _stats.SpendMana(ActiveItem.ManaCost);
                    FireFan(ActiveItem.Magnitude, 5, 30f);
                    break;

                default:
                    break;
            }

            _cooldown = Mathf.Max(0.1f, ActiveItem.Cooldown);

            if (ActiveItem.SingleUse)
            {
                _activeOwned.RemoveAt(_activeIndex);
                _activeIndex = -1;
                ActiveItem = null;
                if (_activeOwned.Count > 0) EquipActive(0);
            }
        }

        private void FireFan(float damage, int count, float spreadDeg)
        {
            var input = KleinInput.I;
            if (input == null) return;
            Vector2 origin = transform.position;
            Vector2 aim = ((Vector2)input.PointerWorld - origin);
            if (aim.sqrMagnitude < 1e-4f) aim = Vector2.right;
            aim.Normalize();

            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var element = _stats != null ? _stats.Element : ElementType.None;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f);
                float angle = (baseAngle + t * spreadDeg) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                var spec = new ProjectileSpec
                {
                    Mask = element.ToMask(),
                    Innate = element,
                    PlayerElement = element,
                    Damage = damage * (_stats != null ? _stats.AttackPower / 100f : 1f),
                    Speed = _stats != null ? _stats.SpellSpeed : 12f,
                    Range = _stats != null ? _stats.CastRange : 8f,
                    Radius = 0.28f,
                    Knockback = 0.5f,
                    FromPlayer = true,
                    Pierce = 0,
                    Color = element.ToColor(),
                };
                Projectile.Spawn(origin, dir, spec, ProjectileTeam.Player);
            }
        }
    }
}
