// unscaled 시간을 셰이더 전역에 주입
using UnityEngine;

namespace DefenseDot.Systems.Rendering
{
    /// <summary>
    /// 매 프레임 Time.unscaledTime 을 전역 셰이더 프로퍼티(_UnscaledTime)로 주입합니다.
    /// 홀로그램 셰이더가 _Time.y(timeScale 영향) 대신 이 값을 써서 일시정지 중에도 흐릅니다.
    /// 부팅 시 자동 생성되어 별도 배선이 필요 없습니다.
    /// </summary>
    public sealed class UnscaledTimeShaderDriver : MonoBehaviour
    {
        private static readonly int unscaledTimeId = Shader.PropertyToID("_UnscaledTime");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject go = new GameObject("[UnscaledTimeShaderDriver]");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<UnscaledTimeShaderDriver>();
        }

        private void Update()
        {
            Shader.SetGlobalFloat(unscaledTimeId, Time.unscaledTime);
        }
    }
}
