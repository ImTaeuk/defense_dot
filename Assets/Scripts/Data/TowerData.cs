using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 타워의 능력치와 설정을 정의하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerData", menuName = "DefenseDot/TowerData")]
    public class TowerData : ScriptableObject
    {
        [Tooltip("타워의 이름")]
        public string towerName;
        [Tooltip("공격당 데미지")]
        public float attackDamage;
        [Tooltip("사거리")]
        public float attackRange;
        [Tooltip("범위(AoE) 공격의 블래스트 반경")]
        public float aoeRadius = 3f;
        [Tooltip("공격 속도 (초당 공격 횟수)")]
        public float attackSpeed;
        [Tooltip("설치 비용")]
        public int cost;
        [Tooltip("게임 내에 생성될 타워 프리팹")]
        public GameObject prefab;
        [Tooltip("이 타워(캐릭터)의 합성 계보")]
        public DefenseDot.Systems.Cards.FusionRecipeSet fusionLineage;
    }
}
