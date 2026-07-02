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

    /// <summary> 로드된 프리팹을 Instantiate 해 PooledBehaviour 컴포넌트를 반환하는 팩토리입니다. </summary>
    public sealed class PrefabFactory : IPoolFactory<PooledBehaviour>
    {
        private readonly UnityEngine.GameObject prefab;

        public PrefabFactory(UnityEngine.GameObject prefab)
        {
            this.prefab = prefab;
        }

        public PooledBehaviour Create()
        {
            UnityEngine.GameObject go = UnityEngine.Object.Instantiate(prefab);
            return go.GetComponent<PooledBehaviour>();
        }
    }
}
