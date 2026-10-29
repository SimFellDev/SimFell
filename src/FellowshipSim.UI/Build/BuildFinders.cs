using System.Text.Json;
using FellowshipSim.Core.Customization.Gems;
using FellowshipSim.Core.Import;

namespace FellowshipSim.UI.Build;

/// <summary>What a finder changes in the build, one variation per run.</summary>
public enum FinderKind { Aspects, Sigils, Glyphs, Relics, Gems, Talents }

/// <summary>
/// How far a rank finder takes each choice: one rank higher, or to its highest rank; for gems, the next node, or each node
/// above the power as a run of its own (<see cref="EveryRank"/>).
/// </summary>
public enum FinderRanks { NextRank, MaxRank, EveryRank }

/// <summary>One variation of a build: what changed (<see cref="Label"/>, then <see cref="Detail"/>) and the build.</summary>
public sealed record BuildVariant(string Label, string Detail, CharacterBuild Build);

/// <summary>The variations of a build a finder sims, each against the build as it is.</summary>
public static class BuildFinders
{
    /// <summary>
    /// The variations of <paramref name="build"/> for <paramref name="kind"/>: each aspect, sigil or glyph one rank higher
    /// or at its highest (<paramref name="ranks"/>); every pair of relics; each gem colour with the power of its next
    /// node, or of each node above its power; or each talent not taken that fits the points left.
    /// </summary>
    public static IReadOnlyList<BuildVariant> Variants(CharacterBuild build, FinderKind kind, FinderRanks ranks)
    {
        var catalog = BuildCatalog.For(build.Hero);
        return kind switch
        {
            FinderKind.Aspects => Ranked(build, catalog.Aspects, ranks, (e, o) => e.AugmentationRank(o.Id), (e, o, r) => e.SetAugmentationRank(o, r)),
            FinderKind.Sigils => Ranked(build, catalog.Sigils, ranks, (e, o) => e.AugmentationRank(o.Id), (e, o, r) => e.SetAugmentationRank(o, r)),
            FinderKind.Glyphs => Ranked(build, catalog.Glyphs, ranks, (e, o) => e.GlyphRank(o.Id), (e, o, r) => e.SetGlyphRank(o, r)),
            FinderKind.Relics => Relics(build, catalog),
            FinderKind.Gems => Gems(build, ranks),
            _ => Talents(build, catalog),
        };
    }

    /// <summary>How many runs <see cref="Variants"/> makes, besides the build as it is.</summary>
    public static int Count(CharacterBuild build, FinderKind kind, FinderRanks ranks) => Variants(build, kind, ranks).Count;

    /// <summary>A deep copy of <paramref name="build"/>.</summary>
    public static CharacterBuild Copy(CharacterBuild build) =>
        JsonSerializer.Deserialize<CharacterBuild>(JsonSerializer.Serialize(build))!;

    private static List<BuildVariant> Ranked(CharacterBuild build, IReadOnlyList<BuildOption> options, FinderRanks ranks,
        Func<BuildEditor, BuildOption, int> rankOf, Action<BuildEditor, BuildOption, int> setRank)
    {
        var variants = new List<BuildVariant>();
        var current = new BuildEditor(build);
        foreach (var option in options.Where(o => o.Modeled))
        {
            int rank = rankOf(current, option);
            if (rank >= option.MaxRank) continue;
            int next = ranks == FinderRanks.MaxRank ? option.MaxRank : rank + 1;
            var editor = new BuildEditor(Copy(build));
            setRank(editor, option, next);
            variants.Add(new BuildVariant(option.Name, $"{rank} → {next}", editor.Build));
        }
        return variants;
    }

    private static List<BuildVariant> Relics(CharacterBuild build, BuildCatalog catalog)
    {
        var variants = new List<BuildVariant>();
        var worn = new[] { build.Relic1ItemId, build.Relic2ItemId }.Where(id => id != 0).Order().ToArray();
        var relics = catalog.Relics;
        for (int i = 0; i < relics.Count; i++)
        {
            for (int j = i + 1; j < relics.Count; j++)
            {
                var pair = new[] { relics[i].ItemId, relics[j].ItemId }.Order().ToArray();
                if (pair.SequenceEqual(worn)) continue;
                var editor = new BuildEditor(Copy(build));
                editor.Relic1 = null;
                editor.Relic2 = null;
                editor.Relic1 = relics[i].ItemId;
                editor.Relic2 = relics[j].ItemId;
                variants.Add(new BuildVariant($"{relics[i].Name} + {relics[j].Name}", $"{relics[i].PassiveName}, {relics[j].PassiveName}", editor.Build));
            }
        }
        return variants;
    }

    private static List<BuildVariant> Gems(CharacterBuild build, FinderRanks ranks)
    {
        // The power a sim gives each colour: the build's, times the relics' multiplier.
        var hero = HeroImporter.Import(build);
        double multiplier = hero.RelicPassives.Aggregate(1.0, (m, r) => m * r.GemPowerMultiplier);
        var variants = new List<BuildVariant>();
        foreach (var color in BuildCatalog.GemColors)
        {
            double power = build.GemPower.GetValueOrDefault(color);
            double effective = power * multiplier;
            var above = Gem.Create(color).Tree.Where(n => n.RequiredPower > effective).OrderBy(n => n.RequiredPower).ToList();
            foreach (var node in ranks == FinderRanks.EveryRank ? above : above.Take(1))
            {
                var copy = Copy(build);
                double needed = Math.Ceiling(node.RequiredPower / multiplier);
                copy.GemPower[color] = needed;
                variants.Add(new BuildVariant($"{color}: {node.Name}", $"{power:0} → {needed:0} power", copy));
            }
        }
        return variants;
    }

    private static List<BuildVariant> Talents(CharacterBuild build, BuildCatalog catalog)
    {
        var variants = new List<BuildVariant>();
        var current = new BuildEditor(build);
        foreach (var talent in catalog.Talents.Where(t => t.Talent.Modeled && !current.HasTalent(t.Slot.TalentId)))
        {
            if (talent.Slot.Cost > current.PointsLeft) continue;
            var editor = new BuildEditor(Copy(build));
            if (!editor.ToggleTalent(talent)) continue;
            variants.Add(new BuildVariant(talent.Talent.Name, $"{talent.Slot.Code}, {talent.Slot.Cost} {(talent.Slot.Cost == 1 ? "point" : "points")}", editor.Build));
        }
        return variants;
    }
}
