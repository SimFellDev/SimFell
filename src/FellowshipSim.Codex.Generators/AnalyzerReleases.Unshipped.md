; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CODEX001 | Codex | Error | CreatureGenerator: the Codex export is missing
CODEX002 | Codex | Error | CreatureGenerator: a Codex export line does not read
CODEX003 | Codex | Error | LegendaryGenerator: a partial legendary class names no legendary power
CODEX004 | Codex | Error | LegendaryGenerator: two tuning values of a legendary power share a name
CODEX005 | Codex | Error | LegendaryGenerator: a legendary power's items name several heroes
CODEX006 | Codex | Error | AbilityGenerator: a partial ability class names no ability record
CODEX007 | Codex | Error | AbilityGenerator: a partial ability class names several ability records
CODEX008 | Codex | Warning | AbilityGenerator: two tuning values of an ability share a name
CODEX009 | Codex | Warning | AbilityGenerator: an ability constant's name is a member of the class or a base class
CODEX010 | Codex | Error | TalentGenerator: a partial talent class names no talent record
CODEX011 | Codex | Error | TalentGenerator: a partial talent class names several talent records
CODEX012 | Codex | Warning | TalentGenerator: two tuning values of a talent share a name
CODEX013 | Codex | Warning | TalentGenerator: a talent constant's name is a member of the class or a base class
CODEX014 | Codex | Error | AugmentationGenerator: a partial augmentation class names no augmentation record
CODEX015 | Codex | Error | AugmentationGenerator: a partial augmentation class names several augmentation records
CODEX016 | Codex | Warning | AugmentationGenerator: two values of an augmentation share a name
CODEX017 | Codex | Warning | AugmentationGenerator: an augmentation constant's name is a member of the class or a base class
CODEX018 | Codex | Error | GlyphGenerator: a partial glyph class names no glyph record
CODEX019 | Codex | Error | GlyphGenerator: a partial glyph class names several glyph records
CODEX020 | Codex | Warning | GlyphGenerator: two tuning values of a glyph share a name
CODEX021 | Codex | Warning | GlyphGenerator: a glyph constant's name is a member of the class or a base class
CODEX022 | Codex | Error | WeaponGenerator: a partial weapon or weapon ability class names no weapon power record
CODEX023 | Codex | Error | WeaponGenerator: a partial weapon or weapon ability class names several weapon power records
CODEX024 | Codex | Warning | WeaponGenerator: two tuning values of a weapon power share a name
CODEX025 | Codex | Warning | WeaponGenerator: a weapon constant's name is a member of the class or a base class
CODEX026 | Codex | Error | RelicPassiveGenerator: a partial relic passive class names no relic passive record
CODEX027 | Codex | Error | RelicPassiveGenerator: a partial relic passive class names several relic passive records
CODEX028 | Codex | Warning | RelicPassiveGenerator: two values of a relic passive share a name
CODEX029 | Codex | Warning | RelicPassiveGenerator: a relic passive constant's name is a member of the class or a base class
CODEX030 | Codex | Error | HeroGenerator: a partial hero class names no hero record
CODEX031 | Codex | Error | HeroGenerator: a partial hero class names several hero records
CODEX032 | Codex | Warning | HeroGenerator: two tuning values of a hero share a name
CODEX033 | Codex | Warning | HeroGenerator: a hero constant's name is a member of the class or a base class
CODEX034 | Codex | Error | DungeonGenerator: a partial dungeon class names no dungeon record
CODEX035 | Codex | Warning | DungeonGenerator: two tuning values of a dungeon share a name
CODEX036 | Codex | Warning | DungeonGenerator: a dungeon constant's name is a member of the class or a base class
CODEX037 | Codex | Warning | DungeonGenerator: a dungeon placement names no creature record
