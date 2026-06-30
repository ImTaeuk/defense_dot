using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Views
{
    /// <summary> 레벨업 카드 모달 프리팹 바인딩. 3개 카드 아이템에 데이터/색/연출 반영. </summary>
    public sealed class CardSelectionView : MonoBehaviour, ICardSelectionView
    {
        [System.Serializable]
        public struct CardItem
        {
            public Button button;
            public Image background;     // CardBackground 머티리얼 인스턴스
            public Image border;         // 상단 액센트 바
            public Image icon;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI kindText;
            public TextMeshProUGUI descText;
            public ParticleSystem glowParticle;
        }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private CardItem[] items;
        [SerializeField] private ArenaCardConfig config;
        [SerializeField] private RectTransform cardsContainer;   // 등장 애니 대상

        [Header("등장 연출")]
        [SerializeField] private float fadeDuration = 0.22f;
        [SerializeField] private float popFromScale = 0.9f;

        public event System.Action<int> OnCardSelected;

        private float animTime;
        private bool animating;

        private void Awake()
        {
            for (int i = 0; i < items.Length; i++)
            {
                int idx = i;
                if (items[i].button != null)
                    items[i].button.onClick.AddListener(() => OnCardSelected?.Invoke(idx));
            }
            Hide();
        }

        public void Show(IReadOnlyList<CardChoice> choices)
        {
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = "[ LEVEL  UP ]";
            for (int i = 0; i < items.Length; i++)
            {
                bool used = i < choices.Count;
                if (items[i].button != null) items[i].button.gameObject.SetActive(used);
                if (!used) continue;
                Bind(items[i], choices[i]);
            }
            StartEntrance();
        }

        private void Bind(in CardItem item, in CardChoice choice)
        {
            CardDisplay disp = CardPresentation.Build(choice);

            if (item.nameText != null)
                item.nameText.text = disp.title;

            if (item.kindText != null)
                item.kindText.text = disp.kindTag;

            if (item.descText != null)
                item.descText.text = disp.desc;

            if (item.icon != null)
            {
                item.icon.sprite = disp.icon;
                item.icon.enabled = disp.icon != null;
            }

            // 등급별 카드 스프라이트 + 홀로그램 포일 적용
            if (config != null && config.tierSet != null && item.background != null)
            {
                CardTierSet.TierStyle s = config.tierSet.Get(disp.tier);
                if (s.cardSprite != null) item.background.sprite = s.cardSprite;
                if (s.foilMaterial != null) item.background.material = s.foilMaterial;
                item.background.color = Color.white;
                item.background.type = Image.Type.Simple;
                item.background.preserveAspect = false;
                if (item.border != null) item.border.enabled = false; // 카드 스프라이트가 프레임 제공
            }
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
            if (!animating)
                return;

            animTime += Time.unscaledDeltaTime;           // 정지(timeScale=0) 중에도 동작

            float t = fadeDuration > 0f ? Mathf.Clamp01(animTime / fadeDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t);       // ease-out quad

            if (canvasGroup != null)
                canvasGroup.alpha = eased;

            if (cardsContainer != null)
                cardsContainer.localScale = Vector3.one * Mathf.Lerp(popFromScale, 1f, eased);

            if (t >= 1f)
                animating = false;
        }

        public void Hide()
        {
            animating = false;

            if (cardsContainer != null)
                cardsContainer.localScale = Vector3.one;
            if (root != null)
                root.SetActive(false);
        }
    }
}
