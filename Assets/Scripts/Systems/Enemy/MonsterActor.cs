// 적 액터 — 주입된 전략으로 이동, 처치/도달 분기, 풀링 대상
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 적(몬스터) 액터입니다. 주입된 이동 전략으로 이동하며, 처치/도달을 스포너에 통지합니다.
    /// </summary>
    public class MonsterActor : ActorBase<EnemyData>, IMovableActor, ITargetable, IPoolable
    {
        private IMovementStrategy movement;
        private EnemySpawner spawner;
        private bool resolved;   // 처치/도달 이중 정산 방지

        /// <summary>
        /// 이 적의 데이터입니다. (풀 회수 시 prefab 식별용)
        /// </summary>
        public EnemyData Data => data;

        /// <summary>
        /// 처치 시 지급할 보상 골드입니다.
        /// </summary>
        public int RewardGold => data != null ? data.rewardGold : 0;

        /// <summary>
        /// 코어 도달 시 입히는 피해입니다. (EnemyData 공급, 미할당 시 1)
        /// </summary>
        public float CoreDamage => data != null ? data.coreDamage : 1f;

        /// <summary>
        /// 회수·통지를 위임할 스포너를 주입합니다.
        /// </summary>
        public void SetSpawner(EnemySpawner s) => spawner = s;

        /// <summary>
        /// 모드가 생성한 이동 전략을 주입합니다.
        /// </summary>
        public void SetMovement(IMovementStrategy strategy) => movement = strategy;

        #region IMovableActor Implementation
        public void SetPosition(Vector3 newPosition) => transform.position = newPosition;

        public bool IsMovableState()
        {
            return currentState == ActorState.Moving || currentState == ActorState.Idle;
        }
        #endregion

        #region ITargetable Implementation
        public bool IsActive => currentState != ActorState.Dead;
        #endregion

        #region IPoolable Implementation
        public void OnSpawn()
        {
            resolved = false;
            movement = null;
            if (data != null) currentHealth = data.health;
            SetState(ActorState.Idle);
        }

        public void OnDespawn()
        {
            movement = null;
            SetState(ActorState.Dead);
        }
        #endregion

        public override void Initialize(EnemyData actorData)
        {
            base.Initialize(actorData);
            currentHealth = actorData.health;
            resolved = false;
        }

        private void Update()
        {
            if (movement == null || resolved) return;

            movement.Tick(Time.deltaTime);
            if (movement.HasReachedGoal) Resolve(reached: true);
        }

        public override void TakeDamage(float amount)
        {
            if (currentState == ActorState.Dead || resolved) return;

            currentHealth -= amount;
            if (currentHealth <= 0f) Resolve(reached: false);
        }

        /// <summary>
        /// 적의 최종 처리를 분기합니다. (도달=코어 피해, 처치=보상)
        /// </summary>
        private void Resolve(bool reached)
        {
            if (resolved) return;
            resolved = true;
            SetState(ActorState.Dead);

            if (reached) spawner?.HandleEnemyReached(this);
            else spawner?.HandleEnemyKilled(this);
        }
    }
}
