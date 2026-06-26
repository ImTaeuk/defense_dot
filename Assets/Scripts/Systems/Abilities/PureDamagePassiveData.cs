using UnityEngine;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 순수 패시브(조건부 데미지 배수). kind 로 4종(맹공/토벌/쇄도/각성) 구분. </summary>
    [CreateAssetMenu(fileName = "PurePassive", menuName = "DefenseDot/Abilities/Pure Passive")]
    public sealed class PureDamagePassiveData : PassiveAbilityData
    {
        public enum PassiveKind { Onslaught, Cull, Press, Awaken }

        [SerializeField] private PassiveKind kind = PassiveKind.Onslaught;

        public override void ApplyModifiers(AbilityModifiers mods, int level)
        {
            if (mods == null) return;
            switch (kind)
            {
                case PassiveKind.Onslaught: mods.onslaughtLevel += level; break;
                case PassiveKind.Cull:      mods.cullLevel += level; break;
                case PassiveKind.Press:     mods.pressLevel += level; break;
                case PassiveKind.Awaken:    mods.awakenLevel += level; break;
            }
        }
    }
}
