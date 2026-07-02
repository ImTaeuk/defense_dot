namespace DefenseDot.Core.Pooling
{
    /// <summary> 풀이 새 인스턴스를 만들 때 호출하는 생성 창구입니다. </summary>
    public interface IPoolFactory<T> where T : class
    {
        T Create();
    }

    /// <summary> 매개변수 없는 생성자로 POCO 를 만드는 팩토리입니다. </summary>
    public sealed class PocoFactory<T> : IPoolFactory<T> where T : class, new()
    {
        public T Create() => new T();
    }
}
