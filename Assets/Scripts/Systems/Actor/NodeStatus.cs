namespace DefenseDot.Systems.Actor
{
    /// <summary> BT 노드 1회 평가 결과입니다. </summary>
    public enum NodeStatus
    {
        /// <summary> 아직 수행 중. </summary>
        Running,
        /// <summary> 성공으로 종료. </summary>
        Success,
        /// <summary> 실패로 종료. </summary>
        Failure
    }
}
