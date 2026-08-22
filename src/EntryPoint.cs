using Mohawk.SystemCore;
using TenCrowns.AppCore;
using TenCrowns.GameCore;

namespace OwTraitMod
{
    // Mod entry point. The game instantiates this and calls Initialize; we install
    // our Factory so CreatePlayer/CreateCharacter return the modded subclasses.
    public class OwTraitModEntryPoint : ModEntryPointAdapter
    {
        public override void Initialize(ModSettings modSettings)
        {
            base.Initialize(modSettings);
            modSettings.Factory = new OwTraitModFactory();
            MohawkLog.Log("[OwTraitMod] loaded: Pick-Later leaders start with no rolled trait and pick one on turn 2");
        }
    }
}
