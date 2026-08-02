using System.Collections.Generic;
using UnityEngine;
using DefenseDot.UI.Base;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 보유 능력 목록을 표시하는 최소 패널입니다. Presenter를 모릅니다. </summary>
    public sealed class AbilityUpgradeView : UIView
    {
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private AbilityUpgradeRow rowPrefab;

        private readonly List<AbilityUpgradeRow> rows = new List<AbilityUpgradeRow>();

        /// <summary> 능력 행들을 (재)생성하고 내용을 반영합니다. </summary>
        /// <param name="abilities">표시할 능력 목록</param>
        public void Render(IReadOnlyList<AbilityInstance> abilities)
        {
            EnsureRowCount(abilities.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < abilities.Count;
                rows[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                rows[i].SetData(new AbilityUpgradeRowData(abilities[i]));
            }
        }

        /// <summary> 필요한 만큼 행을 확보합니다(부족분만 생성). </summary>
        /// <param name="count">필요한 행 수</param>
        private void EnsureRowCount(int count)
        {
            while (rows.Count < count)
            {
                rows.Add(Instantiate(rowPrefab, rowContainer));
            }
        }
    }
}
