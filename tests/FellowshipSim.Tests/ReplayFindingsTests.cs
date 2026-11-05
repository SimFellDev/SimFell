using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

public class ReplayFindingsTests
{
    private const int Bolt = 1_000_001;
    private const int Blast = 1_000_002;

    private static readonly IssueEntity BoltEntity = IssueEntity.Damage(Bolt, "Bolt");

    private static LogDamage Log(int id, double amount, double time) =>
        new() { Time = time, AbilityId = id, AbilityName = id == Bolt ? "Bolt" : "Blast", Amount = amount };

    private static HitPair Matched(ReplayResult r, int id, double engine, double log, double time, double tickFraction = 1.0, EngineState? state = null)
    {
        var hit = Log(id, log, time);
        var pair = new HitPair(time, hit.AbilityName, id, null, engine, hit, false, 0.0, 1.0)
        {
            ExpectedLow = engine * 0.9,
            ExpectedHigh = engine * 1.1,
            LandedTime = time,
            TickFraction = tickFraction,
            State = state,
        };
        r.LogDamage.Add(hit);
        r.AllHits.Add(pair);
        return pair;
    }

    private static ReplayIssue Issue(IssueEntity entity, IssueCategory category, double time, int step, string subject, double? observed = null, ExpectedRange? expected = null) =>
        new(time, step, category, entity, $"{subject} at {time}", subject) { Observed = observed, Expected = expected };

    private static EngineState WithHeroAura(string? aura)
    {
        var hero = new EngineHero(0, 0, 0, 2, 0, 0, 0, 1, [], 0, null, null,
            aura == null ? [] : [new EngineAura(aura, 2_000_001, 1, null, [])], 1);
        return new EngineState(0, hero, null, null, null);
    }

    [Fact]
    public void Each_Issue_Of_One_Entity_Falls_In_One_Finding()
    {
        var r = new ReplayResult();
        Matched(r, Bolt, 100, 100, 1);
        r.Issues.Add(Issue(BoltEntity, IssueCategory.Damage, 1, 1, "damage too high: Bolt (1000001)", 80, new ExpectedRange(90, 110, "damage")));
        r.Issues.Add(Issue(BoltEntity, IssueCategory.Hits, 2, 2, "extra engine hit: Bolt (1000001)"));
        r.Issues.Add(Issue(BoltEntity with { Name = "Bolt (tick)" }, IssueCategory.Ticks, 3, 3, "tick time: Bolt (tick)", 3.1, new ExpectedRange(2.967, 3.033, "s")));
        r.Issues.Add(Issue(IssueEntity.Resource("Mana"), IssueCategory.Resources, 4, 4, "Mana too low", 12, new ExpectedRange(9, 11, "Mana")));

        var findings = ReplayFindings.From(r);

        Assert.Equal(r.Issues.Count, findings.Sum(f => f.Issues.Count));
        Assert.Equal(3, Assert.Single(findings, f => f.Entity.Kind == IssueEntityKind.DamageId).Issues.Count);
    }

    [Fact]
    public void An_Issue_On_A_Damage_Id_With_No_Hits_Keeps_Its_Finding()
    {
        var r = new ReplayResult();
        r.Issues.Add(Issue(IssueEntity.Damage(Blast, Blast.ToString()), IssueCategory.Hits, 1, 1, "extra engine multi-target hit: 1000002"));

        var finding = Assert.Single(ReplayFindings.From(r));

        Assert.Single(finding.Issues);
    }

    [Fact]
    public void Calibration_Ranks_By_Log_Damage_Times_The_Ratio_Offset()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 40; i++)
        {
            // Bolt: half the damage 5% off. Blast: the other half 10% off.
            Matched(r, Bolt, 100, 105 + i % 2, i);
            Matched(r, Blast, 100, 110 + i % 2, i + 0.5);
        }

        var calibration = ReplayFindings.From(r).Where(f => f.Group == FindingGroup.Calibration).ToList();

        Assert.Equal([Blast, Bolt], calibration.Select(f => f.Entity.Id));
        Assert.Equal(0.05, calibration[0].Impact!.Value, 2);
    }

    [Fact]
    public void A_Damage_Id_Inside_Its_Targets_Has_No_Finding_Without_Issues()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 40; i++) Matched(r, Bolt, 100, 99 + i % 3, i);

        Assert.Empty(ReplayFindings.From(r));
    }

    [Fact]
    public void A_Condition_That_Divides_The_Ratio_Is_Shown()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 100; i++)
        {
            bool partial = i % 10 == 0;
            Matched(r, Bolt, 100, (partial ? 250 : 100) + i % 3, i, partial ? 0.4 : 1.0);
        }

        var split = Assert.Single(Assert.Single(ReplayFindings.From(r)).Splits);

        Assert.Equal("Partial tick", split.Condition);
        Assert.Equal(10, split.HitsWith);
        Assert.Equal(2.5, split.RatioWith, 1);
        Assert.Equal(1.0, split.RatioWithout, 1);
    }

    [Fact]
    public void A_Condition_Within_The_Spread_Is_Hidden()
    {
        var r = new ReplayResult();
        // Alternating +/-10% spread, with an aura up on every third hit, and a mean 5% off for a finding.
        for (int i = 0; i < 120; i++) Matched(r, Bolt, 100, i % 2 == 0 ? 115 : 95, i, state: WithHeroAura(i % 3 == 0 ? "Focus" : null));

        var finding = Assert.Single(ReplayFindings.From(r));

        Assert.Empty(finding.Splits);
    }

    [Fact]
    public void An_Aura_Dividing_The_Ratio_Names_The_Aura()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 120; i++)
        {
            bool up = i % 2 == 0;
            Matched(r, Bolt, 100, (up ? 100 : 90) + i % 3, i, state: WithHeroAura(up ? "Focus" : null));
        }

        var split = Assert.Single(Assert.Single(ReplayFindings.From(r)).Splits);

        Assert.Equal("Focus (2000001) on the hero", split.Condition);
        Assert.True(split.RatioWith > split.RatioWithout);
    }

    [Fact]
    public void Amounts_Swapped_Inside_A_Release_Are_Replay_Pairing()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 30; i++)
        {
            Matched(r, Bolt, 1000, 300, i);
            Matched(r, Bolt, 300, 1000, i);
        }

        var finding = Assert.Single(ReplayFindings.From(r));

        Assert.Equal(FindingGroup.ReplayPairing, finding.Group);
        Assert.Contains(finding.Facts, f => f.StartsWith("60 of the 60 hits out of range fit another engine hit"));
        Assert.Empty(finding.Splits);
        Assert.Equal(0.0, finding.Impact!.Value, 6);
    }

    [Fact]
    public void A_Multiplier_On_One_Target_Is_Not_Replay_Pairing_When_Its_Amount_Fits_Another_Target()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 30; i++)
        {
            // The second target takes 1.3 times the engine's amount, which lands on the first target's amount.
            Matched(r, Bolt, 1000, 1000, i);
            Matched(r, Bolt, 770, 1001, i);
        }

        Assert.Equal(FindingGroup.Calibration, Assert.Single(ReplayFindings.From(r)).Group);
    }

    [Fact]
    public void A_Multiplier_In_A_Few_Releases_Is_Not_Replay_Pairing()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 90; i++)
        {
            // Every tenth release takes 1.21 times the engine's amounts, close to its other hit's amount.
            double factor = i % 10 == 0 ? 1.21 : 1.0;
            Matched(r, Bolt, 2041, 2041 * factor, i);
            Matched(r, Bolt, 1879, 1879 * factor, i);
        }

        Assert.NotEqual(FindingGroup.ReplayPairing, Assert.Single(ReplayFindings.From(r)).Group);
    }

    [Fact]
    public void Damage_Wrong_By_One_Factor_Is_Not_Replay_Pairing()
    {
        var r = new ReplayResult();
        for (int i = 0; i < 30; i++)
        {
            Matched(r, Bolt, 1000, 700, i);
            Matched(r, Bolt, 300, 210, i);
        }

        Assert.Equal(FindingGroup.Calibration, Assert.Single(ReplayFindings.From(r)).Group);
    }

    [Fact]
    public void A_Resource_Has_Net_And_Gross_And_Cancelling_Steps()
    {
        var r = new ReplayResult();
        foreach (int index in Enumerable.Range(1, 4)) r.Steps.Add(new ReplayStep { Index = index, AbilityName = $"Cast{index}" });
        var mana = IssueEntity.Resource("Mana");
        var range = new ExpectedRange(9, 11, "Mana");
        r.Issues.Add(Issue(mana, IssueCategory.Resources, 1, 2, "Mana too low", 15, range));
        r.Issues.Add(Issue(mana, IssueCategory.Resources, 2, 3, "Mana too high", 5, range));
        r.Issues.Add(Issue(mana, IssueCategory.Resources, 3, 4, "Mana too low", 13, range));

        var finding = Assert.Single(ReplayFindings.From(r));

        Assert.Equal(FindingGroup.Resources, finding.Group);
        Assert.Contains("net +3, gross 13", finding.Headline);
        Assert.Contains(finding.Facts, f => f.StartsWith("2 of them cancel"));
        Assert.Contains(finding.Facts, f => f.Contains("Cast3 +3 (1 steps)"));
    }

    [Fact]
    public void Ticks_Report_Their_Median_Offset_And_Share_Late()
    {
        var r = new ReplayResult();
        Matched(r, Bolt, 100, 100, 1);
        foreach (double late in new[] { 0.05, 0.06, 0.07 })
        {
            r.Issues.Add(Issue(BoltEntity, IssueCategory.Ticks, 2, 2, "tick time: Bolt", 2 + late, new ExpectedRange(1.967, 2.033, "s")));
        }

        var finding = Assert.Single(ReplayFindings.From(r));

        Assert.Equal(FindingGroup.Ticks, finding.Group);
        Assert.Contains(finding.Facts, f => f.Contains("median offset +60ms, 100% of them late"));
    }

    [Fact]
    public void Examples_Are_The_First_Median_And_Worst_Of_Each_Kind()
    {
        var r = new ReplayResult();
        Matched(r, Bolt, 100, 100, 0);
        var range = new ExpectedRange(90, 110, "damage");
        for (int i = 0; i < 100; i++)
        {
            r.Issues.Add(Issue(BoltEntity, IssueCategory.Damage, i, i, "damage too low: Bolt (1000001)", 111 + i, range));
        }

        var examples = Assert.Single(ReplayFindings.From(r)).Examples;

        Assert.Equal([0.0, 50.0, 99.0], examples.Select(e => e.Time));
    }

    [Fact]
    public void The_Page_Links_Only_Steps_It_Renders()
    {
        var r = new ReplayResult();
        foreach (int index in Enumerable.Range(1, 40)) r.Steps.Add(new ReplayStep { Index = index, AbilityName = "Cast" });
        var range = new ExpectedRange(9, 11, "Mana");
        foreach (var step in r.Steps)
        {
            var issue = Issue(IssueEntity.Resource("Mana"), IssueCategory.Resources, step.Index, step.Index, "Mana too low", 12 + step.Index, range);
            r.Issues.Add(issue);
            step.Issues.Add(issue);
        }

        string html = ReplayReportWriter.Render(r);

        Assert.Contains("<h2>Findings (1)</h2>", html);
        Assert.Contains("id=\"step-1\"", html);
        Assert.DoesNotContain("id=\"step-2\"", html);
        Assert.DoesNotContain("href=\"#step-2\"", html);
    }
}
