// 자가구동 효과 엔티티 베이스 — 반납은 스포너에 위임(풀링 심)
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Abilities.Effects
{
    /// <summary>
    /// 능력이 스폰하는 자가구동 효과의 베이스입니다.
    /// 시간축 거동·데미지는 서브클래스 Update가 수행하고, 종료 시 Release로 반납합니다.
    /// </summary>
    public abstract class AbilityEffect : MonoBehaviour, IPoolable
    {
        private IEffectSpawner spawner;

        /// <summary> 반납 대상 스포너를 주입합니다. </summary>
        public void Bind(IEffectSpawner effectSpawner) { spawner = effectSpawner; }

        /// <summary> 효과를 스포너로 반납합니다. (스포너 없으면 파괴) </summary>
        protected void Release()
        {
            if (spawner != null) spawner.Release(this);
            else Destroy(gameObject);
        }

        /// <summary> 풀 재사용 시 초기화 훅입니다. </summary>
        public virtual void OnSpawn() { }

        /// <summary> 반납 시 정리 훅입니다. </summary>
        public virtual void OnDespawn() { }
    }
}
