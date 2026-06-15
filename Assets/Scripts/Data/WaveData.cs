using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 단일 웨이브의 구성을 정의하는 데이터 클래스입니다.
    /// </summary>
    [Serializable]
    public class WaveEntry
    {
        public EnemyData enemyData;
        public int count = 10;
        public float spawnInterval = 0.5f;
    }

    /// <summary>
    /// 여러 종류의 적이 섞일 수 있는 단일 웨이브 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewWaveData", menuName = "DefenseDot/WaveData")]
    public class WaveData : ScriptableObject
    {
        public List<WaveEntry> entries = new List<WaveEntry>();
        public float nextWaveDelay = 5f;
        public float duration = 30f;              // 라운드 제한시간(초), Arena
        public float killScoreMultiplier = 1f;    // 처치 점수 배율, Arena
        public float timeBonusMultiplier = 1f;    // 시간보너스 배율, Arena
    }
}
