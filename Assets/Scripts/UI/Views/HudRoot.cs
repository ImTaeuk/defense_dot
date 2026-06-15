// HUD 루트 공통 베이스 — 모델을 받아 자신의 프레젠터를 조립(자기설치 위젯)
using UnityEngine;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI.Views
{
    /// <summary>
    /// 모드별 HUD 루트의 공통 베이스입니다. 합성 루트(UIRoot)가 모드를 알지 못해도
    /// 각 HUD가 HudContext를 받아 자신의 프레젠터를 조립합니다.
    /// </summary>
    public abstract class HudRoot : MonoBehaviour, IView
    {
        /// <summary> 주어진 컨텍스트로 이 HUD의 프레젠터를 생성합니다. </summary>
        public abstract IPresenter Bind(in HudContext ctx);

        /// <summary> HUD를 화면에 표시합니다. </summary>
        public void Show() => gameObject.SetActive(true);

        /// <summary> HUD를 화면에서 숨깁니다. </summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
