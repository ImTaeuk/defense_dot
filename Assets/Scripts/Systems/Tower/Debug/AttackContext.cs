// DEBUG: 공격 타입 테스트용 — 실제 능력 시스템 구현 시 삭제
using UnityEngine;
using DefenseDot.Data;

namespace DefenseDot.Systems.Tower.Debugging
{
    /// <summary>
    /// 공격 1회 수행에 필요한 입력 묶음입니다. (DEBUG)
    /// </summary>
    public readonly struct AttackContext
    {
        /// <summary>투사체 생성 등에 쓰는 호스트 MonoBehaviour입니다.</summary>
        public readonly MonoBehaviour Host;
        /// <summary>공격 시작점(타워 위치)입니다.</summary>
        public readonly Vector3 Origin;
        /// <summary>적 질의 수단입니다.</summary>
        public readonly TargetFinder Finder;
        /// <summary>타워 능력치 데이터입니다.</summary>
        public readonly TowerData Data;

        public AttackContext(MonoBehaviour host, Vector3 origin, TargetFinder finder, TowerData data)
        {
            Host = host;
            Origin = origin;
            Finder = finder;
            Data = data;
        }
    }
}
