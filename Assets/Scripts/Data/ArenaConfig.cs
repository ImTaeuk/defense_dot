using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 원형 아레나의 초기 형상·규칙 값을 담는 ScriptableObject입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewArenaConfig", menuName = "DefenseDot/ArenaConfig")]
    public class ArenaConfig : ScriptableObject
    {
        /// <summary> 초기 아레나 반경 </summary>
        public float arenaRadius = 29f;
        /// <summary> 코어 반경 </summary>
        public float coreRadius = 2.2f;
        /// <summary> 코어로부터 스폰 안쪽 여백 </summary>
        public float spawnInnerMargin = 4f;
        /// <summary> 경계로부터 스폰 바깥 여백 </summary>
        public float spawnOuterMargin = 2f;
        /// <summary> 동시 생존 적 수용 한계 </summary>
        public int maxAlive = 80;
        /// <summary> 기본 공전 각속도(라디안/초) </summary>
        public float baseAngularSpeed = 0.5f;
        /// <summary> 적 배치 높이(Y) </summary>
        public float enemyHeight = 0.8f;
    }
}
