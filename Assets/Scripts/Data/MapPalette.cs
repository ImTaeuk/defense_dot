using System;
using System.Collections.Generic;
using UnityEngine;

namespace DefenseDot.Data
{
    /// <summary>
    /// 셀 유형별로 사용할 수 있는 여러 3D 프리팹 리스트를 정의하는 데이터 에셋입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMapPalette", menuName = "DefenseDot/MapPalette")]
    public class MapPalette : ScriptableObject
    {
        [Serializable]
        public class CellPrefabList
        {
            public CellType type;
            public List<GameObject> prefabs = new List<GameObject>();
        }

        /// <summary> 셀 유형별 프리팹 리스트 </summary>
        public List<CellPrefabList> paletteItems = new List<CellPrefabList>();

        /// <summary>
        /// 특정 셀 유형의 프리팹 리스트를 반환합니다.
        /// </summary>
        public List<GameObject> GetPrefabs(CellType type)
        {
            var item = paletteItems.Find(i => i.type == type);
            return item?.prefabs;
        }

        /// <summary>
        /// 특정 셀 유형의 특정 인덱스 프리팹을 반환합니다.
        /// </summary>
        public GameObject GetPrefab(CellType type, int index)
        {
            var prefabs = GetPrefabs(type);
            if (prefabs == null || index < 0 || index >= prefabs.Count) return null;
            return prefabs[index];
        }
    }
}
