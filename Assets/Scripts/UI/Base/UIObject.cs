// Canvas 위 모든 UI의 최상위 베이스 — 얇게 유지
using UnityEngine;

namespace DefenseDot.UI.Base
{
    /// <summary>
    /// Canvas에 그려지는 모든 UI 요소의 베이스입니다.
    /// 깊이와 RectTransform 캐싱만 책임지며, 동작은 인터페이스로 분리합니다.
    /// </summary>
    public abstract class UIObject : MonoBehaviour
    {
        [SerializeField] private UIDepth depth = UIDepth.HUD;

        /// <summary> 이 UI 를 장부에서 꺼낼지 풀에서 빌릴지 정합니다. </summary>
        [SerializeField] private UIObjectType objectType = UIObjectType.Single;

        private RectTransform cachedRect;

        /// <summary> 이 UI의 렌더 깊이 계층입니다. </summary>
        public UIDepth Depth => depth;

        /// <summary> 이 UI 를 어디서 얻는지의 구분입니다. </summary>
        public UIObjectType ObjectType => objectType;

        /// <summary> 캐시된 RectTransform입니다. </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (cachedRect == null) cachedRect = transform as RectTransform;
                return cachedRect;
            }
        }
    }
}
