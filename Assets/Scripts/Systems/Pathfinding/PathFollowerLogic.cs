using System.Collections.Generic;
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Systems.Enemy;

namespace DefenseDot.Systems.Pathfinding
{
    /// <summary>
    /// MonoBehaviour가 아닌 순수 C# 클래스(POCO)로 구현된 이동 로직입니다.
    /// Actor와 독립적으로 이동 계산을 수행합니다.
    /// </summary>
    public class PathFollowerLogic
{
        private readonly IMovableActor actor;
        private readonly float moveSpeed;
        
        private List<Vector2Int> currentPath;
        private int currentPathIndex;
        private System.Action onComplete;

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
            // 액터가 이동 가능한 상태가 아니면 로직을 수행하지 않음 (인터페이스를 통한 상태 확인)
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
            actor.SetState(ActorState.Idle);
            onComplete?.Invoke();
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            // 3D 환경 (XZ 평면)에 맞춰 Map Y를 World Z로 매핑
            // Y축을 0.8f로 설정하여 1유닛 높이의 타일(중심 0, 상단 0.5) 위에 적(반지름 0.3)이 위치하게 함
            return new Vector3(cell.x + 0.5f, 0.8f, cell.y + 0.5f);
        }
}
}
