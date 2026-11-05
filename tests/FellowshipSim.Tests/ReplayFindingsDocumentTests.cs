using System.Text.Json;
using FellowshipSim.Engine.Replay;

namespace FellowshipSim.Tests;

public class ReplayFindingsDocumentTests
{
    private const int Bolt = 1_000_001;
    private static readonly IssueEntity BoltEntity = IssueEntity.Damage(Bolt, "Bolt");
    private static readonly IssueEntity Mana = IssueEntity.Resource("Mana");

    private static ReviewedFight Fight(ReplayResult result, int fightId = 1) =>
        new(result, new ReplayScript { ReportCode = "code", FightId = fightId, FightName = "Fight", Player = new ReplayPlayer { Hero = "Rime" } }, 7);

    private static void Hit(ReplayResult r, double engine, double log, double time)
    {
        var hit = new LogDamage { Time = time, AbilityId = Bolt, AbilityName = "Bolt", Amount = log };
        r.LogDamage.Add(hit);
        r.AllHits.Add(new HitPair(time, "Bolt", Bolt, null, engine, hit, false, 0.0, 1.0)
        {
            ExpectedLow = engine * 0.9,
            ExpectedHigh = engine * 1.1,
            LandedTime = time,
            State = new EngineState(time, new EngineHero(0, 0, 0, 2, 0, 0, 0, 1, [], 0, null, null, [new EngineAura("Focus", 2_000_001, 2, 3.5, [])], 1), null, null, null),
        });
    }

    private static ReplayResult Calibrated(double log)
    {
        var r = new ReplayResult();
        for (int i = 0; i < 40; i++) Hit(r, 100, log + i % 2, i);
        return r;
    }

    private static ReplayIssue Damage(double time, double observed) =>
        new(time, 1, IssueCategory.Damage, BoltEntity, "damage too low", "damage too low: Bolt (1000001)")
        {
            Observed = observed,
            Expected = new ExpectedRange(90, 110, "damage"),
            State = new EngineState(time, new EngineHero(0, 0, 0, 2, 0, 0, 0, 1, [], 0, null, null, [], 1), null, null, null),
        };

    private static JsonElement Finding(JsonDocument document, string key) =>
        document.RootElement.GetProperty("findings").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key);

    [Fact]
    public void A_Fights_Document_Holds_Every_Issue_Once()
    {
        var r = Calibrated(110);
        r.Issues.Add(Damage(1, 80));
        r.Issues.Add(new ReplayIssue(2, 1, IssueCategory.Resources, Mana, "Mana too low", "Mana too low") { Observed = 15, Expected = new ExpectedRange(9, 11, "Mana") });

        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForFight(Fight(r)));

        var root = document.RootElement;
        Assert.Equal(ReplayFindingsDocument.Schema, root.GetProperty("schema").GetInt32());
        Assert.Equal(r.Issues.Count, root.GetProperty("totals").GetProperty("issues").GetInt32());
        Assert.Equal(r.Issues.Count, root.GetProperty("findings").EnumerateArray().Sum(f => f.GetProperty("issues").GetInt32()));
    }

    [Fact]
    public void A_Fights_Document_Has_The_Evidence_And_One_State_For_Each_Kind_Of_Issue()
    {
        var r = Calibrated(110);
        for (int i = 0; i < 6; i++) r.Issues.Add(Damage(i, 80 - i));

        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForFight(Fight(r)));

        var finding = Finding(document, "DamageId:1000001");
        Assert.Equal("Calibration", finding.GetProperty("group").GetString());
        Assert.Single(finding.GetProperty("examples").EnumerateArray(), e => e.TryGetProperty("state", out _));
        var sample = finding.GetProperty("sampleHits")[0];
        Assert.Equal("Focus (2000001) x2 3.5s", sample.GetProperty("state").GetProperty("hero").GetProperty("auras")[0].GetString());
        Assert.False(finding.TryGetProperty("byFight", out _));
    }

    [Fact]
    public void A_Heros_Document_Pools_The_Fights_And_Names_Each_Fights_Numbers()
    {
        var fights = new[] { Fight(Calibrated(110), 1), Fight(Calibrated(109), 2) };

        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForHero("Rime", "baseline", fights));

        var finding = Finding(document, "DamageId:1000001");
        Assert.Equal(80, finding.GetProperty("damage").GetProperty("normal").GetProperty("count").GetInt32());
        Assert.Equal(2, finding.GetProperty("fightsOff").GetInt32());
        Assert.Equal("code_fight1_p7", finding.GetProperty("byFight")[0].GetProperty("review").GetString());
        Assert.False(finding.TryGetProperty("examples", out _));
        Assert.Equal("data/fights/code_fight2_p7.raw.json", document.RootElement.GetProperty("fights")[1].GetProperty("files").GetProperty("rawFight").GetString());
    }

    [Fact]
    public void Releases_Of_Two_Fights_At_One_Time_Are_Two_Releases()
    {
        var first = new ReplayResult();
        var second = new ReplayResult();
        for (int i = 0; i < 40; i++)
        {
            Hit(first, 1000, 300, i);
            Hit(second, 300, 1000, i);
        }

        var finding = Assert.Single(ReplayFindings.From([first, second]));

        Assert.Equal(0, finding.Damage!.ReleaseFits);
        Assert.Equal(0, finding.Damage.Releases.Count);
        Assert.NotEqual(FindingGroup.ReplayPairing, finding.Group);
    }

    [Fact]
    public void Steps_Of_Two_Fights_Do_Not_Cancel()
    {
        var range = new ExpectedRange(9, 11, "Mana");
        var first = new ReplayResult();
        first.Issues.Add(new ReplayIssue(1, 4, IssueCategory.Resources, Mana, "Mana too low", "Mana too low") { Observed = 15, Expected = range });
        var second = new ReplayResult();
        second.Issues.Add(new ReplayIssue(2, 5, IssueCategory.Resources, Mana, "Mana too high", "Mana too high") { Observed = 5, Expected = range });

        Assert.Equal(0, Assert.Single(ReplayFindings.From([first, second])).Drift!.Cancelling);
    }

    [Fact]
    public void An_Infinite_Bound_Is_Left_Out()
    {
        var r = new ReplayResult();
        r.Issues.Add(new ReplayIssue(1, 1, IssueCategory.Auras, IssueEntity.Aura(5, "Focus"), "Focus too long", "Focus")
        {
            Observed = 9,
            Expected = new ExpectedRange(double.NegativeInfinity, 3, "s"),
        });

        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForFight(Fight(r)));

        var example = Finding(document, "Aura:5").GetProperty("examples")[0];
        Assert.False(example.TryGetProperty("expectedLow", out _));
        Assert.Equal(3, example.GetProperty("expectedHigh").GetDouble());
    }

    [Fact]
    public void The_Comparison_Names_Unmodeled_Content_And_Fights_That_Moved()
    {
        var before = Calibrated(110);
        before.UnmodeledAbilities["Blast"] = 3;
        before.UnmodeledCustomizations.Add("Old talent");
        string baseline = ReplayFindingsDocument.ForHero("Rime", "baseline", [Fight(before, 1), Fight(Calibrated(110), 2)]);

        var after = Calibrated(110);
        after.UnmodeledAbilities["Beam"] = 1;
        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForHero("Rime", "fix-01", [Fight(after, 1), Fight(Calibrated(110), 3)], "baseline", baseline));

        var comparison = document.RootElement.GetProperty("comparison");
        Assert.Equal(["Blast", "Old talent"], comparison.GetProperty("unmodeledResolved").EnumerateArray().Select(n => n.GetString()));
        Assert.Equal("Beam", Assert.Single(comparison.GetProperty("unmodeledAppeared").EnumerateArray()).GetString());
        Assert.Equal("code_fight3_p7", Assert.Single(comparison.GetProperty("fightsAdded").EnumerateArray()).GetString());
        Assert.Equal("code_fight2_p7", Assert.Single(comparison.GetProperty("fightsRemoved").EnumerateArray()).GetString());
    }

    [Fact]
    public void A_Small_Gain_With_Its_Issue_Count_Unchanged_Is_A_Change()
    {
        string baseline = ReplayFindingsDocument.ForHero("Rime", "baseline", [Fight(Calibrated(110), 1), Fight(Calibrated(110), 2)]);

        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForHero("Rime", "fix-01", [Fight(Calibrated(109.9), 1), Fight(Calibrated(109.9), 2)], "baseline", baseline));

        Assert.Single(document.RootElement.GetProperty("comparison").GetProperty("changed").EnumerateArray());
    }

    [Fact]
    public void An_Unchanged_Run_Has_Nothing_To_Compare()
    {
        var fights = new[] { Fight(Calibrated(110), 1), Fight(Calibrated(107), 2) };
        string baseline = ReplayFindingsDocument.ForHero("Rime", "baseline", fights);

        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForHero("Rime", "fix-01", fights, "baseline", baseline));

        var comparison = document.RootElement.GetProperty("comparison");
        Assert.Empty(comparison.GetProperty("changed").EnumerateArray());
        Assert.Empty(comparison.GetProperty("resolved").EnumerateArray());
        Assert.Empty(comparison.GetProperty("appeared").EnumerateArray());
    }

    [Fact]
    public void The_Comparison_Names_What_Moved_By_Key()
    {
        var before = Calibrated(110);
        before.Issues.Add(new ReplayIssue(1, 1, IssueCategory.Resources, Mana, "Mana too low", "Mana too low") { Observed = 15, Expected = new ExpectedRange(9, 11, "Mana") });
        string baseline = ReplayFindingsDocument.ForHero("Rime", "baseline", [Fight(before, 1), Fight(Calibrated(110), 2)]);

        var after = Calibrated(105);
        after.Issues.Add(new ReplayIssue(1, 1, IssueCategory.Resources, IssueEntity.Resource("Rage"), "Rage too low", "Rage too low") { Observed = 15, Expected = new ExpectedRange(9, 11, "Rage") });
        using var document = JsonDocument.Parse(ReplayFindingsDocument.ForHero("Rime", "fix-01", [Fight(after, 1), Fight(Calibrated(105), 2)], "baseline", baseline));

        var comparison = document.RootElement.GetProperty("comparison");
        Assert.Equal("baseline", comparison.GetProperty("against").GetString());
        Assert.Equal("Resource:Mana", Assert.Single(comparison.GetProperty("resolved").EnumerateArray()).GetProperty("key").GetString());
        Assert.Equal("Resource:Rage", Assert.Single(comparison.GetProperty("appeared").EnumerateArray()).GetProperty("key").GetString());
        var changed = Assert.Single(comparison.GetProperty("changed").EnumerateArray());
        Assert.Equal("DamageId:1000001", changed.GetProperty("key").GetString());
        Assert.True(changed.GetProperty("impactAfter").GetDouble() < changed.GetProperty("impactBefore").GetDouble());
        Assert.True(comparison.GetProperty("impactAfter").GetDouble() < comparison.GetProperty("impactBefore").GetDouble());
    }
}
