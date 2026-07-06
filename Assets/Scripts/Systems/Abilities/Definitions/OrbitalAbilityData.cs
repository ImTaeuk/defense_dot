// 오비탈 능력(상시) — 장착 시 회전 위성 스폰, 해제 시 반납
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DefenseDot.Systems.Abilities.Effects;

namespace DefenseDot.Systems.Abilities.Definitions
{
    /// <summary> 장착 동안 코어 주위를 도는 위성을 유지하는 상시 능력입니다. </summary>
    [CreateAssetMenu(fileName = "OrbitalAbility", menuName = "DefenseDot/Abilities/Orbital")]
    public sealed class OrbitalAbilityData : ActiveAbilityData, IAbilityLifecycle
    {
        [SerializeField] private AssetReferenceGameObject orbiterAsset;
        [SerializeField] private float baseDamage = 3f;
        [SerializeField] private float damagePerLevel = 2f;
        [SerializeField] private float rotSpeed = 2f;

        public override float ValueAtLevel(int level) { return baseDamage + damagePerLevel * (level - 1); }

        /// <summary> 위성 프리팹(예열 대상). </summary>
        public override IEnumerable<AssetReferenceGameObject> EffectAssets
        {
            get { if (orbiterAsset != null && orbiterAsset.RuntimeKeyIsValid()) yield return orbiterAsset; }
        }

        public void OnEquip(in AbilityContext ctx, AbilityInstance self)
        {
            if (ctx.Effects == null || orbiterAsset == null || !orbiterAsset.RuntimeKeyIsValid()) return;
            OrbiterSetEffect fx = ctx.Effects.Spawn<OrbiterSetEffect>(orbiterAsset);
            if (fx == null) return;   // 스폰 실패 시 장착 무산
            DamageSource src = new DamageSource(this, self, ctx.Modifiers);
            fx.Activate(ctx.Origin, 1 + self.level, src, rotSpeed, ctx.Finder);
            self.runtimeState = fx;
        }

        public void OnUnequip(in AbilityContext ctx, AbilityInstance self)
        {
            if (self.runtimeState is AbilityEffect fx) ctx.Effects.Release(fx);
            self.runtimeState = null;
        }

        // 회전·데미지는 효과가 자가 수행 → Tick·Fire는 비움(상시 능력)
        public override void Tick(in AbilityContext ctx, AbilityInstance self, float deltaTime) { }

        protected override void Fire(in AbilityContext ctx, AbilityInstance self, DefenseDot.Core.ITargetable target) { }
    }
}
