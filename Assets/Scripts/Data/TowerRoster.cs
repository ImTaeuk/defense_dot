using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary> 빌드 모달에 노출할 구매 가능 타워 목록입니다. </summary>
    [CreateAssetMenu(fileName = "TowerRoster", menuName = "DefenseDot/TowerRoster")]
    public class TowerRoster : ScriptableObject
    {
        [Tooltip("구매 가능한 타워 목록")]
        public TowerData[] towers;
    }
}
