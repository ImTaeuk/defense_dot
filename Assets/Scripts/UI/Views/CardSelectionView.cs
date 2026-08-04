using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.UI.Widgets;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Views
{
    /// <summary> 레벨업 카드 모달. CardSlotWidget 3개를 소유·조립하고 선택 인덱스를 중계합니다. </summary>
    public sealed class CardSelectionView : UIView
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private CardSlotWidget[] slots;
        [SerializeField] private ArenaCardConfig config;
        [SerializeField] private RectTransform cardsContainer;

        [Header("등장 연출")]
        [SerializeField] private float fadeDuration = 0.22f;
        [SerializeField] private float popFromScale = 0.9f;

        /// <summary> 카드가 선택되면 인덱스를 통지합니다. </summary>
        public event System.Action<int> OnCardSelected;

        private float animTime;
        private bool animating;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < slots.Length; i++)
            {
                int idx = i;
                if (slots[i] != null && slots[i].Button != null)
                    slots[i].Button.onClick.AddListener(() => OnCardSelected?.Invoke(idx));
            }
            if (root != null) root.SetActive(false);
        }

        /// <summary> 카드 목록을 위젯에 반영하고 모달을 표시합니다. </summary>
        public void ShowChoices(IReadOnlyList<Card> choices)
        {
            Show();   // gameObject 활성화(UIView)
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = "[ LEVEL  UP ]";
            for (int i = 0; i < slots.Length; i++)
            {
                bool used = i < choices.Count;
                if (slots[i] != null) slots[i].SetActiveSlot(used);
                if (!used) continue;
                CardDisplay disp = CardDisplayBuilder.Build(choices[i]);
                slots[i].SetData(disp);
                if (config != null && config.tierSet != null)
                    slots[i].SetTierStyle(config.tierSet.Get(disp.tier));
            }
            StartEntrance();
        }

        private void StartEntrance()
        {
            animTime = 0f;
            animating = true;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (cardsContainer != null) cardsContainer.localScale = Vector3.one * popFromScale;
        }

        private void Update()
        {
            if (!animating) return;
            animTime += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(animTime / fadeDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t);
            if (canvasGroup != null) canvasGroup.alpha = eased;
            if (cardsContainer != null) cardsContainer.localScale = Vector3.one * Mathf.Lerp(popFromScale, 1f, eased);
            if (t >= 1f) animating = false;
        }

        /// <summary> 모달을 숨깁니다. </summary>
        protected override void OnHide()
        {
            animating = false;
            if (cardsContainer != null) cardsContainer.localScale = Vector3.one;
            if (root != null) root.SetActive(false);
        }
    }
}
