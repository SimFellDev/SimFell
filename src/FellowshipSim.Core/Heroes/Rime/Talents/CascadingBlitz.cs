using FellowshipSim.Core.Heroes.Rime.Abilities;

namespace FellowshipSim.Core.Heroes.Rime.Talents;

/// <summary>Talent 192: each Anima generated during Ice Blitz conjures a Frost Swallow.</summary>
public class CascadingBlitz : Talent
{
    public const int TalentId = 192;
    public const string TalentName = "Cascading Blitz";

    public override string Name => TalentName;
    public override int Id => TalentId;

    /// <summary>Tooltip: you conjure a Frost Swallow to attack your target (a replay follows the log if the player briefly had another target).</summary>
    public override void Bind(Hero hero)
    {
        var swallows = hero.GetAbility<FrostSwallows>();
        if (swallows == null) return;
        // Anima generated during Ice Blitz sends swallows, preferring the enemy the Anima came from.
        hero.OnResourceGained += (resource, amount, from, ctx) =>
        {
            if (resource != Rime.Anima || from == null || !hero.Auras.HasAura(IceBlitz.Buff, ctx.CurrentTime)) return;
            for (int i = 0; i < (int)amount; i++)
            {
                var enemy = ctx.Outcomes.ChooseTarget(swallows.DamageLogId, ctx.ActiveEnemies, ctx, preferred: from);
                if (enemy != null) swallows.Attack(ctx, enemy, 1);
            }
        };
    }
}
