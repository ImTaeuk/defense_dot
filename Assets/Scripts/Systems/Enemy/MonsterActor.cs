using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Data;
using DefenseDot.Systems.Pathfinding;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 적(몬스터) 액터 클래스입니다. 이동과 타겟팅 기능을 포함합니다.
    /// </summary>
    public class MonsterActor : ActorBase<EnemyData>, IMovableActor, ITargetable, IPoolable
    {
        private PathFollowerLogic movementLogic;
        private EnemySpawner spawner;

        public void SetSpawner(EnemySpawner s) => this.spawner = s;

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
            if (data != null) currentHealth = data.health;
            SetState(ActorState.Idle);
        }

        public void OnDespawn()
        {
            SetState(ActorState.Dead);
        }
        #endregion

        private void Awake()
        {
            // Initial logic moved to Initialize
        }

        public override void Initialize(EnemyData actorData)
        {
            base.Initialize(actorData);
            movementLogic = new PathFollowerLogic(this, actorData.moveSpeed);
            currentHealth = actorData.health;
        }

        private void Update()
        {
            movementLogic?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 경로를 설정하여 이동을 시작합니다.
        /// </summary>
        public void MoveToPath(System.Collections.Generic.List<Vector2Int> path)
        {
            movementLogic.SetPath(path, () => {
                // 도착 시 행동 (Core 데미지 처리 후 파괴)
                DestroyActor();
            });
        }

        public void TakeDamage(float amount)
        {
            if (currentState == ActorState.Dead) return;

            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                DestroyActor();
            }
        }

        private void DestroyActor()
        {
            SetState(ActorState.Dead);
            spawner?.HandleEnemyRemoved(this);
            Destroy(gameObject); // 나중에 Pooling으로 교체 권장
        }
}
}
