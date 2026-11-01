using FellowshipSim.Core.Heroes.Xavian.Abilities;

namespace FellowshipSim.Core.Heroes.Xavian.Talents;

/// <summary>
/// Talent 583: "Your Brilliant Flare and Brilliant Flash has a 15% chance to grant you Sun's Verdict for 12 seconds. Sun's
/// Verdict: Your next Sun Strike resets the cooldown of your Blinding Slash ability and causes your target and all enemies
/// near it to be hit by Sun Beams from above, dealing 124% Strength magic damage to each of them." Effect 2648 (12s, 2
/// stacks); the beams are effect 2647, targetCountFalloff threshold 8 (ability 1845). The chance is the game data constant
/// Sune.SingleTargetFillerMelee.Talent.AoeProcBuff.Proc.ProcChance 0.2, not the tooltip's 15%. UNVERIFIED: no combat log
/// has this talent.
/// </summary>
public class SunsVerdict : Talent
{
    public const int TalentId = 583;
    public const string TalentName = "Sun's Verdict";
    public const double Chance = 0.2;
    public const string ProcKey = "suns_verdict";
    public const int BuffEffectId = 2648;
    public const double BuffSeconds = 12.0;
    public const int BuffMaxStacks = 2;
    public const int BeamEffectId = 2647;
    public const double BeamCoefficient = 1.24;
    public const double BeamTargetThreshold = 8;

    public static readonly AuraDef Buff = AuraDef.Effect(TalentName, BuffEffectId, BuffSeconds, BuffMaxStacks);

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var xavian = (Xavian)hero;
        var beams = new SunBeams(xavian);
        void Roll(SimulationContext ctx)
        {
            if (ctx.Outcomes.RollProc(ProcKey, Chance, ctx)) hero.Auras.Apply(Buff, ctx.CurrentTime);
        }
        xavian.BrilliantFlash.Flashed += (ctx, _) => Roll(ctx);
        xavian.GetAbility<BrilliantFlare>()!.Flared += Roll;
        xavian.GetAbility<SunStrike>()!.Striking += (ctx, target) =>
        {
            if (!hero.Auras.HasAura(Buff, ctx.CurrentTime)) return;
            hero.Auras.ConsumeStack(Buff, ctx.CurrentTime);
            hero.CooldownOf(hero.GetAbility<BlindingSlash>()!).ResetAll();
            beams.Strike(ctx, target);
        };
    }

    /// <summary>The Sun Beams' damage.</summary>
    private sealed class SunBeams : Ability
    {
        public SunBeams(Xavian owner) : base(owner, LogIds.Effect(BeamEffectId), "Sun Beams")
        {
            BaseCoefficient = BeamCoefficient;
            AoeTargetThreshold = BeamTargetThreshold;
        }

        public void Strike(SimulationContext ctx, EnemyTarget target)
        {
            var enemies = Abilities.XavianAbility.Area(ctx, target);
            DealAoeDamage(ctx, enemies);
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
