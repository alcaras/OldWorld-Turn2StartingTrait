using System.Collections.Generic;
using Mohawk.SystemCore;
using TenCrowns.GameCore;

namespace OwTraitMod
{
    /// <summary>
    /// Two hooks:
    ///   1. createInitialLeader — strip the random strength trait the engine rolls for a
    ///      freshly created leader (Character.fillValues -> generateAdjective).
    ///   2. processTurn — one turn later, hand that leader a level-up-style trait pick.
    /// Both only apply to players whose leader was set to "Pick Later" with the
    /// Customize Leader option off.
    /// </summary>
    public class TraitModPlayer : Player
    {
        /// <summary>
        /// The leader is built by Player.createInitialLeader -> createAdult -> fillValues,
        /// which adds the archetype and then rolls 1-2 random strength "adjective" traits.
        /// Keep the archetype (the player picked it when founding); drop the rolled ones.
        /// </summary>
        protected override Character createInitialLeader(TraitType eArchetype, TraitType eTrait = TraitType.NONE)
        {
            Character pLeader = base.createInitialLeader(eArchetype, eTrait);

            if ((pLeader != null) && isStartingTraitModActive())
            {
                stripRolledTraits(pLeader);
            }

            return pLeader;
        }

        /// <summary>Remove every trait from the adjective pool (miAdjectiveDie), i.e. exactly
        /// what generateAdjective can hand out. The archetype and the adult traits
        /// (GAY/BISEXUAL, from doAdultTrait) are not in that pool and are left alone.</summary>
        protected virtual void stripRolledTraits(Character pLeader)
        {
            using (var traitListScoped = CollectionCache.GetListScoped<TraitType>())
            {
                List<TraitType> aeTraits = traitListScoped.Value;
                pLeader.getTraits(aeTraits);

                foreach (TraitType eLoopTrait in aeTraits)
                {
                    if (infos().trait(eLoopTrait).mbArchetype)
                    {
                        continue;
                    }
                    if (infos().trait(eLoopTrait).miAdjectiveDie <= 0)
                    {
                        continue;
                    }

                    MohawkLog.Log($"[OwTraitMod] stripped starting trait {infos().trait(eLoopTrait).mzType} from player {(int)getPlayer()}'s leader");
                    pLeader.removeTrait(eLoopTrait);
                }
            }
        }

        protected override void processTurn()
        {
            // processTurn no-ops when it has already run this turn; only follow a real one.
            bool bFirstThisTurn = (getLastDoTurn() != game().getTurn());

            base.processTurn();

            if (bFirstThisTurn)
            {
                tryGrantStartingTrait();
            }
        }

        /// <summary>
        /// Fire the trait pick on the turn after the founding leader appeared - turn 2 for a
        /// turn-1 founding. getNationTurn is stamped when the character joins the nation
        /// (Game.createCharacterNext), so this is true on exactly one turn and needs no extra
        /// saved state. Restricted to the founding leader so no later heir re-triggers it.
        /// </summary>
        protected virtual void tryGrantStartingTrait()
        {
            if (!isStartingTraitModActive())
            {
                return;
            }
            if (!hasLeader())
            {
                return;
            }

            Character pLeader = leader();

            if ((pLeader == null) || !pLeader.isAlive())
            {
                return;
            }

            List<int> aiLeaders = getLeaders();

            if ((aiLeaders.Count != 1) || (aiLeaders[0] != pLeader.getID()))
            {
                return;
            }
            if (game().getTurn() != (pLeader.getNationTurn() + 1))
            {
                return;
            }

            TraitModCharacter pModLeader = pLeader as TraitModCharacter;

            if (pModLeader != null)
            {
                MohawkLog.Log($"[OwTraitMod] turn {game().getTurn()}: offering the starting trait to player {(int)getPlayer()}'s leader");
                pModLeader.doStartingTraitEvent();
            }
        }

        /// <summary>Active for a player whose setup archetype is "Pick Later" while the
        /// Customize Leader option is off - a customized leader's trait is chosen by hand,
        /// and a preset/random leader was never the target of this mod.</summary>
        public virtual bool isStartingTraitModActive()
        {
            if (!game().isCharacters())
            {
                return false;
            }

            GameParameters pParameters = game().getGameParameters();

            if (pParameters == null)
            {
                return false;
            }

            int iPlayer = (int)getPlayer();

            if ((iPlayer < 0) || (iPlayer >= pParameters.lPlayerParameters.Count))
            {
                return false;
            }
            if (pParameters.lPlayerParameters[iPlayer].Archetype != infos().Globals.PICK_LATER_ARCHETYPE_TRAIT)
            {
                return false;
            }
            if ((infos().Globals.GAMEOPTION_CUSTOMIZE_LEADER != GameOptionType.NONE) && game().isGameOption(infos().Globals.GAMEOPTION_CUSTOMIZE_LEADER))
            {
                return false;
            }

            return true;
        }
    }
}
