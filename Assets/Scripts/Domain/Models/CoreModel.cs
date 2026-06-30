// 코어(본진) 체력 상태를 소유·통지하는 도메인 모델
using UnityEngine;
using DefenseDot.Domain;

namespace DefenseDot.Domain.Models
{
    /// <summary>
    /// 코어(본진) 체력 상태를 소유하고 변경·파괴를 통지하는 도메인 모델입니다.
    /// </summary>
    public class CoreModel : BaseModel
    {
        private readonly ReactiveProperty<HealthState> health = new ReactiveProperty<HealthState>(new HealthState(40f, 40f));

        /// <summary> 코어 체력(현재/최대/비율) 상태입니다. (읽기 전용 RP) </summary>
        public IReadOnlyReactiveProperty<HealthState> Health => health;

        /// <summary> 현재 코어 체력입니다. </summary>
        public float CurrentHp => health.Value.Hp;

        /// <summary> 최대 코어 체력입니다. </summary>
        public float MaxHp => health.Value.MaxHp;

        /// <summary> 현재 체력 비율(0~1)입니다. </summary>
        public float HealthRatio => health.Value.Ratio;

        /// <summary> 코어가 파괴(HP 0)되면 발생합니다. </summary>
        public event System.Action OnCoreDestroyed;

        /// <summary> 최대 체력을 설정하고 현재 체력을 가득 채웁니다. </summary>
        public void Configure(float max)
        {
            health.SetValueAndForceNotify(new HealthState(max, max));
        }

        /// <summary> 현재 체력을 절대값으로 설정합니다. (헤드룸 표시용 — 파괴 통지 없음) </summary>
        public void SetCurrent(float value)
        {
            float max = health.Value.MaxHp;
            health.Value = new HealthState(Mathf.Clamp(value, 0f, max), max);
        }

        /// <summary> 코어에 피해를 적용합니다. HP가 0에 도달하면 파괴를 통지합니다. </summary>
        public void ApplyDamage(float amount)
        {
            HealthState current = health.Value;
            if (current.Hp <= 0f) return;
            float hp = Mathf.Max(0f, current.Hp - amount);
            health.Value = new HealthState(hp, current.MaxHp);
            if (hp <= 0f) OnCoreDestroyed?.Invoke();
        }
    }
}
