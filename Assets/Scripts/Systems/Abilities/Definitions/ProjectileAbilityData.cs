// 발사체 능력(이산) — 쿨다운마다 최근접 적에 유도 투사체
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 쿨다운마다 유도 투사체를 발사하는 발사체 능력입니다. </summary>
    [CreateAssetMenu(fileName = "ProjectileAbility", menuName = "DefenseDot/Abilities/Projectile")]
    public sealed class ProjectileAbilityData : ActiveAbilityData
    {
        [SerializeField] private ProjectileEffect projectilePrefab;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float damagePerLevel = 1f;
        [SerializeField] private float speed = 12f;
        [SerializeField] private int pierce = 1;
        [SerializeField] private float range = 30f;
        [SerializeField] private GameObject muzzlePrefab;   // 발사 머즐 VFX(Hovl Flash)

        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            if (ctx.Effects == null || projectilePrefab == null) return;
            if (target == null || !target.IsActive)
                target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, range) : null;
            if (target == null) return;

            ProjectileEffect fx = ctx.Effects.Spawn(projectilePrefab);
            float dmg = ValueAtLevel(self.level) + ctx.Modifiers.damageBonus;
            fx.Activate(ctx.Origin, target, dmg, speed, pierce, range, ctx.Finder);
            if (muzzlePrefab != null)
            {
                Vector3 dir = target.Position - ctx.Origin;
                Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                VfxPlayer.SpawnOneShot(muzzlePrefab, ctx.Origin, rot);
            }
        }
    }
}
