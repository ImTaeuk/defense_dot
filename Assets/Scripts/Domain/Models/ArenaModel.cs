// 현재 아레나/스폰 반경 상태를 소유·통지하는 도메인 모델
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 현재 아레나/스폰 반경 상태를 소유하고 변경을 통지하는 도메인 모델입니다.
    /// 반경은 동적으로 변하며(Expand/Shrink), 적은 이를 비율로 참조합니다.
    /// </summary>
    public class ArenaModel : BaseModel
    {
        private float arenaRadius;
        private float coreRadius;
        private float spawnInnerMargin;
        private float spawnOuterMargin;
        private int maxAlive;

        /// <summary> 반경이 변경되면 발생합니다. </summary>
        public event System.Action OnRadiusChanged;

        /// <summary> 현재 아레나 반경입니다. </summary>
        public float ArenaRadius => arenaRadius;

        /// <summary> 코어 반경입니다. </summary>
        public float CoreRadius => coreRadius;

        /// <summary> 스폰 최소 반경(코어 + 안쪽 여백)입니다. </summary>
        public float SpawnMinRadius => coreRadius + spawnInnerMargin;

        /// <summary> 스폰 최대 반경(아레나 - 바깥 여백)입니다. </summary>
        public float SpawnMaxRadius => arenaRadius - spawnOuterMargin;

        /// <summary> 수용 한계입니다. </summary>
        public int MaxAlive => maxAlive;

        /// <summary> 초기 형상 값을 설정합니다. </summary>
        public void Initialize(float arenaRadius, float coreRadius, float spawnInnerMargin, float spawnOuterMargin, int maxAlive)
        {
            this.arenaRadius = arenaRadius;
            this.coreRadius = coreRadius;
            this.spawnInnerMargin = spawnInnerMargin;
            this.spawnOuterMargin = spawnOuterMargin;
            this.maxAlive = maxAlive;
        }

        /// <summary> 아레나를 확장하고 통지합니다. </summary>
        public void Expand(float amount) => SetRadius(arenaRadius + amount);

        /// <summary> 아레나를 축소하고 통지합니다. </summary>
        public void Shrink(float amount) => SetRadius(arenaRadius - amount);

        private void SetRadius(float value)
        {
            // 스폰 범위 음수 방지
            float min = coreRadius + spawnInnerMargin + spawnOuterMargin;
            float clamped = UnityEngine.Mathf.Max(min, value);
            if (SetField(ref arenaRadius, clamped)) OnRadiusChanged?.Invoke();
        }
    }
}
