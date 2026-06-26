using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities
{
    /// <summary> 패시브들이 누적하는 합산 보정값입니다. (필드는 패시브 추가에 따라 확장) </summary>
    public sealed class AbilityModifiers
    {
        /// <summary> 가산 공격력 보너스. </summary>
        public float damageBonus;
        /// <summary> 쿨다운 감소(초). </summary>
        public float cooldownReduction;

        // 순수 패시브 누적 레벨 (RecalculateModifiers 에서 재계산)
        public int onslaughtLevel;  // 맹공: 비보스 & HP>50%
        public int cullLevel;       // 토벌: 비보스
        public int pressLevel;      // 쇄도: 생존 적 수 비례
        public int awakenLevel;     // 각성: 라운드 비례

        /// <summary> 실시간 전투 상태(셋업 시 1회 주입, 재계산 시 보존). </summary>
        public ICombatState combatState;

        /// <summary> 누적 보정값을 0으로 초기화(combatState 는 보존). </summary>
        public void ResetAccumulated()
        {
            damageBonus = 0f;
            cooldownReduction = 0f;
            onslaughtLevel = 0;
            cullLevel = 0;
            pressLevel = 0;
            awakenLevel = 0;
        }

        /// <summary> 순수 패시브 조건부 데미지 배수(소스당 +500% cap). </summary>
        public float ConditionalMultiplier(ICombatTargetInfo target)
        {
            float mult = 1f;
            bool boss = target != null && target.IsBoss;
            float hp = target != null ? target.HealthRatio : 1f;
            int round = combatState != null ? combatState.Round : 1;
            int alive = combatState != null ? combatState.AliveEnemyCount : 0;

            if (onslaughtLevel > 0 && !boss && hp > 0.5f)
                mult *= 1f + Cap(onslaughtLevel * 0.12f);
            if (cullLevel > 0 && !boss)
                mult *= 1f + Cap(cullLevel * 0.10f);
            if (pressLevel > 0)
                mult *= 1f + Cap(Mathf.Min(pressLevel * 0.15f, alive * pressLevel * 0.01f));
            if (awakenLevel > 0)
                mult *= 1f + Cap(Mathf.Min(awakenLevel * 0.20f, round * awakenLevel * 0.01f));
            return mult;
        }

        private static float Cap(float value) => Mathf.Min(5f, value);
    }
}
