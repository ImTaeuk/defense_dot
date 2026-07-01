using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DefenseDot.UI.Base;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 카드 1장을 표시하는 위젯입니다. (이름·종류·설명·아이콘·등급 포일) </summary>
    public sealed class CardSlotWidget : UIWidget<CardDisplay>
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image border;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI kindText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private ParticleSystem glowParticle;

        /// <summary> 클릭 중계를 위해 버튼을 노출합니다. </summary>
        public Button Button => button;

        /// <summary> 카드 표시 데이터를 반영합니다. </summary>
        public override void SetData(CardDisplay disp)
        {
            if (nameText != null) nameText.text = disp.title;
            if (kindText != null) kindText.text = disp.kindTag;
            if (descText != null) descText.text = disp.desc;
            if (icon != null) { icon.sprite = disp.icon; icon.enabled = disp.icon != null; }
        }

        /// <summary> 등급별 카드 스프라이트·포일 머티리얼을 적용합니다. </summary>
        public void SetTierStyle(CardTierSet.TierStyle style)
        {
            if (background == null) return;
            if (style.cardSprite != null) background.sprite = style.cardSprite;
            if (style.foilMaterial != null) background.material = style.foilMaterial;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            if (border != null) border.enabled = false;
        }

        /// <summary> 슬롯 사용 여부(빈 슬롯 숨김)를 설정합니다. </summary>
        public void SetActiveSlot(bool active)
        {
            if (button != null) button.gameObject.SetActive(active);
        }
    }
}
