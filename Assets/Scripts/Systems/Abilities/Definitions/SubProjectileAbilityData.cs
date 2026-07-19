// 동반 투사체 능력 — 주축 발사에 함께 나가는 유도 투사체
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 주축 발사에 동반해 함께 나가는 투사체 능력입니다. </summary>
    [CreateAssetMenu(fileName = "SubProjectileAbility", menuName = "DefenseDot/Abilities/Sub Projectile")]
    public sealed class SubProjectileAbilityData : SubAbilityData
    {
        [SerializeField] private AssetReferenceGameObject projectileAsset;
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private float damagePerLevel = 1f;
        [SerializeField] private float speed = 12f;
        [SerializeField] private int pierce = 1;
        [SerializeField] private float range = 30f;
        [SerializeField] private AssetReferenceGameObject muzzleAsset;   // 발사 머즐 VFX
        [SerializeField] private AssetReferenceGameObject hitVfxAsset;   // 명중 VFX

        /// <summary> 레벨별 데미지입니다. </summary>
        /// <param name="level">현재 레벨</param>
        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        /// <summary> 투사체·머즐·명중 VFX 프리팹(예열 대상). </summary>
        public override IEnumerable<AssetReferenceGameObject> EffectAssets
        {
            get
            {
                if (projectileAsset != null && projectileAsset.RuntimeKeyIsValid())
                    yield return projectileAsset;
                if (muzzleAsset != null && muzzleAsset.RuntimeKeyIsValid())
                    yield return muzzleAsset;
                if (hitVfxAsset != null && hitVfxAsset.RuntimeKeyIsValid())
                    yield return hitVfxAsset;
            }
        }

        /// <summary> 대상에게 유도 투사체를 발사합니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">발사 대상</param>
        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            ProjectileLauncher.Launch(ctx, self, target, this,
                projectileAsset, muzzleAsset, hitVfxAsset, speed, pierce, range);
        }
    }
}
