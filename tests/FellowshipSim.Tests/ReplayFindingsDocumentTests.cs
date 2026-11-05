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
        Assert.Equal("data/fights/code_fight1_p7.raw.json", document.RootElement.GetProperty("fight").GetProperty("files").GetProperty("rawFight").GetString());
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
}
