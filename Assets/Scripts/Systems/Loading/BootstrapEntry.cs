// 게임 진입 지점 — 전역 상태를 초기화하고 첫 씬을 연다
using Cysharp.Threading.Tasks;
using UnityEngine;
using DefenseDot.UI.Hover;

namespace DefenseDot.Systems.Loading
{
    /// <summary>
    /// 게임의 진입 지점입니다. 전역 상태를 초기화하고 첫 씬 로드를 개시하며,
    /// 자신과 자식(로더·로딩 화면)이 씬 전환에서 살아남게 합니다.
    /// </summary>
    public sealed class BootstrapEntry : MonoBehaviour
    {
        /// <summary> 씬 전환과 준비 대기를 맡는 로더. 같은 계층에 배치해 인스펙터로 잇는다. </summary>
        [SerializeField] private SceneLoadManager sceneLoad;

        /// <summary> 부팅 후 처음 열 씬입니다. 인스펙터 목록에서 고릅니다. </summary>
        [SerializeField] private SceneId firstScene = SceneId.Arena;

        /// <summary> 이 계층을 씬 전환에서 살아남게 합니다. </summary>
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        /// <summary> 전역 상태를 비우고 첫 씬 로드를 개시합니다. </summary>
        private void Start()
        {
            // 1. 전역 중재자 초기화 — 진입 지점에서 눈에 보이게 호출한다
            HoverMediator.Reset();

            // 2. 첫 씬 로드
            if (!IsValid())
                return;

            sceneLoad.LoadSceneAsync(firstScene, destroyCancellationToken).Forget();
        }

        /// <summary> 로드를 개시할 수 있는 배선인지 검사합니다. </summary>
        private bool IsValid()
        {
            if (sceneLoad == null)
            {
                Debug.LogError("[BootstrapEntry] SceneLoadManager가 할당되지 않았습니다.", this);
                return false;
            }

            return true;
        }
    }
}