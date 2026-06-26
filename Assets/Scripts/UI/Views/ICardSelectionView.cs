using System.Collections.Generic;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI.Views
{
    /// <summary> 카드 선택 모달 뷰 계약(프레젠터 테스트용 추상화). </summary>
    public interface ICardSelectionView
    {
        void Show(IReadOnlyList<CardChoice> choices);
        void Hide();
        event System.Action<int> OnCardSelected;
    }
}
