// 코어(본진) 체력 상태를 소유·통지하는 도메인 모델
using UnityEngine;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 코어(본진) 체력 상태를 소유하고 변경·파괴를 통지하는 도메인 모델입니다.
    /// </summary>
    [System.Serializable]
    public class CoreModel : BaseModel
    {
        [SerializeField] private float currentHp;
        [SerializeField] private float maxHp = 40f;

        /// <summary>
        /// 체력 비율(0~1)이 변경되면 발생합니다.
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action<float> OnHealthChanged;

        /// <summary>
        /// 코어가 파괴(HP 0)되면 발생합니다.
        /// </summary>
        [field: System.NonSerialized]
        public event System.Action OnCoreDestroyed;

        /// <summary>
        /// 현재 코어 체력입니다.
        /// </summary>
        public float CurrentHp => currentHp;

        /// <summary>
        /// 최대 코어 체력입니다.
        /// </summary>
        public float MaxHp => maxHp;

        /// <summary>
        /// 현재 체력 비율(0~1)입니다.
        /// </summary>
        public float HealthRatio => maxHp > 0f ? currentHp / maxHp : 0f;

        /// <summary>
        /// 최대 체력을 설정하고 현재 체력을 가득 채웁니다.
        /// </summary>
        public void Configure(float max)
        {
            maxHp = max;
            currentHp = max;
            OnHealthChanged?.Invoke(HealthRatio);
        }

        /// <summary>
        /// 코어에 피해를 적용합니다. HP가 0에 도달하면 파괴를 통지합니다.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (currentHp <= 0f) return;
            currentHp = Mathf.Max(0f, currentHp - amount);
            OnHealthChanged?.Invoke(HealthRatio);
            if (currentHp <= 0f) OnCoreDestroyed?.Invoke();
        }
    }
}
