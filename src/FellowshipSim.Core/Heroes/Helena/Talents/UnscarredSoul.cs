namespace FellowshipSim.Core.Heroes.Helena.Talents;

/// <summary>
/// Talent 390: "The absorb shield from your Empowered Shield Slams takes 20% reduced damage &amp; enemies that attack you while
/// it is active take 100% Strength physical damage." The reflect is ability 1591, its hits effect 2340. An attack on Helena
/// is a defensive proc: a replay deals each reflect hit the log has. UNVERIFIED: no log has the talent.
/// </summary>
public partial class UnscarredSoul : Talent
{
    public const int ReflectEffectId = 2340;
    public const string ProcKey = "unscarred_soul";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        if (hero is not Helena helena) return;
        var reflect = new Reflect(helena);
        hero.AddDefensiveProc(ProcKey, (ctx, enemy) =>
        {
            if (enemy is { IsSleeping: false }) reflect.DealDamage(ctx, enemy);
        });
    }

    /// <summary>The reflect's hit: 100% of Strength.</summary>
    private sealed class Reflect : Ability
    {
        public Reflect(Helena owner) : base(owner, LogIds.Effect(ReflectEffectId), TalentName)
        {
            BaseCoefficient = StrengthCoefficient;
            DamageLogId = LogIds.Effect(ReflectEffectId);
        }

        public override void Execute(SimulationContext ctx, EnemyTarget target) { }
    }
}
