using UnityEngine;

namespace DefenseDot.Systems.Abilities
{
    /// <summary>
    /// 주축 공격 능력(추상). 공격 모션의 주인이며 발사 주기의 기준입니다.
    /// 스스로 발사하지 않고 CoreWeapon 이 발사 시점을 정합니다. 코어에 1개만 장착됩니다.
    /// </summary>
    public abstract class MainAbilityData : ActiveAbilityData
    {
        /// <summary> 공격 모션. 없으면 모션 없이 즉시 발사합니다. </summary>
        [SerializeField] private AnimationClip castAnimation;

        /// <summary> 타워 기본 주기에 더할 시간(초). 음수면 빨라집니다. </summary>
        public float cycleDelta;

        /// <summary> 공격 모션(외부 조회용). </summary>
        public AnimationClip CastAnimation => castAnimation;
    }
}
