namespace DefenseDot.Systems.Actor
{
    /// <summary> BT 트리를 코드로 조립하는 fluent 정적 빌더입니다. </summary>
    public static class BT
    {
        /// <summary> 자식을 순서대로 평가하는 Sequence를 만듭니다. </summary>
        public static BTNode Sequence(params BTNode[] children) { return new Sequence(children); }

        /// <summary> 자식을 순서대로 평가하는 Selector를 만듭니다. </summary>
        public static BTNode Selector(params BTNode[] children) { return new Selector(children); }
    }
}
