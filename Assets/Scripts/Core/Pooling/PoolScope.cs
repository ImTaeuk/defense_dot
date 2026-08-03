namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 풀이 씬 전환을 넘어 살아남는지를 정합니다. 예열할 때 호출자가 지정합니다.
    /// </summary>
    public enum PoolScope
    {
        /// <summary> 씬과 함께 정리됩니다. 지정하지 않으면 이 값입니다. </summary>
        Scene,

        /// <summary> 씬이 바뀌어도 유지됩니다. 앱 종료 시에만 해제됩니다. </summary>
        Global,
    }
}
