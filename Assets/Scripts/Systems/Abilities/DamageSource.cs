using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 명중 시점에 데미지를 산출하는 소스. 능력·인스턴스·보정을 참조로 들고
    /// 명중 순간의 라이브 레벨·보정으로 계산한다(비행 중 레벨업·패시브 반영).
    /// </summary>
    public readonly struct DamageSource
    {
        private readonly ActiveAbilityData ability;
        private readonly AbilityInstance instance;
        private readonly AbilityModifiers modifiers;

        public DamageSource(ActiveAbilityData ability, AbilityInstance instance, AbilityModifiers modifiers)
        {
            this.ability = ability;
            this.instance = instance;
            this.modifiers = modifiers;
        }

        /// <summary> 명중 대상에 대한 최종 데미지(라이브 레벨·보정 반영). </summary>
        public float Resolve(ITargetable target)
        {
            if (ability == null || instance == null) return 0f;
            float dmg = ability.ValueAtLevel(instance.level);
            if (modifiers != null)
            {
                dmg += modifiers.damageBonus;
                dmg *= modifiers.ConditionalMultiplier(target as ICombatTargetInfo);
            }
            return dmg;
        }
    }
}
