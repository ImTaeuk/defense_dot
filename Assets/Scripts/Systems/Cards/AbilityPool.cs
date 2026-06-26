using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Systems.Cards
{
    /// <summary> "신규 능력" 카드 후보 풀(콘텐츠). </summary>
    [CreateAssetMenu(menuName = "DefenseDot/Cards/Ability Pool", fileName = "AbilityPool")]
    public sealed class AbilityPool : ScriptableObject
    {
        public List<AbilityData> abilities = new List<AbilityData>();
    }
}
