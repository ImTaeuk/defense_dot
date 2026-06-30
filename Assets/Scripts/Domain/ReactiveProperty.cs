// 단일 값 변경을 구독·통지하는 경량 반응형 프로퍼티
using System.Collections.Generic;

namespace DefenseDot.Domain
{
    /// <summary> 읽기 전용 반응형 프로퍼티 계약입니다. </summary>
    public interface IReadOnlyReactiveProperty<T>
    {
        /// <summary> 현재 값입니다. </summary>
        T Value { get; }

        /// <summary> 변경을 구독합니다. 구독 즉시 현재 값을 1회 통지합니다. </summary>
        System.IDisposable Subscribe(System.Action<T> onNext);
    }

    /// <summary> 값이 실제로 바뀔 때만 통지하는 경량 반응형 프로퍼티입니다. </summary>
    public sealed class ReactiveProperty<T> : IReadOnlyReactiveProperty<T>
    {
        private T value;
        private System.Action<T> onChanged;

        /// <summary> 초기값으로 생성합니다. </summary>
        public ReactiveProperty(T initialValue = default)
        {
            value = initialValue;
        }

        /// <summary> 현재 값입니다. 같은 값 대입은 통지하지 않습니다. </summary>
        public T Value
        {
            get => value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value)) return;
                this.value = value;
                onChanged?.Invoke(this.value);
            }
        }

        /// <summary> 동등 비교를 우회해 현재 값을 강제 통지합니다. (재시작 등) </summary>
        public void SetValueAndForceNotify(T newValue)
        {
            value = newValue;
            onChanged?.Invoke(value);
        }

        /// <summary> 구독하고 즉시 현재 값을 1회 통지합니다. 토큰으로 해제합니다. </summary>
        public System.IDisposable Subscribe(System.Action<T> onNext)
        {
            if (onNext == null) return EmptyDisposable.Instance;
            onChanged += onNext;
            onNext(value);
            return new Subscription(this, onNext);
        }

        private void Remove(System.Action<T> handler)
        {
            onChanged -= handler;
        }

        private sealed class Subscription : System.IDisposable
        {
            private ReactiveProperty<T> owner;
            private System.Action<T> handler;

            public Subscription(ReactiveProperty<T> owner, System.Action<T> handler)
            {
                this.owner = owner;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (owner == null) return;
                owner.Remove(handler);
                owner = null;
                handler = null;
            }
        }

        private sealed class EmptyDisposable : System.IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
