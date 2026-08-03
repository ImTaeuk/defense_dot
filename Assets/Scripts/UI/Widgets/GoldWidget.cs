// 골드 수치 표시 위젯 — 현재 어느 화면에도 붙어 있지 않다
//
// 아레나 HUD 에서 2026-08-03 내렸다. 골드를 쓰는 곳이 아레나에 하나도 없어(강화·삭제를
// 걷어낸 뒤로) 화면에 뜻 없는 숫자만 남았고, 점수는 ScoreModel 이 라운드 가중치·시간
// 보너스까지 계산해 따로 표시하므로 역할이 겹쳤다.
//
// 골드 획득 로직(EconomySystem)과 EconomyModel 은 그대로 살아 있다 — 그리드 모드가
// 타워 건설비로 쓰고, 메타층(가챠·강화소 등)을 도입하면 소비처가 생긴다.
// 그때 이 위젯을 HUD 에 다시 붙이면 된다.
using UnityEngine;
using TMPro;
using DefenseDot.UI.Base;

namespace DefenseDot.UI.Widgets
{
    /// <summary> 골드 수치를 표시하는 위젯입니다. 현재 사용처 없음 — 파일 상단 주석 참고. </summary>
    public sealed class GoldWidget : UIWidget<int>
    {
        [SerializeField] private TextMeshProUGUI valueText;

        /// <summary> 골드 표시를 갱신합니다. </summary>
        public override void SetData(int gold)
        {
            if (valueText != null) valueText.text = gold.ToString();
        }
    }
}
