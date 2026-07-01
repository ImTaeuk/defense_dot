// UIView 타입에 대응하는 UIPresenter<TView> 를 리플렉션으로 찾아 생성하는 POCO 팩토리
using System.Collections.Generic;
using DefenseDot.Domain;
using DefenseDot.UI.Base;
using DefenseDot.UI.Presenters;

namespace DefenseDot.UI
{
    /// <summary>
    /// UIView 타입 → UIPresenter&lt;TView&gt; 매핑을 리플렉션으로 구축해 Presenter 를 생성합니다.
    /// View 증가에도 코드가 늘지 않습니다.
    /// </summary>
    public sealed class UIPresenterFactory
    {
        private readonly GameContext context;
        private readonly Dictionary<System.Type, System.Type> viewToPresenter;

        /// <summary> 컨텍스트를 받고 매핑을 1회 구축합니다. </summary>
        public UIPresenterFactory(GameContext context)
        {
            this.context = context;
            viewToPresenter = BuildMap();
        }

        /// <summary> View 타입에 맞는 Presenter 를 생성합니다. 실패 시 로그 후 null. </summary>
        public IPresenter Create(UIView view)
        {
            if (view == null) return null;
            if (!viewToPresenter.TryGetValue(view.GetType(), out System.Type presenterType))
            {
                UnityEngine.Debug.LogError($"[UIPresenterFactory] {view.GetType().Name} 에 대응하는 UIPresenter<> 구현이 없습니다.", view);
                return null;
            }
            try
            {
                return (IPresenter)System.Activator.CreateInstance(presenterType, view, context);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[UIPresenterFactory] {presenterType.Name} 생성 실패 — (TView, GameContext) 생성자 확인 필요: {e.Message}", view);
                return null;
            }
        }

        private static Dictionary<System.Type, System.Type> BuildMap()
        {
            var map = new Dictionary<System.Type, System.Type>();
            System.Type openBase = typeof(UIPresenter<>);
            // 메인·테스트 어셈블리(DefenseDot*)만 스캔
            foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("DefenseDot")) continue;
                System.Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    // 일부 타입 로드 실패 시 로드된 것만 스캔(전체 배선 붕괴 방지)
                    UnityEngine.Debug.LogError($"[UIPresenterFactory] {asm.GetName().Name} 타입 일부 로드 실패 — 성공분만 스캔: {e.Message}");
                    types = System.Array.FindAll(e.Types, t => t != null);
                }
                foreach (System.Type type in types)
                {
                    if (type.IsAbstract) continue;
                    System.Type baseType = type.BaseType;
                    while (baseType != null)
                    {
                        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == openBase)
                        {
                            map[baseType.GetGenericArguments()[0]] = type;
                            break;
                        }
                        baseType = baseType.BaseType;
                    }
                }
            }
            return map;
        }
    }
}
