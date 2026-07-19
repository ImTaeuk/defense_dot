using UnityEngine.AddressableAssets;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary> 잔류형 범위 존 생성을 수행하는 공용 조각입니다. 주축·자율 범위 능력이 함께 씁니다. </summary>
    public static class AreaZoneLauncher
    {
        /// <summary> 대상 위치에 잔류형 범위 존을 떨어뜨립니다. </summary>
        /// <param name="ctx">능력 구동 컨텍스트</param>
        /// <param name="self">발동한 능력의 런타임 인스턴스</param>
        /// <param name="target">낙하 지점을 정할 대상</param>
        /// <param name="source">데미지 산출 기준이 되는 능력</param>
        /// <param name="zoneAsset">존 프리팹</param>
        /// <param name="radius">존 반경</param>
        /// <param name="duration">존 지속 시간(초)</param>
        /// <param name="range">대상 재탐색 범위</param>
        public static void Drop(in AbilityContext ctx, AbilityInstance self, ITargetable target,
            ActiveAbilityData source, AssetReferenceGameObject zoneAsset,
            float radius, float duration, float range)
        {
            if (ctx.Effects == null || zoneAsset == null || !zoneAsset.RuntimeKeyIsValid())
                return;

            if (target == null || !target.IsActive)
                target = ctx.Finder != null ? ctx.Finder.FindNearest(ctx.Origin, range) : null;
            if (target == null)
                return;

            AreaZoneEffect fx = ctx.Effects.Spawn<AreaZoneEffect>(zoneAsset);
            if (fx == null)
                return;   // 스폰 실패 시 이 발동만 무산

            DamageSource src = new DamageSource(source, self, ctx.Modifiers);
            fx.Activate(target.Position, radius, src, duration, ctx.Finder);
        }
    }
}
