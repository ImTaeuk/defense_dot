// 로딩이 기다려야 할 준비 작업 하나 — 구현체가 스스로 등록한다
using Cysharp.Threading.Tasks;

namespace DefenseDot.Systems.Loading
{
    /// <summary>
    /// 로딩이 완료를 기다려야 할 준비 작업 하나입니다.
    /// 구현체가 SceneLoadManager 에 자신을 등록하며, 진행률은 등록된 개수로 산출됩니다.
    /// </summary>
    public interface ISceneWarmup
    {
        /// <summary> 이 대상을 사용 가능한 상태로 만듭니다. </summary>
        /// <param name="cancellationToken">씬 파괴 등으로 중단할 때 쓰는 토큰</param>
        UniTask WarmupAsync(System.Threading.CancellationToken cancellationToken);
    }
}