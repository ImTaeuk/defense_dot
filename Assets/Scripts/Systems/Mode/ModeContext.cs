// 모드 생성에 필요한 공통 입력 묶음
using UnityEngine;
using DefenseDot.Core;
using DefenseDot.Core.Pooling;
using DefenseDot.Domain.Models;
using DefenseDot.Systems.Tower;

namespace DefenseDot.Systems.Mode
{
    /// <summary>
    /// 모드(IGameMode) 생성과 모드별 배선에 필요한 공통 입력을 담는 구조체입니다.
    /// </summary>
    public readonly struct ModeContext
    {
        /// <summary> 코어 체력 모델 (TD 모드의 코어 피해용) </summary>
        public readonly CoreModel Core;
        /// <summary> 골드 재화 모델 (타워 배치 비용 차감용) </summary>
        public readonly EconomyModel Economy;
        /// <summary> 타겟 탐색기 (타워의 적 탐색용) </summary>
        public readonly TargetFinder TargetFinder;
        /// <summary> 스폰 기준 원점 (스포너 위치) </summary>
        public readonly Vector3 SpawnOrigin;
        /// <summary> 아레나 중심 (코어 위치) </summary>
        public readonly Vector3 CoreCenter;
        /// <summary> 게임 진행 단계 모델 (능력 구동 게이트용) </summary>
        public readonly GameFlowModel Flow;
        /// <summary> 실시간 전투 상태 (조건부 데미지: 라운드·생존 적 수) </summary>
        public readonly ICombatState CombatState;
        /// <summary> 공용 오브젝트 풀 (이펙트 스폰용) </summary>
        public readonly PoolManager Pooling;

        public ModeContext(CoreModel core, EconomyModel economy, TargetFinder targetFinder, Vector3 spawnOrigin, Vector3 coreCenter, GameFlowModel flow, ICombatState combatState, PoolManager pooling)
        {
            Core = core;
            Economy = economy;
            TargetFinder = targetFinder;
            SpawnOrigin = spawnOrigin;
            CoreCenter = coreCenter;
            Flow = flow;
            CombatState = combatState;
            Pooling = pooling;
        }
    }
}
