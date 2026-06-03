// 적 공전 이동 전략(원형 아레나) — 반경을 비율로 관리하여 동적 크기에 자동 대응
using UnityEngine;
using DefenseDot.Domain.Models;

namespace DefenseDot.Systems.Enemy
{
    /// <summary>
    /// 원형 아레나에서 적이 코어 주위를 공전하는 이동 전략입니다. (POCO)
    /// 반경을 비율(0~1)로 관리하므로 아레나가 줄면 적도 비례해 압축됩니다.
    /// 반경은 원본처럼 전체 범위(0~1)를 천천히 진동합니다.
    /// </summary>
    public class ArenaOrbitLogic : IMovementStrategy
    {
        private readonly IMovableActor actor;
        private readonly Vector3 center;
        private readonly ArenaModel arena;
        private readonly float angularSpeed;
        private readonly float height;
        private readonly float ratioMoveSpeed;

        private float angle;
        private float currentRatio;
        private float targetRatio;
        private float ratioChangeTimer;

        /// <summary> 아레나 공전은 코어 도달 개념이 없으므로 항상 false입니다. </summary>
        public bool HasReachedGoal => false;

        public ArenaOrbitLogic(IMovableActor actor, Vector3 center, ArenaModel arena,
            float startAngle, float startRatio, float angularSpeed, float height, float ratioMoveSpeed = 0.15f)
        {
            this.actor = actor;
            this.center = center;
            this.arena = arena;
            this.angle = startAngle;
            this.currentRatio = startRatio;
            this.targetRatio = startRatio;
            this.angularSpeed = angularSpeed;
            this.height = height;
            this.ratioMoveSpeed = ratioMoveSpeed;
            this.ratioChangeTimer = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (!actor.IsMovableState()) return;

            angle += angularSpeed * deltaTime;

            // 반경 진동(원본 방식)
            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, ratioMoveSpeed * deltaTime);
            ratioChangeTimer -= deltaTime;
            if (ratioChangeTimer <= 0f)
            {
                targetRatio = Random.value;
                ratioChangeTimer = Random.Range(1.5f, 4f);
            }

            float radius = Mathf.Lerp(arena.SpawnMinRadius, arena.SpawnMaxRadius, currentRatio);
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            actor.SetPosition(pos);
        }
    }
}
