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
        [Tooltip("공격 속도 (초당 공격 횟수)")]
        public float attackSpeed;
        [Tooltip("설치 비용")]
        public int cost;
        [Tooltip("게임 내에 생성될 타워 프리팹")]
        public GameObject prefab;
    }

    /// <summary>
    /// 적의 능력치와 설정을 정의하는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "DefenseDot/EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Tooltip("적의 이름")]
        public string enemyName;
        [Tooltip("최대 체력")]
        public float health;
        [Tooltip("이동 속도")]
        public float moveSpeed;
        [Tooltip("처치 시 획득하는 골드")]
        public int rewardGold;
        [Tooltip("게임 내에 생성될 적 프리팹")]
        public GameObject prefab;
    }
}
