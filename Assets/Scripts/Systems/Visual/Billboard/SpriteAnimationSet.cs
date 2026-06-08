// 액터 상태별 스프라이트 프레임 묶음 (디자이너 에셋)
using UnityEngine;
using DefenseDot.Core;

namespace DefenseDot.Systems.Visual.Billboard
{
    /// <summary>
    /// 액터 상태별 스프라이트 프레임 배열과 재생 속도를 담는 설정입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpriteAnimationSet", menuName = "DefenseDot/SpriteAnimationSet")]
    public class SpriteAnimationSet : ScriptableObject
    {
        /// <summary> 상태별 프레임 묶음. </summary>
        [System.Serializable]
        public struct StateClip
        {
            /// <summary> 대상 액터 상태 </summary>
            public ActorState state;
            /// <summary> 순서대로 재생할 프레임 </summary>
            public Sprite[] frames;
            /// <summary> 순환 재생 여부 </summary>
            public bool loop;
        }

        /// <summary> 초당 프레임 수 </summary>
        public float framesPerSecond = 8f;
        /// <summary> 상태별 클립 목록 </summary>
        public StateClip[] clips;

        /// <summary>
        /// 상태에 해당하는 클립을 찾습니다. 없으면 found=false.
        /// </summary>
        public StateClip GetClip(ActorState state, out bool found)
        {
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].state == state)
                    {
                        found = true;
                        return clips[i];
                    }
                }
            }
            found = false;
            return default;
        }
    }
}
