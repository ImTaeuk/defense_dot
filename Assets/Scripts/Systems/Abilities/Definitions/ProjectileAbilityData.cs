// 발사체 능력(이산) — 쿨다운마다 최근접 적에 유도 투사체
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 쿨다운마다 유도 투사체를 발사하는 발사체 능력입니다. </summary>
    [CreateAssetMenu(fileName = "ProjectileAbility", menuName = "DefenseDot/Abilities/Projectile")]
    public sealed class ProjectileAbilityData : ActiveAbilityData
    {
        [SerializeField] private AssetReferenceGameObject projectileAsset;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float damagePerLevel = 1f;
        [SerializeField] private float speed = 12f;
        [SerializeField] private int pierce = 1;
        [SerializeField] private float range = 30f;
        [SerializeField] private AssetReferenceGameObject muzzleAsset;   // 발사 머즐 VFX
        [SerializeField] private AssetReferenceGameObject hitVfxAsset;   // 명중 VFX

        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        /// <summary> 발사체·머즐·명중 VFX 프리팹(예열 대상). </summary>
        public override IEnumerable<AssetReferenceGameObject> EffectAssets
        {
            get
            {
                if (projectileAsset != null && projectileAsset.RuntimeKeyIsValid()) yield return projectileAsset;
                if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid()) yield return muzzleAsset;
                if (hitVfxAsset != null && hitVfxAsset.RuntimeKeyIsValid()) yield return hitVfxAsset;
            }
        }

        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            if (ctx.Effects == null || projectileAsset == null || !projectileAsset.RuntimeKeyIsValid()) return;
            if (target == null || !target.IsActive)
                target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, range) : null;
            if (target == null) return;

            ProjectileEffect fx = ctx.Effects.Spawn<ProjectileEffect>(projectileAsset);
            DamageSource src = new DamageSource(this, self, ctx.Modifiers);
            fx.Activate(ctx.Origin, target, src, speed, pierce, range, ctx.Finder, hitVfxAsset);
            if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid())
            {
                Vector3 dir = target.Position - ctx.Origin;
                Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                ctx.Effects.PlayOneShot(muzzleAsset, ctx.Origin, rot);
            }
        }
    }
}
