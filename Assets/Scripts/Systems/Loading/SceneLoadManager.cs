// 씬을 로드하고 준비 작업이 전부 끝날 때까지 기다리며 상태를 알린다
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using DefenseDot.Core;

namespace DefenseDot.Systems.Loading
{
    /// <summary>
    /// 씬을 로드한 뒤 등록된 준비 작업이 전부 완료될 때까지 기다리고, 상태 변화를 관찰자에게 알립니다.
    /// 무엇을 준비할지는 알지 못하며, 진행률 표시는 맡지 않습니다.
    /// </summary>
    public sealed class SceneLoadManager : Singleton<SceneLoadManager>
    {
        /// <summary> 로딩 상태 변화를 받는 대상입니다. </summary>
        public interface ILoadingObserver
        {
            /// <summary> 로딩 상태가 바뀌면 호출됩니다. 필요한 값은 관찰자가 직접 조회합니다. </summary>
            void OnLoadingStateChanged();
        }

        /// <summary> AsyncOperation.progress가 활성화를 기다리며 멈추는 지점. </summary>
        private const float ACTIVATION_PROGRESS = 0.9f;

        /// <summary> 로딩 진행 단계입니다. </summary>
        public enum LoadingState
        {
            Idle,
            LoadingScene,
            Warmup,
            Complete,
        }

        /// <summary> 이번 세션에 등록된 준비 작업들. WarmupAllAsync 가 소비하고 비운다. </summary>
        private readonly List<ISceneWarmup> warmups = new List<ISceneWarmup>();

        /// <summary> 상태 변화를 통보받을 대상들. 세션 경계에서 건드리지 않는다. </summary>
        private readonly List<ILoadingObserver> observers = new List<ILoadingObserver>();

        private LoadingState state = LoadingState.Idle;
        private int completedCount;
        private int totalCount;
        private float sceneProgress;

        /// <summary> 현재 로딩 상태입니다. </summary>
        public LoadingState State => state;

        /// <summary> 완료된 준비 작업 수입니다. </summary>
        public int CompletedCount => completedCount;

        /// <summary> 이번 세션이 기다리는 준비 작업 총 수입니다. </summary>
        public int TotalCount => totalCount;

        /// <summary> 씬 로드 진행률(0~1)입니다. AsyncOperation 실측값 그대로입니다. </summary>
        public float SceneProgress => sceneProgress;

        /// <summary> 준비 작업 진행률(0~1)입니다. 기다릴 것이 없으면 1 입니다. </summary>
        public float WarmupProgress
        {
            get
            {
                if (totalCount <= 0)
                    return 1f;

                return (float)completedCount / totalCount;
            }
        }

        /// <summary> 준비 작업을 다음 세션 대상으로 등록합니다. 진행 중에는 받지 않습니다. </summary>
        /// <param name="warmup">등록할 준비 작업</param>
        public void RegisterWarmup(ISceneWarmup warmup)
        {
            if (warmup == null)
                return;

            // 씬 로드 중 등록은 정상(로드된 씬이 자기를 올린다). 예열이 시작되면 마감이다
            if (state == LoadingState.Warmup)
            {
                Debug.LogWarning($"진행 중인 예열에는 등록할 수 없습니다: {warmup.GetType().Name}", this);
                return;
            }

            if (warmups.Contains(warmup))
                return;

            warmups.Add(warmup);
        }

        /// <summary> 상태 변화를 받을 대상을 등록하고 현재 상태를 즉시 알립니다. </summary>
        /// <param name="observer">등록할 관찰자</param>
        /// <param name="shouldNotifyImmediately">등록 즉시 현재 상태를 통보할지 여부</param>
        public void RegisterObserver(ILoadingObserver observer, bool shouldNotifyImmediately = true)
        {
            if (observer == null)
                return;

            if (observers.Contains(observer))
                return;

            observers.Add(observer);

            // 구독 전 변화는 되살릴 수 없다
            if (shouldNotifyImmediately)
                observer.OnLoadingStateChanged();
        }

        /// <summary> 상태 변화 통보 대상에서 제외합니다. </summary>
        /// <param name="observer">해제할 관찰자</param>
        public void UnregisterObserver(ILoadingObserver observer)
        {
            if (observer == null)
                return;

            observers.Remove(observer);
        }

        /// <summary> 씬을 전환하고 로드 진행률을 알립니다. 이 호출이 한 세션의 시작입니다. </summary>
        /// <param name="scene">전환할 씬</param>
        /// <param name="cancellationToken">중단할 때 쓰는 토큰</param>
        public async UniTask LoadSceneAsync(SceneId scene, System.Threading.CancellationToken cancellationToken)
        {
            // 1. 세션 리셋 — 이전 세션의 잔여 등록·진행률을 버린다
            warmups.Clear();
            completedCount = 0;
            totalCount = 0;
            sceneProgress = 0f;
            SetState(LoadingState.LoadingScene);

            // 2. 로드 진행 추적 — 완료 프레임에 새 씬이 스스로 준비 작업을 등록한다
            AsyncOperation operation = SceneManager.LoadSceneAsync(ToSceneName(scene), LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                // progress는 활성화 대기 지점인 0.9에서 멈추므로 0~1로 편다
                sceneProgress = Mathf.Clamp01(operation.progress / ACTIVATION_PROGRESS);
                NotifyObservers();

                bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken)
                    .SuppressCancellationThrow();
                if (canceled)
                    return;
            }

            sceneProgress = 1f;
            NotifyObservers();
        }

        /// <summary> 씬 값을 빌드 설정에 등록된 씬 이름으로 옮깁니다. </summary>
        /// <param name="scene">옮길 씬 값</param>
        private static string ToSceneName(SceneId scene)
        {
            switch (scene)
            {
                case SceneId.Arena:
                    return "ArenaScene";

                case SceneId.Grid:
                    return "GridScene";

                default:
                    throw new System.ArgumentOutOfRangeException(nameof(scene), scene, "처리되지 않은 값입니다.");
            }
        }

        /// <summary> 등록된 준비 작업을 전부 수행합니다. 이 호출이 등록 마감이며 목록을 비웁니다. </summary>
        /// <param name="cancellationToken">씬 파괴 등으로 중단할 때 쓰는 토큰</param>
        public async UniTask WarmupAllAsync(System.Threading.CancellationToken cancellationToken)
        {
            // 1. 등록 마감·총량 확정
            totalCount = warmups.Count;
            completedCount = 0;
            SetState(LoadingState.Warmup);

            // 2. 등록분 수행
            for (int i = 0; i < warmups.Count; i++)
            {
                bool canceled = await warmups[i].WarmupAsync(cancellationToken).SuppressCancellationThrow();
                if (canceled)
                {
                    warmups.Clear();
                    return;
                }

                completedCount++;
                NotifyObservers();
            }

            // 3. 다음 세션을 위해 비움
            warmups.Clear();
            SetState(LoadingState.Complete);
        }

        /// <summary> 상태를 바꾸고 값이 실제로 달라졌을 때만 통보합니다. </summary>
        /// <param name="next">전이할 상태</param>
        private void SetState(LoadingState next)
        {
            if (state == next)
                return;

            state = next;
            NotifyObservers();
        }

        /// <summary> 등록된 관찰자 전원에게 상태 변화를 알립니다. </summary>
        private void NotifyObservers()
        {
            // 순회 중 해제돼도 안전하게
            for (int i = observers.Count - 1; i >= 0; i--)
            {
                observers[i].OnLoadingStateChanged();
            }
        }
    }
}