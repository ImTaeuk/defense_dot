using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.Data;

namespace DefenseDot.UI.Views
{
    /// <summary> 빈 슬롯 선택 시 구매 가능 타워를 나열하는 빌드 모달 View 입니다. (dumb) </summary>
    public class TowerBuildModalView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;

        /// <summary> 타워 버튼이 선택됨. </summary>
        public event System.Action<TowerData> OnTowerChosen;
        private readonly List<Button> spawned = new List<Button>();

        /// <summary> 로스터로 버튼을 구성하고 패널을 표시합니다. cost > gold 면 버튼 비활성. </summary>
        public void Show(TowerRoster roster, int gold)
        {
            Clear();
            // 비활성 하위에선 레이아웃 리빌드가 무효라 먼저 활성화
            if (panel != null) panel.SetActive(true);
            if (roster != null && roster.towers != null)
            {
                foreach (TowerData tower in roster.towers)
                {
                    if (tower == null) continue;
                    Button button = Instantiate(buttonPrefab, buttonContainer);
                    TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                    if (label != null) label.text = $"{tower.towerName}\n{tower.cost}G";
                    button.interactable = gold >= tower.cost;
                    TowerData captured = tower;
                    button.onClick.AddListener(() => OnTowerChosen?.Invoke(captured));
                    spawned.Add(button);
                    // 텍스트 반영 후 버튼 자신의 레이아웃 갱신 (TMP preferred 크기 확정)
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)button.transform);
                }
            }
            // 버튼 스택(컨테이너) 갱신
            if (buttonContainer is RectTransform containerRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        /// <summary> 모달을 숨깁니다. </summary>
        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Clear()
        {
            // Destroy 는 지연 파괴라, 레이아웃에서 즉시 빠지도록 비활성화 후 파괴
            foreach (Button button in spawned)
            {
                if (button == null) continue;
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
            spawned.Clear();
        }
    }
}
