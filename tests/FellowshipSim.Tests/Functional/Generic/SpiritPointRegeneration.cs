using FellowshipSim.Core.Domain;
using FellowshipSim.Core.Engine;
namespace FellowshipSim.Tests.Functional;

public class SpiritPointRegeneration
{
    public static TheoryData<string> Heroes => new() { "Rime", "Ardeos", "Mara", "Gunde", "Tariq", "Elarion" };

    private static StepDriver Fight(string hero, double spirit) => hero switch
    {
        "Rime" => StepFights.Rime(spirit: spirit),
        "Ardeos" => StepFights.Ardeos(spirit: spirit),
        "Mara" => StepFights.Mara(spirit: spirit),
        "Gunde" => StepFights.Gunde(spirit: spirit),
        "Tariq" => StepFights.Tariq(spirit: spirit),
        _ => StepFights.Elarion(spirit: spirit),
    };

    /// <summary>
    /// Player: every hero regenerates 1 Spirit Point every 3 seconds, whatever their Spirit: the stat raises Spirit Refund, not this.
    /// Dev: starts from 50 Spirit Points; at 0%, 10%, 25% and 35% Spirit, 12 seconds pass; always 54.
    /// </summary>
    [Theory]
    [MemberData(nameof(Heroes))]
    public void Spirit_Points_Regenerate_One_Per_Three_Seconds_Whatever_The_Spirit(string hero)
    {
        foreach (double spirit in new[] { 0.0, 0.1, 0.25, 0.35 })
        {
            var f = Fight(hero, spirit);
            f.Hero.Resources.Get(Hero.SpiritPoints).Current = 50;

            f.Wait(12);

            Assert.Equal(54.0, f.Hero.ResourceAmount(Hero.SpiritPoints), 1e-3);
        }
    }
}
