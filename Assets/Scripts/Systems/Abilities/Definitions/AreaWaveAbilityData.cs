// AOE 능력(이산) — 쿨다운마다 최근접 적 위치에 잔류형 범위 존 생성
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 쿨다운마다 잔류형 AOE 존을 적 위치에 떨구는 범위 능력입니다. </summary>
    [CreateAssetMenu(fileName = "AreaWaveAbility", menuName = "DefenseDot/Abilities/AreaWave")]
    public sealed class AreaWaveAbilityData : ActiveAbilityData
    {
        [SerializeField] private AreaZoneEffect zonePrefab;
        [SerializeField] private float baseDamage = 3f;
        [SerializeField] private float damagePerLevel = 2f;
        [SerializeField] private float radius = 5f;
        [SerializeField] private float duration = 1.5f;
        [SerializeField] private float range = 30f;   // 타겟(낙하 지점) 탐색 범위

        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            if (ctx.Effects == null || zonePrefab == null) return;
            if (target == null || !target.IsActive)
                target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, range) : null;
            if (target == null) return;

            AreaZoneEffect fx = ctx.Effects.Spawn(zonePrefab);
            DamageSource src = new DamageSource(this, self, ctx.Modifiers);
            fx.Activate(target.Position, radius, src, duration, ctx.Finder);
        }
    }
}
