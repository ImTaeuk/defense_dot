using System.Collections.Generic;
using UnityEngine;
using DefenseDot.UI.Base;
using DefenseDot.Systems.Abilities;

namespace DefenseDot.UI.Views
{
    /// <summary> 보유 능력 목록·강화/삭제 버튼을 표시하는 최소 패널입니다. Presenter를 모릅니다. </summary>
    public sealed class AbilityUpgradeView : UIView
    {
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private AbilityUpgradeRow rowPrefab;

        private readonly List<AbilityUpgradeRow> rows = new List<AbilityUpgradeRow>();

        /// <summary> 강화 요청을 중계합니다. </summary>
        public event System.Action<AbilityInstance> OnUpgrade;
        /// <summary> 삭제 요청을 중계합니다. </summary>
        public event System.Action<AbilityInstance> OnDismiss;

        /// <summary> 능력 행들을 (재)생성하고 강화 상태를 반영합니다. </summary>
        public void Render(IReadOnlyList<AbilityInstance> abilities, System.Func<AbilityInstance, (bool isMax, int cost, bool canAfford)> query)
        {
            EnsureRowCount(abilities.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < abilities.Count;
                rows[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                (bool isMax, int cost, bool canAfford) s = query(abilities[i]);
                rows[i].SetData(new AbilityUpgradeRowData(abilities[i], s.isMax, s.cost, s.canAfford));
            }
        }

        /// <summary> 필요한 만큼 행을 확보합니다(부족분만 생성). </summary>
        private void EnsureRowCount(int count)
        {
            while (rows.Count < count)
            {
                AbilityUpgradeRow row = Instantiate(rowPrefab, rowContainer);
                row.OnUpgrade += ability => OnUpgrade?.Invoke(ability);
                row.OnDismiss += ability => OnDismiss?.Invoke(ability);
                rows.Add(row);
            }
        }
    }
}
