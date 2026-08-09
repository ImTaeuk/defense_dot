// Canvas 위 모든 UI의 최상위 베이스 — 얇게 유지
using System.Collections.Generic;
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

        /// <summary> 등록된 Single 인스턴스와 그 소속 씬입니다. </summary>
        private readonly struct SingleEntry
        {
            /// <summary> 등록된 인스턴스입니다. </summary>
            public readonly UIObject Target;

            /// <summary> 이 인스턴스가 속한 씬 이름입니다. </summary>
            public readonly string SceneName;

            /// <summary> 등록 항목을 만듭니다. </summary>
            /// <param name="target">등록할 인스턴스</param>
            /// <param name="sceneName">이 인스턴스가 속한 씬 이름</param>
            public SingleEntry(UIObject target, string sceneName)
            {
                Target = target;
                SceneName = sceneName;
            }
        }

        private static readonly Dictionary<System.Type, SingleEntry> singles =
            new Dictionary<System.Type, SingleEntry>();

        /// <summary> Single 인스턴스를 장부에 올립니다. </summary>
        /// <param name="target">등록할 인스턴스</param>
        /// <param name="sceneName">이 인스턴스가 속한 씬 이름</param>
        public static void RegisterSingle(UIObject target, string sceneName)
        {
            if (target == null)
                return;

            System.Type key = target.GetType();
            if (singles.ContainsKey(key))
                Debug.LogWarning($"{key.Name} 이 이미 등록돼 있어 덮어씁니다.", target);

            singles[key] = new SingleEntry(target, sceneName);
        }

        /// <summary> 이 타입의 UI 를 얻습니다. Single 은 장부에서, Poolable 은 풀에서 옵니다. </summary>
        /// <typeparam name="T">얻을 UI 타입</typeparam>
        /// <returns>인스턴스. 없으면 null</returns>
        public static T Create<T>() where T : UIObject
        {
            if (singles.TryGetValue(typeof(T), out SingleEntry entry) && entry.Target != null)
                return entry.Target as T;

            return null;
        }

        /// <summary> 한 씬 소속 Single 을 장부에서 지우고 파괴합니다. </summary>
        /// <param name="sceneName">떠나는 씬 이름</param>
        public static void ReleaseScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return;

            using (UnityEngine.Pool.ListPool<System.Type>.Get(out List<System.Type> targets))
            {
                foreach (KeyValuePair<System.Type, SingleEntry> pair in singles)
                {
                    if (pair.Value.SceneName != sceneName)
                        continue;

                    targets.Add(pair.Key);
                }

                foreach (System.Type key in targets)
                {
                    UIObject target = singles[key].Target;
                    singles.Remove(key);
                    if (target != null)
                        DestroyImmediate(target.gameObject);
                }
            }
        }

        /// <summary> 장부를 비웁니다. 게임 진입 지점에서 호출합니다. </summary>
        public static void ClearRegistry()
        {
            singles.Clear();
        }
    }
}
