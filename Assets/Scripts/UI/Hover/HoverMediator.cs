// 호버 요소와 표시 패널을 잇는 단일 중재 창구
using System.Collections.Generic;

namespace DefenseDot.UI.Hover
{
    /// <summary> 호버 요소와 표시 패널을 서로 모르는 채로 잇는 중재자입니다. </summary>
    public static class HoverMediator
    {
        /// <summary> 표시할 내용이 정해졌을 때 발생합니다. </summary>
        public static event System.Action<HoverContent> OnHoverEntered;

        /// <summary> 표시를 거둬야 할 때 발생합니다. </summary>
        public static event System.Action OnHoverExited;

        /// <summary> 지금 호버 중인 요소들. 맨 뒤가 가장 안쪽이며 그것을 표시한다. </summary>
        private static readonly List<IUIHoverable> entered = new List<IUIHoverable>();

        /// <summary> 호버 진입을 알립니다. 맨 뒤에 쌓여 곧바로 표시 대상이 됩니다. </summary>
        /// <param name="hoverable">호버된 요소</param>
        public static void NotifyEntered(IUIHoverable hoverable)
        {
            if (hoverable == null)
                return;

            // 중복 진입을 뒤로 옮긴다 — 두 번 쌓이면 이탈 한 번에 안 빠져 유령이 남는다
            entered.Remove(hoverable);
            entered.Add(hoverable);

            OnHoverEntered?.Invoke(hoverable.BuildHoverContent());
        }

        /// <summary> 호버 이탈을 알립니다. 표시 중이던 요소가 빠지면 바깥쪽으로 되돌립니다. </summary>
        /// <param name="hoverable">이탈한 요소</param>
        public static void NotifyExited(IUIHoverable hoverable)
        {
            if (hoverable == null)
                return;

            int lastIndex = entered.Count - 1;
            int index = entered.IndexOf(hoverable);
            if (index < 0)
                return;

            entered.RemoveAt(index);

            // 표시 중이던 맨 뒤가 아니면 표시 대상이 그대로이므로 알리지 않는다
            if (index != lastIndex)
                return;

            // 바깥쪽 요소가 아직 호버 중이면 그 내용으로 되돌린다
            if (entered.Count > 0)
            {
                OnHoverEntered?.Invoke(entered[entered.Count - 1].BuildHoverContent());
                return;
            }

            OnHoverExited?.Invoke();
        }

        /// <summary> 구독과 진입 목록을 모두 비웁니다. 게임 진입 지점에서 호출합니다. </summary>
        public static void Reset()
        {
            OnHoverEntered = null;
            OnHoverExited = null;
            entered.Clear();
        }
    }
}