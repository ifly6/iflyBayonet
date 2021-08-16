# iflyBayonet
Adds a belt-equippable bayonet which attaches to bayonet-compatible weapons automatically without needing to create a whole new gun. Those bayonets can then be used in melee combat (if also equipping a compatible bayonet-luggable ranged weapon). Bayonets cannot be used alone; an invisible force holds them to the belt.

This mod also patches the average melee damage calculation and battle log to report use of the bayonet. It also creates an alert if a pawn equips a bayonet belt without also having a compatible weapon. If your pawns are equipping the belt willy-nilly and you don't like this, change your Apparel rules in Assign.

Bayonets are created at the smithing table and have most of their statistics taken from a combination of knives and spears. The cost is a bit more expensive than a knife (the model of the bayonet is that of a 1907 SMLE bayonet with quillion) and the damage is a bit less than a spear, being a bit more clunky.

## Installation
[Subscribe on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2575309478).

## Technical information
For the technically minded, the mod specifically changes the bayonet-compatible weapon's "poke" melee attacks. It leaves other weapon types, like bashing with a stock, in, replacing any time a bayonet-compatible weapon pokes with a bayonet stab. This is done by patches in Harmony.

As a broad overview, the patches are grouped into three main sections:

- `Patch_MeleeDamageCalc`
  -  Determines whether the weapon is a bayonet compatible weapon (see `Utilities` and `Utilities.IsNotCrapWeapon`)
  -  Determines whether a pawn can use the bayonet (`Utilities.GetBayonetBeltIfValidWielder`)
  -  Patches the melee damage calculations at `VerbProperties.AdjustedMeleeDamageAmount` if pawn is a valid wielder by changing the input parameters so that a pawn is attacking _as if_ he is using the bayonet
  -  Checks that replacement was successful in `Pawn_MeleeVerbs.TryMeleeAttack`

- `Patch_MeleeExplanation`
  - Does string matching to generate and then replace the explanation such that it reflects use of the bayonet.
  - Patches here are difficult due to RimWorld code not calculating melee damage on `Thing` but, rather, on `ThingDef`. Any patch would have to go in the middle of a method to execute properly.

- `Patch_BayonetWield`
  - When a weapon is equipped and it is a valid bayonet combination, the attack tool is replaced with a bayonet. See `Pawn_EquipmentTracker.AddEquipment`. Replacing that attack tool also updates the battle log to reflect use of bayonet accurately. This is also triggered if a wielded weapon is complemented with a bayonet. See `Pawn_ApparelTracker.Wear`.
  - When a save game is loaded, there is no equip call; so patching is also done at `Map.FinalizeInit` to ensure that all pawns' weapons have their bayonets fixed.
  - When something is dropped or unequipped, a call is made to patch the weapon back to its original non-bayonet state.

- `Patch_MeleeManoeuvre`
  - In verb melee attack, if you are attacking properly with a bayonet, you should be using the stab manoeuvre, which also determines the flavour text in the battle log.
  - If the bayonet is configured properly, the pawn is validly using a bayonet, and the verb is using the bayonet tool as expected in `Patch_BayonetWield`, this changes the manoeuvre in the verb as it is attacking, into a stab. After the melee attack is cast, it reverts that change.

An alert, based on the code for the alert with shield belt wearers and ranged weapons, is also added so that players know if pawns are equipping items not compatible with the bayonet belt.
