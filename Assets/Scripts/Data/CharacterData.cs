using UnityEngine;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.Data
{
    /// <summary> 캐릭터 1인의 정의입니다. 기본 공격(능력·모션·공격속도)과 전용 계보를 소유합니다. </summary>
    [CreateAssetMenu(fileName = "Character", menuName = "DefenseDot/Character")]
    public sealed class CharacterData : ScriptableObject
    {
        /// <summary> 이 캐릭터의 기본 공격(tier=Basic 능력). 발사 주기의 주인입니다. </summary>
        [SerializeField] private AbilityData basicAttack;
        /// <summary> 기본 공격 모션(재생 속도는 공격속도로 스케일). 없으면 즉시 발사. </summary>
        [SerializeField] private AnimationClip castAnimation;
        /// <summary> 기본 공격속도(초당 횟수). CombatStats 초기값. </summary>
        [SerializeField] private float baseAttackSpeed = 1f;
        /// <summary> 이 캐릭터 전용 계보(공통 세트와 합집합). 없으면 공통만. </summary>
        [SerializeField] private FusionRecipeSet characterLineage;

        /// <summary> 이 캐릭터의 기본 공격입니다. </summary>
        public AbilityData BasicAttack => basicAttack;
        /// <summary> 기본 공격 모션입니다. </summary>
        public AnimationClip CastAnimation => castAnimation;
        /// <summary> 기본 공격속도(초당 횟수)입니다. </summary>
        public float BaseAttackSpeed => baseAttackSpeed;
        /// <summary> 캐릭터 전용 계보입니다(없으면 null). </summary>
        public FusionRecipeSet CharacterLineage => characterLineage;
    }
}