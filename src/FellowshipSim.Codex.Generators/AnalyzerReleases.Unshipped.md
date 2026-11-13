; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CODEX001 | Codex | Error | CreatureGenerator: the Codex export is missing
CODEX002 | Codex | Error | CreatureGenerator: a Codex export line does not read
CODEX003 | Codex | Error | LegendaryGenerator: a partial legendary class names no legendary power
CODEX005 | Codex | Error | LegendaryGenerator: a legendary power's items name several heroes
CODEX006 | Codex | Error | AbilityGenerator: a partial ability class names no ability record
CODEX007 | Codex | Error | AbilityGenerator: a partial ability class names several ability records
CODEX009 | Codex | Error | AbilityGenerator: an ability constant's name is a member of the class or a base class
CODEX010 | Codex | Error | TalentGenerator: a partial talent class names no talent record
CODEX011 | Codex | Error | TalentGenerator: a partial talent class names several talent records
CODEX013 | Codex | Error | TalentGenerator: a talent constant's name is a member of the class or a base class
CODEX014 | Codex | Error | AugmentationGenerator: a partial augmentation class names no augmentation record
CODEX015 | Codex | Error | AugmentationGenerator: a partial augmentation class names several augmentation records
CODEX017 | Codex | Error | AugmentationGenerator: an augmentation constant's name is a member of the class or a base class
CODEX018 | Codex | Error | GlyphGenerator: a partial glyph class names no glyph record
CODEX019 | Codex | Error | GlyphGenerator: a partial glyph class names several glyph records
CODEX021 | Codex | Error | GlyphGenerator: a glyph constant's name is a member of the class or a base class
CODEX022 | Codex | Error | WeaponGenerator: a partial weapon or weapon ability class names no weapon power record
CODEX023 | Codex | Error | WeaponGenerator: a partial weapon or weapon ability class names several weapon power records
CODEX025 | Codex | Error | WeaponGenerator: a weapon constant's name is a member of the class or a base class
CODEX026 | Codex | Error | RelicPassiveGenerator: a partial relic passive class names no relic passive record
CODEX027 | Codex | Error | RelicPassiveGenerator: a partial relic passive class names several relic passive records
CODEX028 | Codex | Warning | RelicPassiveGenerator: a relic passive's slot has several items
CODEX029 | Codex | Error | RelicPassiveGenerator: a relic passive constant's name is a member of the class or a base class
CODEX030 | Codex | Error | HeroGenerator: a partial hero class names no hero record
CODEX031 | Codex | Error | HeroGenerator: a partial hero class names several hero records
CODEX033 | Codex | Error | HeroGenerator: a hero constant's name is a member of the class or a base class
CODEX034 | Codex | Error | DungeonGenerator: a partial dungeon class names no dungeon record
CODEX036 | Codex | Error | DungeonGenerator: a dungeon constant's name is a member of the class or a base class
CODEX037 | Codex | Warning | DungeonGenerator: a dungeon placement names no creature record
CODEX038 | Codex | Error | TuningNames: two tuning keys of a record have different values and one constant name
CODEX039 | Codex | Error | TuningNames: a hand-written constant or static readonly field of the class has the name of a member of its Constants
CODEX040 | Codex | Error | ItemGenerator: an item has a stat no build holds
CODEX041 | Codex | Error | ItemGenerator: an item at its most tempers has other stats than its untempered stats and the tempers
CODEX042 | Codex | Error | ItemGenerator: a wearable slot has no GearKind member
CODEX043 | Codex | Error | ItemGenerator: two items of one hero have one ID
CODEX044 | Codex | Error | ItemGenerator: a weapon power grants no ability
CODEX045 | Codex | Error | ItemGenerator: a wearable slot has no empty slot icon
CODEX046 | Codex | Error | AbilityGenerator: an ability class's Ability attribute names no ability record
CODEX047 | Codex | Error | AbilityGenerator: an ability record's base ability has no partial class
CODEX048 | Codex | Error | DungeonAffixGenerator: a partial dungeon affix class names no dungeon affix record
CODEX049 | Codex | Error | DungeonAffixGenerator: a partial dungeon affix class names several dungeon affix records
CODEX050 | Codex | Warning | DungeonAffixGenerator: several effects of a dungeon affix have one name
CODEX051 | Codex | Error | DungeonAffixGenerator: a dungeon affix constant's name is a member of the class or a base class
