using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 게임 전체의 웨이브 진행 순서를 관리하는 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWaveSequence", menuName = "DefenseDot/WaveSequence")]
    public class WaveSequence : ScriptableObject
    {
        public List<WaveData> waves = new List<WaveData>();
    }
}
