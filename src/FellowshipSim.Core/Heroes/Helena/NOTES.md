# Helena: mechanics in the combat logs

The fights are the 18 Helena fights of patch 0.5.0.0 in `data/fights`, from `tools/fetch-fights.cs --hero Helena`.
7Q1wTztGbqpCm9ga fight 8 has no combatant info for its Helena, so it has no build and no replay.

## Resources

- Toughness is resource type 3: the amount and the maximum, in hundredths. The maximum changes during a fight
  (HrNZ6wQfFKGxybDR fight 1: 769,174 at 2.0s, 1,053,920 at 13.0s, 1,550,152 at 24.4s), so the replay compares the share
  of the maximum (`LogResources`).
- Spirit Points are resource type 4, as for every hero.
- The game data gives Shield Slam and Shields Up a cost of 3 Combo Points and Sweeping Strike 2. The logs have no such
  resource for Helena, and nothing she casts generates one: 6,572 Shield Slams are cast with no cost.

## Veteran of War

- Each reduction is twice the seconds its tooltip names. With the tooltips' seconds the replay finds Shockwave on cooldown
  at 427 of its logged casts, Shield Throw at 338 and Shield Slam at 274; with twice them at 0, 0 and 199.
- A Spirit Refund on a Veteran of War cast doubles that cast's reductions again (hero 22 spiritAbility). Without it the
  replay finds Shield Throw on cooldown at 218 casts, 12.3s early at the most common.
- Rising Spirit (effect 3115) comes with each Spirit Refund and stacks to 10, as for every hero.

## Abilities

- Shockwave's hits are effect 1272, all in the cast's server frame, on up to 20 enemies. Its slow is effect 1270.
- Shield Slam's hits on the main target and on the other enemies are all logged as 977, in the cast's server frame.
- Shield Throw's first hit lands 0.27-0.37s after the cast; each bounce lands 0.03-0.2s after the hit before it, on an
  enemy the shield has not hit yet. A cast has 3.5 hits on average (9,856 hits from 2,778 casts).
- Sweeping Strike's hits lose damage as sqrt(1/N): v8rhNxVc3PwDqzBA fight 4 @ 12.823 strikes 15 enemies, and each hit
  is 0.93-1.08 of the engine's sqrt(1/15) share. Its bleed ticks are effect 1276.
- Charge is cast up to 1.1s into a global cooldown.
- Razor Shrapnel (effect 2357) deals 40% of the Shield Throw hit that set it off, in that hit's server frame, and never
  crits (3,260 hits).

## Talents

- Aftershock: Helena's hits on an enemy with the debuff (effect 2428) deal 1.19-1.23 times what they deal without it, as
  the game data's IncomingDamageScaler 1.2 says, against the tooltip's 30%.
- Siegebreaker with Martial Command: every hit of Helena's deals about 0.93 of the engine's damage while Siegebreaker is
  up (17 fights, 0.91-0.98 per fight), Shield Slam, Shield Throw and Shockwave as much as the others.
- The Best Defense (effect 1231) comes 389 times in 7 fights, each time from a block.
- Second Wind (effect 1215) comes 176 times in 4 fights; its build-up is effect 1214.
