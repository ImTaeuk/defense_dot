namespace DefenseDot.Systems.Actor
{
    /// <summary>
    /// 상태를 바꾸는 동작 노드의 베이스입니다. 조건 노드와 구분하는 표식이며,
    /// 반환 상태(Running/Success/Failure)가 동작마다 달라 공통 로직은 두지 않습니다.
    /// </summary>
    public abstract class ActionNode : BTNode
    {
    }
}