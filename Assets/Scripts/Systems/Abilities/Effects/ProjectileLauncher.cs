using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 유도 투사체 발사를 수행하는 공용 조각입니다. 주축·동반 능력이 함께 씁니다. </summary>
    public static class ProjectileLauncher
    {
        /// <summary> 총구에서 대상으로 유도 투사체를 발사하고 머즐 VFX를 재생합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">발사한 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        /// <param name="source">데미지 산출 기준이 되는 능력</param>
        /// <param name="projectileAsset">투사체 프리팹</param>
        /// <param name="muzzleAsset">발사 머즐 VFX(없으면 null)</param>
        /// <param name="hitVfxAsset">명중 VFX(없으면 null)</param>
        /// <param name="speed">투사체 속도</param>
        /// <param name="pierce">관통 횟수</param>
        /// <param name="range">유효 사거리</param>
        public static void Launch(in AbilityContext ctx, AbilityInstance self, ITargetable target,
            ActiveAbilityData source, AssetReferenceGameObject projectileAsset,
            AssetReferenceGameObject muzzleAsset, AssetReferenceGameObject hitVfxAsset,
            float speed, int pierce, float range)
        {
            if (ctx.Effects == null || projectileAsset == null || !projectileAsset.RuntimeKeyIsValid())
                return;

            if (target == null || !target.IsActive)
                target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, range) : null;
            if (target == null)
                return;

            Vector3 firePos = ctx.FirePosition;   // 타워 총구(없으면 코어 중심)
            ProjectileEffect fx = ctx.Effects.Spawn<ProjectileEffect>(projectileAsset);
            if (fx == null)
                return;   // 스폰 실패 시 이 발동만 무산

            DamageSource src = new DamageSource(source, self, ctx.Modifiers);
            fx.Activate(firePos, target, src, speed, pierce, range, ctx.Finder, hitVfxAsset);

            if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid())
            {
                Vector3 dir = target.Position - firePos;
                Quaternion rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                ctx.Effects.PlayOneShot(muzzleAsset, firePos, rot);
            }
        }
    }
}
