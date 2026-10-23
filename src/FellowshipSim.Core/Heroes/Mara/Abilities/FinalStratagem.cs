namespace FellowshipSim.Core.Heroes.Mara.Abilities;

/// <summary>
/// Ability 1468: "Your Energy and Combo Points are instantly refilled and all your Hero Ability cooldowns are reset. Can
/// be used during Global Cooldown." 180s cooldown. Macabre Stratagem replaces it (<see cref="MacabreStratagem"/>).
/// UNVERIFIED: no combat log has it; the reset covers every ability of Mara's but this one and the Spirit ability.
/// </summary>
public class FinalStratagem : MaraAbility
{
    public const int AbilityId = 1468;
    public const string AbilityName = "Final Stratagem";
    public const double CooldownSeconds = 180.0;

    public FinalStratagem(Mara owner) : base(owner, AbilityId, AbilityName, AbilityCategory.Major)
    {
        SetCooldown(CooldownSeconds);
        IgnoresGcd = true;
    }

    protected override bool CanCast(SimulationContext ctx, bool explain, out string? reason)
    {
        if (Mara.HasMacabreStratagem)
        {
            reason = explain ? $"{Name} is replaced by Macabre Stratagem" : null;
            return false;
        }
        return base.CanCast(ctx, explain, out reason);
    }

    public override void Execute(SimulationContext ctx, EnemyTarget target)
    {
        var hero = Mara;
        TriggerCooldown(ctx);
        hero.GainEnergy(Mara.MaxEnergy, ctx);
        hero.GainComboPoints(Mara.MaxComboPoints, ctx);
        foreach (var ability in hero.Abilities)
        {
            if (ability == this || ability.Category is AbilityCategory.Spirit or AbilityCategory.Weapon || ability is Attack) continue;
            if (hero.Cooldowns.TryGetValue(ability.Name, out var state)) state.ResetAll();
        }
    }
}
