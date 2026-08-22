using System;
using System.Collections.Generic;
using Mohawk.SystemCore;
using TenCrowns.GameCore;

namespace OwTraitMod
{
    /// <summary>
    /// Adds the turn-2 "starting trait" pick, built from the stock level-up code
    /// (Character.doUpgradeEvent, Reference/Source/.../GameCore/Character.cs:9094).
    /// </summary>
    public class TraitModCharacter : Character
    {
        /// <summary>How many options the turn-2 popup offers. Stock level-up shows
        /// UPGRADES_AVAILABLE (3) role traits plus 1 extra trait = 4.</summary>
        public static int STARTING_TRAIT_CHOICES = 4;

        /// <summary>Stock doUpgradeEvent first rolls EVENTTRIGGER_UPGRADE_CHARACTER and
        /// skips the decision entirely if a story fires. That would rob the leader of the
        /// starting trait this mod owes them, so it is off. Flip to true for the literal
        /// stock code path.</summary>
        public static bool USE_UPGRADE_EVENT_TRIGGER = false;

        /// Offer every mod-eligible leader the SAME options, so a competitive game is decided
        /// by the choice rather than the roll. Stock does the equivalent for the trait this
        /// mod removes: createInitialLeader (Player.cs:16400) copies an opposing leader's
        /// strength trait in competitive 2-team games. Set false for independent rolls.
        public static bool MIRROR_OFFERS = true;

        /// Arbitrary fixed ids feeding Game.getSeedForId, so every leader shuffles the same
        /// list the same way. Two ids so the job and other pools are not correlated.
        const int SHARED_SEED_JOB = 97;
        const int SHARED_SEED_OTHER = 98;

        /// <summary>
        /// Push the level-up decision that hands this character their starting trait.
        /// Mirrors Character.doUpgradeEvent, with two deliberate changes:
        ///   * options are always traits (stock pads with +1 rating options),
        ///   * an unassigned leader gets STARTING_TRAIT_CHOICES traits rather than the
        ///     single random one stock offers when no job-specific traits qualify.
        /// A leader who already holds a job keeps the stock shape: UPGRADES_AVAILABLE
        /// job traits plus one from the other pool.
        /// </summary>
        public virtual void doStartingTraitEvent()
        {
            if (!hasPlayer())
            {
                return;
            }

            if (USE_UPGRADE_EVENT_TRIGGER)
            {
                if (player().doEventTrigger(infos().Globals.UPGRADE_CHARACTER_EVENTTRIGGER, pTriggerSubject: this))
                {
                    return;
                }
            }

            bool bGeneral = isUnitGeneral();
            bool bExplorer = isUnitExplorer();
            bool bGovernor = isCityGovernor();
            bool bJob = (bGeneral || bExplorer || bGovernor);

            using (var jobListScoped = CollectionCache.GetListScoped<TraitType>())
            using (var otherListScoped = CollectionCache.GetListScoped<TraitType>())
            using (var choiceListScoped = CollectionCache.GetListScoped<TraitType>())
            using (var peerListScoped = CollectionCache.GetListScoped<TraitModCharacter>())
            {
                List<TraitType> aeJobTraits = jobListScoped.Value;
                List<TraitType> aeOtherTraits = otherListScoped.Value;
                List<TraitType> aeChoices = choiceListScoped.Value;
                List<TraitModCharacter> aPeers = peerListScoped.Value;

                if (MIRROR_OFFERS)
                {
                    collectPeerLeaders(aPeers);
                }

                for (TraitType eLoopTrait = 0; eLoopTrait < infos().traitsNum(); eLoopTrait++)
                {
                    if (!isValidUpgradeTrait(eLoopTrait, false, false, false))
                    {
                        continue;
                    }
                    // Mirroring: only offer what every other eligible leader could also take,
                    // so all of them build the identical candidate list.
                    if (!isValidForPeers(aPeers, eLoopTrait))
                    {
                        continue;
                    }

                    if (bJob && isValidUpgradeTrait(eLoopTrait, bGeneral, bExplorer, bGovernor))
                    {
                        aeJobTraits.Add(eLoopTrait);
                    }
                    else
                    {
                        aeOtherTraits.Add(eLoopTrait);
                    }
                }

                aeJobTraits.Shuffle(sharedSeed(SHARED_SEED_JOB));
                aeOtherTraits.Shuffle(sharedSeed(SHARED_SEED_OTHER));

                // Job traits first (capped like stock at UPGRADES_AVAILABLE), then top up
                // from the other pool. Stock would fill the shortfall with ratings instead.
                int iJobWanted = (bJob ? Math.Min(infos().Globals.UPGRADES_AVAILABLE, STARTING_TRAIT_CHOICES) : 0);
                addChoices(aeChoices, aeJobTraits, Math.Min(iJobWanted, aeJobTraits.Count));
                addChoices(aeChoices, aeOtherTraits, STARTING_TRAIT_CHOICES - aeChoices.Count);
                addChoices(aeChoices, aeJobTraits, STARTING_TRAIT_CHOICES - aeChoices.Count);

                if (aeChoices.Count > 0)
                {
                    UpgradeCharacterDecision pDecision = new UpgradeCharacterDecision(player().nextDecisionID(), infos(), getID(), !(player().isProcessingTurn()));

                    for (int i = 0; i < aeChoices.Count; ++i)
                    {
                        pDecision.setTrait(i, aeChoices[i]);
                    }

                    var zNames = new List<string>();
                    foreach (TraitType eTrait in aeChoices)
                    {
                        zNames.Add(infos().trait(eTrait).mzType);
                    }
                    MohawkLog.Log($"[OwTraitMod] starting-trait options (general={bGeneral} explorer={bExplorer} governor={bGovernor}): {string.Join(", ", zNames)}");

                    player().pushDecisionDataNext(pDecision);
                }
            }
        }

        /// <summary>A game-wide seed every leader derives identically, so the same list
        /// shuffles into the same order for all of them.</summary>
        protected virtual ulong sharedSeed(int iID)
        {
            return MIRROR_OFFERS ? game().getSeedForId(iID) : nextRandomSeed();
        }

        /// <summary>The other leaders this mod is offering a starting trait to.</summary>
        protected virtual void collectPeerLeaders(List<TraitModCharacter> aPeers)
        {
            for (PlayerType eLoopPlayer = 0; eLoopPlayer < game().getNumPlayers(); eLoopPlayer++)
            {
                TraitModPlayer pLoopPlayer = game().player(eLoopPlayer) as TraitModPlayer;

                if ((pLoopPlayer == null) || !pLoopPlayer.isStartingTraitModActive() || !pLoopPlayer.hasLeader())
                {
                    continue;
                }

                TraitModCharacter pLoopLeader = pLoopPlayer.leader() as TraitModCharacter;

                if ((pLoopLeader == null) || (pLoopLeader == this) || !pLoopLeader.isAlive())
                {
                    continue;
                }

                aPeers.Add(pLoopLeader);
            }
        }

        protected virtual bool isValidForPeers(List<TraitModCharacter> aPeers, TraitType eTrait)
        {
            foreach (TraitModCharacter pPeer in aPeers)
            {
                if (!pPeer.isValidUpgradeTrait(eTrait, false, false, false))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Move up to iCount not-yet-chosen traits from aeSource into aeChoices.</summary>
        protected virtual void addChoices(List<TraitType> aeChoices, List<TraitType> aeSource, int iCount)
        {
            for (int i = 0; (i < aeSource.Count) && (iCount > 0); ++i)
            {
                if (!aeChoices.Contains(aeSource[i]))
                {
                    aeChoices.Add(aeSource[i]);
                    --iCount;
                }
            }
        }
    }
}
