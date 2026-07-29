// 호버 패널에 넘길 표시 데이터
using UnityEngine;

namespace DefenseDot.UI.Hover
{
    /// <summary> 호버 패널에 표시할 내용입니다. </summary>
    public readonly struct HoverContent
    {
        /// <summary> 패널에 표시할 문자열. </summary>
        public readonly string Text;

        /// <summary> 패널을 띄울 월드 좌표. 감지 객체가 정해서 넘긴다. </summary>
        public readonly Vector3 Position;

        /// <summary> 표시 내용을 구성합니다. </summary>
        /// <param name="text">패널에 표시할 문자열</param>
        /// <param name="position">패널을 띄울 월드 좌표</param>
        public HoverContent(string text, Vector3 position)
        {
            Text = text;
            Position = position;
        }
    }
}