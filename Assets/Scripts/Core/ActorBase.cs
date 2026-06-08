using UnityEngine;

namespace DefenseDot.Core
{
    /// <summary>
    /// 모든 인게임 액터(몬스터, 타워 등)의 공통 기반 클래스입니다.
    /// 데이터와 상태를 관리하며 인터페이스를 구현합니다.
    /// </summary>
    /// <typeparam name="TData">액터가 사용할 데이터 클래스 (TowerData, EnemyData 등)</typeparam>
    public abstract class ActorBase<TData> : MonoBehaviour, IActor, IDamageable where TData : ScriptableObject
    {
        [Header("Actor Settings")]
        [SerializeField] protected TData data;
        [SerializeField] protected float currentHealth;

        protected ActorState currentState = ActorState.Idle;

        /// <summary> 상태 변경 시 발생 (View가 구독해 애니메이션 전환) </summary>
        public event System.Action<ActorState> StateChanged;

        #region IActor Implementation
        public virtual Vector3 Position => transform.position;
        public ActorState CurrentState => currentState;

        public virtual void SetState(ActorState newState)
        {
            if (currentState == newState) return;
            currentState = newState;
            OnStateChanged(newState);
            StateChanged?.Invoke(newState);
        }
        #endregion

        #region IDamageable Implementation
        public virtual void TakeDamage(float amount)
        {
            if (currentState == ActorState.Dead) return;

            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                Die();
            }
        }
        #endregion

        protected virtual void OnStateChanged(ActorState newState) { }

        protected virtual void Die()
        {
            SetState(ActorState.Dead);
            // 풀링 반환 등 추가 로직
        }

        /// <summary>
        /// 액터 초기화 시 데이터를 설정합니다.
        /// </summary>
        public virtual void Initialize(TData actorData)
        {
            data = actorData;
            // 초기 체력 설정 로직 등 (데이터 구조에 따라 다름)
        }
    }
}
