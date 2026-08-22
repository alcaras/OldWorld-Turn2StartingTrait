using TenCrowns.GameCore;

namespace OwTraitMod
{
    // Only Player and Character are replaced; everything else falls through to stock.
    public class OwTraitModFactory : GameFactory
    {
        public override Player CreatePlayer()
        {
            return new TraitModPlayer();
        }

        public override Character CreateCharacter()
        {
            return new TraitModCharacter();
        }
    }
}
