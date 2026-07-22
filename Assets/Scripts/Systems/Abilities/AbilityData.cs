using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 능력의 정적 설계도(추상). 능력 1종 = 이 파생형의 에셋 1개. </summary>
    public abstract class AbilityData : ScriptableObject
    {
        /// <summary> 고유 식별자. </summary>
        public string id;
        /// <summary> 표시 이름. </summary>
        public string displayName;
        /// <summary> 카드/슬롯 아이콘. </summary>
        public Sprite icon;
        /// <summary> 등급/티어. </summary>
        public int rarity;
        /// <summary> 최대 레벨. </summary>
        public int maxLevel = 5;
        /// <summary> 강화 기본 비용(능력별). </summary>
        public int baseCost = 30;
        /// <summary> 카드 표시용 설명(선택). </summary>
        [TextArea] public string description;

        /// <summary> 위계. 카드 가중치·비용·기본 공격 판정에 쓰입니다. </summary>
        public AbilityTier tier;
        /// <summary> 형태. 실행기 분기·3D 모션 분류의 기준입니다. </summary>
        public AbilityKind kind;

        /// <summary> 이 능력이 사용하는 모든 풀링 프리팹(예열 대상). </summary>
        public virtual IEnumerable<AssetReferenceGameObject> EffectAssets
            => System.Array.Empty<AssetReferenceGameObject>();
    }
}
