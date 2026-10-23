namespace FellowshipSim.Core.Heroes.Mara.Talents;

/// <summary>
/// Talent Macabre Stratagem: "Your Final Stratagem is replaced by Macabre Stratagem. Upon use, Macabre Stratagem grants
/// you 10 seconds of Matriarch Macabre. If used while Matriarch Macabre is already active, its duration is extended by
/// 10 seconds." The game data names it talent 8; combat logs name it talent 2010000 (talent 10000), and every log with
/// it casts Macabre Stratagem (ability 2233; FellowshipAnalyzer Mara corpus, 43 logs).
/// </summary>
public class MacabreStratagemTalent : Talent
{
    public const int TalentId = 10000;
    public const string TalentName = "Macabre Stratagem";

    public override string Name => TalentName;
    public override int Id => TalentId;

    public override void Bind(Hero hero) => ((Mara)hero).HasMacabreStratagem = true;
}
