using System.Collections.Generic;

namespace DefenseDot.Core.Pooling
{
    /// <summary>
    /// 큐 기반 재사용 풀입니다. 놀고 있으면 꺼내고, 없으면 팩토리로 새로 만듭니다.
    /// 활성/리셋은 IActivatable/IPoolable 로 위임하므로 MB·POCO 를 모릅니다.
    /// </summary>
    public sealed class Pool<T> : IPool where T : class, IPoolable, IActivatable
    {
        private readonly Queue<T> idle = new Queue<T>();
        private readonly IPoolFactory<T> factory;

        public Pool(IPoolFactory<T> factory)
        {
            this.factory = factory;
        }

        public T Get()
        {
            T item = idle.Count > 0 ? idle.Dequeue() : factory.Create();
            item.OnSpawn();
            item.Activate();
            return item;
        }

        public void Return(T item)
        {
            item.Deactivate();
            item.OnDespawn();
            idle.Enqueue(item);
        }

        void IPool.ReturnObject(object item) => Return((T)item);
        void IPool.Clear() => idle.Clear();
    }

    /// <summary> 서로 다른 T 의 Pool 을 한 레지스트리에 담기 위한 비제네릭 창구입니다. </summary>
    internal interface IPool
    {
        void ReturnObject(object item);
        void Clear();
    }
}
