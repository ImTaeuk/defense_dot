using UnityEngine;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.Data
{
    /// <summary>
    /// 타워 1종의 정의입니다. 이 타워가 가진 것(기본 공격·스탯·배치 정보)만 담습니다.
    /// 무엇을 쏘는지는 basicAttack(능력)이 정합니다.
    /// 합성 계보처럼 "무엇을 얻을 수 있는가"는 획득 규칙이므로 아레나 모드가 소유합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerData", menuName = "DefenseDot/TowerData")]
    public class TowerData : ScriptableObject
    {
        [Tooltip("타워의 이름")]
        public string towerName;

        [Header("기본 공격")]
        [Tooltip("이 타워의 기본 공격(tier=Basic 능력). 발사 주기의 주인입니다")]
        public AbilityData basicAttack;
        [Tooltip("기본 공격 모션(재생 속도는 공격속도로 스케일). 없으면 즉시 발사")]
        public AnimationClip castAnimation;
        [Tooltip("공격 속도 (초당 공격 횟수)")]
        public float attackSpeed = 1f;
        [Tooltip("타겟 탐색 사거리")]
        public float attackRange;

        [Header("배치")]
        [Tooltip("설치 비용")]
        public int cost;
        [Tooltip("게임 내에 생성될 타워 프리팹")]
        public GameObject prefab;
    }
}