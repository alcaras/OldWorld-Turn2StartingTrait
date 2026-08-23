# owtraitmod — Turn 2 Starting Trait

An Old World C# mod. Under **Leader = Pick Later** with **Customize Leader off**, your
leader no longer rolls a random strength trait at founding. Instead, on **turn 2** you
get the familiar **Promote** popup and pick the trait yourself.

The archetype you choose when founding your capital is untouched, and so are ratings.

## Status

- ✅ Builds (`./build.sh`) and deploys to your Old World Mods folder.
- ✅ **Verified against the real game engine headlessly** — the trait strip, the turn-2 offer,
  the job branch, the human founding path and the mirrored offers are all asserted by driving
  actual games to turn 2. Not yet eyeballed in the GUI; the popup itself is stock UI.

## What it changes

| | stock | with the mod |
|---|---|---|
| leader at founding | archetype + 1–2 random strength traits | archetype only, plus an event explaining what is coming |
| turn 2 | nothing | Promote popup: pick 1 of 4 traits |

### The founding event

When you found your capital and pick your archetype, **The Measure of a King** (or Queen) fires
— a normal Old World event card with your ruler's portrait — so the missing trait reads as
intentional rather than as a bug:

> You are **{ruler}**, first ruler of **{nation}**. Your court is uncertain what sort of ruler
> you might be.
>
> "We followed you, dear **King**," says one of the elders. "The other kingdoms speak of their
> rulers as righteous, or cunning, or swift. We do not yet know you.
>
> "All we can say is that you have brought us this far. May the gods of **{nation}** smile upon
> you."
>
> Your people await your first command.
>
> — *I will show them who I am.*

Righteous, cunning and swift are all real `bUpgrade` traits, so every adjective the elder names
is one the popup can actually offer next turn.

It is a real `eventStory` in `mod/Infos/`, fired from code rather than by a trigger so it
reaches exactly the players the mod applies to, and only once their leader exists. The option
carries a help tooltip stating the mechanic plainly, so the body stays in voice.

Written against the shipped founding events (`EVENTSTORY_START_*_FOUND`), which open on
"You are {ruler}, [title] of {nation}", run three short paragraphs, and close on a
forward-looking line. The title keeps the Protagoras echo — *man is the measure of all things* —
and genders itself with `{G0:King:Queen}`, the same pattern shipped titles use.

Popup contents, following the stock level-up shape:

- **Leader with no job** — 4 traits to choose from. (Stock, in the equivalent situation,
  offers 3 rating bumps + 1 random trait; this mod offers traits only, because the point
  is to replace a trait you no longer got.)
- **Leader already a general / explorer / governor** — `UPGRADES_AVAILABLE` (3) traits
  valid for that job, plus 1 from the other pool. If the job pool holds fewer than 3
  eligible traits, the mod tops up from the other pool rather than padding with ratings.

### Any archetype, any trait

Stock gates 12 of the 33 upgrade traits on a **rating** — Intelligent and Cunning need
non-negative Wisdom, Prosperous/Frugal/Vigilant/Strict need Discipline, and so on — and
archetypes set ratings. Three archetypes therefore lose options:

| archetype | rating | stock blocks |
|---|---|---|
| Zealot | Wisdom −1 | Intelligent, Cunning |
| Schemer | Courage −1 | Warlike, Brave, Fierce |
| Orator | Discipline −1 | Prosperous, Frugal, Vigilant, Strict |

`TraitModCharacter.ALLOW_OFF_ARCHETYPE_TRAITS` (default **on**) lifts that, so every leader
draws from the same 33 whatever their archetype — the same freedom the leader customizer
already gives, where the trait list is every strength/weakness with no eligibility check at
all. It also means mirrored offers cost nothing: with equal pools, both players see the same
four every time.

Three engine facts make it work: `canAddTrait` takes a `bTestPrereqs` flag and the rating gate
lives entirely inside it; the decision-resolution path calls `addTrait(trait, bForce: true)`,
which skips `canAddTrait` outright; and `Player.isDecisionValid` is virtual, so the mod
re-implements it for `UpgradeCharacterDecision` only — otherwise `updateDecisions()` would
quietly delete the offer on the next turn.

The trait pool is the engine's own level-up pool: the 33 traits flagged `bUpgrade`, run
through `Character.isValidUpgradeTrait`. All 33 are strength traits, and they split
cleanly by role with no overlap:

| role | pool | notes |
|---|---|---|
| governor | **18** — Affable, Carpenter, Cultivator, Cunning, Delver, Eloquent, Equestrian, Frugal, Inspiring, Intelligent, Naturalist, Pathfinder, Prosperous, Righteous, Robust, Strict, Vigilant, Warlike | traits with a `GovernorEffectCity` |
| general | **15** — Besieger, Bloodthirsty, Brave, Engineer, Fierce, Heckler, Herbalist, Highlander, Horsebane, Ranger, Shieldbearer, Steadfast, Swift, Tough, Tracker | traits with a `GeneralEffectUnit`; slightly cut by the **unit type** the leader leads (see below) |
| explorer | **0** | no `bUpgrade` trait has an `ExplorerEffectUnit`, so an explorer leader draws all 4 from the full pool |
| unassigned | all **33** | |

Runtime pools are a little smaller: traits already held, unmet prereqs and DLC gating drop out.

Only units with `bGeneral` can carry a general — military units, *not* Settler, Worker, Scout,
Caravan, Militia or Conscript. Across those, `Game.isEffectUnitValid` trims the 15:

| unit kind | pool | loses |
|---|---|---|
| foot and siege (Warrior, Spearman, Archer, Legionary, Ballista, Ram, …) | **15** | — |
| mounted and elephants (Horseman, Chariot, Cataphract, War Elephant, …) | **13** | Besieger, Engineer |
| tribal foot / mounted | 14 / 12 | also Steadfast |
| ships (Bireme, Trireme, Dromon) | **6** | keeps only Tracker, Swift, Tough, Bloodthirsty, Heckler, Herbalist |

(Numbers read straight out of `Game.isEffectUnitValid` for every `bGeneral` unit.)

### Mirrored offers

Every mod-eligible leader is offered the **identical four traits, in the same order**, and the
menu does not depend on who takes their turn first. Three things make that hold:

- one **canonical trait order**, shuffled with a game-wide seed (`Game.getSeedForId`) that every
  leader derives identically, rather than each shuffling its own filtered list — shuffling
  different lists with the same seed scrambles the order instead of just skipping a trait;
- an **intersection** across leaders, because `canAddTrait` gates traits on ratings and
  archetypes set ratings, so without it two archetypes give two different menus;
- that intersection counts a trait a peer **already holds** as still offerable. `canAddTrait`
  rejects a trait its holder already has, so a plain intersection would drop whatever the first
  player had just picked out of everyone else's menu — in hot seat, player 2's fourth option
  changed because player 1 had chosen.

This restores what stock did for the trait the mod removes: in competitive 2-team games
`createInitialLeader` copies an opposing leader's strength trait so both sides start alike.

If the two leaders hold *different* roles the sets necessarily differ — a general needs
general traits. Same role, same menu. Turn it off with `TraitModCharacter.MIRROR_OFFERS`.

## How it works

Two hooks, both anchored on shipped code:

- `src/TraitModPlayer.cs`
  - `createInitialLeader` — calls base, then removes every trait in the adjective pool
    (`miAdjectiveDie > 0`), which is exactly what `Character.fillValues` →
    `generateAdjective` can hand out. The archetype is not in that pool.
  - `processTurn` — after the stock turn processing, fires the pick when
    `game.getTurn() == leader.getNationTurn() + 1` and the leader is the player's founder
    leader. `getNationTurn` is stamped when the character joins the nation, so this is true
    on exactly one turn and needs no extra saved state. Found on turn 1 → pick on turn 2;
    found late → pick the turn after founding.
  - `createNationCharacters` — fires the founding event. It has to be here rather than in
    `createInitialLeader`, which runs *before* `addLeader`, leaving `SUBJECT_LEADER_US` with
    no leader to resolve to.
- `src/TraitModCharacter.cs` — `doStartingTraitEvent()`, modelled on
  `Character.doUpgradeEvent` (GameCore/Character.cs:9094): same `isValidUpgradeTrait`
  filter, same `UpgradeCharacterDecision`, same `Player.pushDecisionDataNext`, so the popup
  and the trait application are the stock ones.

Eligibility, per player: setup archetype is `PICK_LATER_ARCHETYPE_TRAIT` **and**
`GAMEOPTION_CUSTOM_LEADER` is off. A customized leader's trait is chosen by hand, so it is
left alone; preset and fixed-archetype leaders are not targets either. AI opponents in a
normal single-player setup use a random/preset archetype, so they are unaffected — see
"Tweaks" if you want it applied to everyone.

## Build

```
./build.sh            # compile and deploy into your Old World Mods folder
./package.sh          # also produce dist/OwTraitMod-<version>.zip
```

Needs the .NET SDK and an Old World install. The project points at the macOS Steam location by
default; override it with `-p:GameManaged=<path to Old World's Managed folder>`.

In-game: **Mods → enable "Turn 2 Starting Trait" → restart → New Game** with Leader set to
**Pick Later** and **Customize Leader off**.

## Tweaks

- `TraitModCharacter.STARTING_TRAIT_CHOICES` (default 4) — how many options the popup offers.
- `TraitModCharacter.USE_UPGRADE_EVENT_TRIGGER` (default false) — set true to run the literal
  stock path, which first rolls `EVENTTRIGGER_UPGRADE_CHARACTER` and shows a story event
  instead of the trait pick when one fires. Off, because that would leave the leader with no
  starting trait at all.
- Apply to every player, AI included: drop the `Archetype != PICK_LATER_ARCHETYPE_TRAIT`
  check in `TraitModPlayer.isStartingTraitModActive`.
