// 도메인 모델 공통 기반 — 값이 실제로 바뀔 때만 통지하는 헬퍼 제공
using System.Collections.Generic;

namespace DefenseDot.Domain
{
    /// <summary>
    /// 모든 도메인 모델의 기반 클래스입니다.
    /// 상태가 실제로 변경된 경우에만 통지하도록 돕는 헬퍼를 제공합니다.
    /// </summary>
    public abstract class BaseModel
    {
        /// <summary>
        /// 값이 기존과 다를 때만 필드를 갱신하고 true를 반환합니다. (중복 통지 방지)
        /// </summary>
        protected bool SetField<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            return true;
        }
    }
}
