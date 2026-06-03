// 모드 생성에 필요한 공통 입력 묶음
using UnityEngine;
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

        public ModeContext(CoreModel core, EconomyModel economy, TargetFinder targetFinder, Vector3 spawnOrigin, Vector3 coreCenter)
        {
            Core = core;
            Economy = economy;
            TargetFinder = targetFinder;
            SpawnOrigin = spawnOrigin;
            CoreCenter = coreCenter;
        }
    }
}
