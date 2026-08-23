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
        const string UNWRITTEN_REPUTATION_EVENTSTORY = "EVENTSTORY_OWTRAITMOD_UNWRITTEN_REPUTATION";

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

        /// <summary>
        /// createInitialLeader runs before addLeader, so the event has to be fired from here:
        /// SUBJECT_LEADER_US cannot resolve until the leader is registered with the player.
        /// </summary>
        public override void createNationCharacters(TraitType eLeaderArchetype, TraitType eTrait = TraitType.NONE, NameType eName = NameType.NONE, GenderType eGender = GenderType.NONE, int iAge = 0, CharacterPortraitType ePortrait = CharacterPortraitType.NONE)
        {
            bool bHadLeader = hasLeader();

            base.createNationCharacters(eLeaderArchetype, eTrait, eName, eGender, iAge, ePortrait);

            if (!bHadLeader && hasLeader() && isStartingTraitModActive())
            {
                doUnwrittenReputationEvent(leader());
            }
        }

        /// <summary>
        /// Tell the player their ruler has no reputation yet and will earn one next turn.
        /// Fired here rather than from an eventTrigger because it has to reach exactly the
        /// players this mod applies to, and only once their leader exists — under Pick Later
        /// that is the moment they found their capital, not the start of the game.
        /// Silently skipped if the mod's XML is not installed alongside the DLL.
        /// </summary>
        protected virtual void doUnwrittenReputationEvent(Character pLeader)
        {
            if (!isHuman() || !canDoEvents())
            {
                return;
            }

            EventStoryType eEventStory = infos().getType<EventStoryType>(UNWRITTEN_REPUTATION_EVENTSTORY, false);

            if (eEventStory == EventStoryType.NONE)
            {
                MohawkLog.Log("[OwTraitMod] " + UNWRITTEN_REPUTATION_EVENTSTORY + " not found - Infos/ missing from the mod folder?");
                return;
            }

            using (var subjectListScoped = CollectionCache.GetListScoped<object>())
            {
                List<object> lSubjects = subjectListScoped.Value;
                lSubjects.Add(pLeader);

                if (!doEventStory(eEventStory, game().getSeedForId(pLeader.getID()), bModal: true, lSubjects))
                {
                    MohawkLog.Log("[OwTraitMod] could not fire " + UNWRITTEN_REPUTATION_EVENTSTORY);
                }
            }
        }

        /// <summary>
        /// Stock validates an UpgradeCharacterDecision with canAddTrait(bTestPrereqs: true), so
        /// an offer containing a trait the leader's archetype gates on a rating would be refused
        /// at push time and then culled by updateDecisions. Re-implement just that decision type
        /// with the gate under our control - deliberately mirroring the stock checks (queue
        /// membership, character alive, every trait addable) so nothing else is waved through.
        /// The resolution path already calls addTrait(eTrait, bForce: true), which skips
        /// canAddTrait entirely, so the chosen trait applies.
        /// </summary>
        public override bool isDecisionValid(DecisionData pDecision, bool bCheckQueue = true)
        {
            if (!TraitModCharacter.ALLOW_OFF_ARCHETYPE_TRAITS || !(pDecision is UpgradeCharacterDecision pUpgrade))
            {
                return base.isDecisionValid(pDecision, bCheckQueue);
            }

            if (bCheckQueue && !getDecisionList().Contains(pDecision))
            {
                return false;
            }

            Character pCharacter = game().character(pUpgrade.getCharacterID());

            if ((pCharacter == null) || pCharacter.isDead())
            {
                return false;
            }

            int iNumUpgrades = pUpgrade.getNumUpgrades();

            for (int i = 0; i < iNumUpgrades; ++i)
            {
                TraitType eTrait = pUpgrade.getTrait(i);

                if ((eTrait != TraitType.NONE)
                    && !game().canAddTrait(eTrait, pCharacter, CharacterType.NONE, bTestPrereqs: TraitModCharacter.TestPrereqs))
                {
                    return false;
                }
            }
            return true;
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
