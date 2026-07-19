// 주축 범위 능력 — 무기 주기에 맞춰 잔류형 범위 존을 떨어뜨림
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Core;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 무기의 주축이 되는 범위 능력입니다. 투사체 대신 범위 존으로 주 공격을 대체합니다. </summary>
    [CreateAssetMenu(fileName = "MainAreaWaveAbility", menuName = "DefenseDot/Abilities/Main AreaWave")]
    public sealed class MainAreaWaveAbilityData : MainAbilityData
    {
        [SerializeField] private AssetReferenceGameObject zoneAsset;
        [SerializeField] private float baseDamage = 3f;
        [SerializeField] private float damagePerLevel = 2f;
        [SerializeField] private float radius = 5f;
        [SerializeField] private float duration = 1.5f;
        [SerializeField] private float range = 30f;   // 타겟(낙하 지점) 탐색 범위

        /// <summary> 레벨별 데미지입니다. </summary>
        /// <param name="level">현재 레벨</param>
        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        protected override float Range => range;

        /// <summary> 존 프리팹(예열 대상). </summary>
        public override IEnumerable<AssetReferenceGameObject> EffectAssets
        {
            get
            {
                if (zoneAsset != null && zoneAsset.RuntimeKeyIsValid())
                    yield return zoneAsset;
            }
        }

        /// <summary> 대상 위치에 범위 존을 떨어뜨립니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">이 능력의 런타임 인스턴스</param>
        /// <param name="target">낙하 지점을 정할 대상</param>
        protected override void Fire(in AbilityContext ctx, AbilityInstance self, ITargetable target)
        {
            AreaZoneLauncher.Drop(ctx, self, target, this, zoneAsset, radius, duration, range);
        }
    }
}
