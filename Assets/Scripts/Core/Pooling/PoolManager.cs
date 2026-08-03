// 풀을 게임 전체 수명으로 보유하고, 씬이 끝나면 그 씬 몫만 정리한다
using UnityEngine;
using UnityEngine.SceneManagement;
using DefenseDot.Core;
using DefenseDot.Systems.Assets;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀을 게임 전체 수명으로 보유하는 전역 관리자입니다. Bootstrap 계층에 배치합니다.
    /// 씬이 언로드되면 그 씬 몫의 풀만 정리하고, 전역 풀은 앱 종료 시 해제합니다.
    /// </summary>
    public sealed class PoolManager : Singleton<PoolManager>
    {
        private PoolSystem system;

        /// <summary> 풀 조율자입니다. 배치 이후 non-null. </summary>
        public PoolSystem System => system;

        /// <summary> 조율자를 만들고 씬 언로드 통보를 구독합니다. </summary>
        protected override void OnAwake()
        {
            system = new PoolSystem(new AssetLoader(), transform);
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        /// <summary> 구독을 끊고 남은 풀·에셋을 전량 해제합니다(앱 종료 경로). </summary>
        protected override void OnDestroyed()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            system?.Dispose();
            system = null;
        }

        /// <summary> 떠난 씬 몫의 풀을 정리합니다. </summary>
        /// <param name="scene">언로드된 씬</param>
        private void HandleSceneUnloaded(Scene scene)
        {
            // 인스턴스가 이 계층 아래 있어 씬과 함께 죽지 않으므로, 언로드 후 정리해도 늦지 않다
            system?.ReleaseScene(scene.name);
        }
    }
}
