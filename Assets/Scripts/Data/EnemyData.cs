using UnityEngine;

namespace DefenseDot.Data
{
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
        [Tooltip("코어 도달 시 입히는 피해")]
        public float coreDamage = 1f;
        [Tooltip("게임 내에 생성될 적 프리팹")]
        public GameObject prefab;
    }
}
