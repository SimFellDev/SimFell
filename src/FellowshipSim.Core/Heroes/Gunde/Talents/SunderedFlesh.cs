namespace FellowshipSim.Core.Heroes.Gunde.Talents;

/// <summary>
/// Talent 765: "When your Rend or Slaughter contains more damage than an enemy has left in remaining health, they
/// instantly take that damage." The Rend's damage still to deal is taken as one hit, logged as Rend. UNVERIFIED: no combat
/// log has this talent; Slaughter is not checked.
/// </summary>
public class SunderedFlesh : Talent
{
    public const int TalentId = 765;
    public const string TalentName = "Sundered Flesh";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero)
    {
        var gunde = (Gunde)hero;
        gunde.SunderedFleshCheck = (ctx, target) =>
        {
            if (target.MaxHealth <= 0 || target.IsSleeping) return;
            double left = gunde.Rend.Remaining(target);
            if (left <= 0.0 || left < target.CurrentHealth) return;
            double damage = gunde.Rend.Consume(ctx, target);
            ctx.ApplyDamage(gunde, target, damage, Rend.DamageId, Rend.DamageName, canCrit: false, 0.0, 1.0);
        };
    }
}
