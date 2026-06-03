// 경로추종 이동 전략(타워디펜스 모드) — 셀 경로를 따라 적 이동
using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Pathfinding
{
    /// <summary>
    /// MonoBehaviour가 아닌 순수 C# 클래스(POCO)로 구현된 경로추종 이동 전략입니다.
    /// Actor와 독립적으로 이동을 계산하며, IMovementStrategy로 모드에 주입됩니다.
    /// </summary>
    public class PathFollowerLogic : IMovementStrategy
    {
        private readonly IMovableActor actor;
        private readonly float moveSpeed;

        private List<Vector2Int> currentPath;
        private int currentPathIndex;
        private System.Action onComplete;
        private bool reachedGoal;

        /// <summary>
        /// 경로 끝(코어)에 도달했는지 여부입니다.
        /// </summary>
        public bool HasReachedGoal => reachedGoal;

        /// <summary>
        /// 생성자에서 이동을 수행할 액터를 캐싱하도록 강제합니다.
        /// </summary>
        /// <param name="actor">이동을 수행할 액터 인터페이스</param>
        /// <param name="moveSpeed">이동 속도</param>
        public PathFollowerLogic(IMovableActor actor, float moveSpeed)
        {
            this.actor = actor;
            this.moveSpeed = moveSpeed;
        }

        /// <summary>
        /// 새로운 경로를 설정하고 이동을 시작합니다.
        /// </summary>
        public void SetPath(List<Vector2Int> path, System.Action onComplete = null)
        {
            currentPath = path;
            currentPathIndex = 0;
            this.onComplete = onComplete;
            reachedGoal = false;

            if (currentPath != null && currentPath.Count > 0)
            {
                actor.SetState(ActorState.Moving);
            }
        }

        /// <summary>
        /// 매 프레임 액터의 업데이트 루프에서 호출되어 실제 위치를 계산합니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!actor.IsMovableState()) return;
            if (currentPath == null || currentPathIndex >= currentPath.Count) return;

            Vector3 targetWorldPos = CellToWorld(currentPath[currentPathIndex]);
            Vector3 currentPos = actor.Position;

            Vector3 nextPos = Vector3.MoveTowards(currentPos, targetWorldPos, moveSpeed * deltaTime);
            actor.SetPosition(nextPos);

            if (Vector3.Distance(nextPos, targetWorldPos) < 0.05f)
            {
                currentPathIndex++;
                if (currentPathIndex >= currentPath.Count)
                {
                    CompleteMovement();
                }
            }
        }

        private void CompleteMovement()
        {
            currentPath = null;
            reachedGoal = true;
            actor.SetState(ActorState.Idle);
            onComplete?.Invoke();
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            // 3D 환경(XZ 평면)에 맞춰 Map Y를 World Z로 매핑
            // Y축 0.8f로 타일 위에 적(반지름 0.3) 배치
            return new Vector3(cell.x + 0.5f, 0.8f, cell.y + 0.5f);
        }
    }
}
