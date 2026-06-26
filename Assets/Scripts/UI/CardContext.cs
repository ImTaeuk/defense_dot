using DefenseDot.Domain.Models;
using DefenseDot.Systems.Abilities;
using DefenseDot.Systems.Cards;

namespace DefenseDot.UI
{
    /// <summary> 카드 선택 프레젠터 조립 파라미터. </summary>
    public readonly struct CardContext
    {
        public readonly LevelModel Level;
        public readonly ArenaCardConfig Config;
        public readonly AbilityPool Pool;
        public readonly ICardCommandTarget Core;
        public readonly GameFlowModel Flow;

        public CardContext(LevelModel level, ArenaCardConfig config, AbilityPool pool,
            ICardCommandTarget core, GameFlowModel flow)
        {
            Level = level;
            Config = config;
            Pool = pool;
            Core = core;
            Flow = flow;
        }
    }
}
