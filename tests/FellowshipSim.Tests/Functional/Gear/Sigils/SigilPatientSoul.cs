using FellowshipSim.Core.Customization.Sigils;

namespace FellowshipSim.Tests.Functional;

/// <summary>
/// Patient Soul (sigil 46): "Standing still for 3 seconds grants you Patient Soul, increasing your Max Health by 5% and your Expertise
/// rating by 12 / 22 / 32 / 42. Patient Soul persists for 6 seconds after you start moving" (ranks 1 to 4). A rating point is 0.16%.
///
/// The sim has no movement: the hero always stands still and has Patient Soul.
/// </summary>
public class SigilPatientSoul
{
    /// <summary>Patient Soul gives 12 / 22 / 32 / 42 Expertise rating, for as long as you stand still.</summary>
    [Theory]
    [InlineData("Rime", 1, 12)]
    [InlineData("Rime", 2, 22)]
    [InlineData("Rime", 3, 32)]
    [InlineData("Rime", 4, 42)]
    [InlineData("Ardeos", 4, 42)]
    [InlineData("Elarion", 4, 42)]
    [InlineData("Gunde", 4, 42)]
    [InlineData("Mara", 4, 42)]
    [InlineData("Tariq", 4, 42)]
    public void Patient_Soul_Gives_Expertise_Rating_While_You_Stand_Still(string hero, int rank, double rating)
    {
        var f = StepFights.Of(hero, augmentations: [new(PatientSoul.SigilId, rank)]);

        // sim does not model movement yet

        using var check = new Checks(f);
        check.True(f.Has(PatientSoul.Buff));
        check.Equal(rating * 0.0016, f.Expertise());
    }

    /// <summary>
    /// Patient Soul gives you 5% more health, for as long as you stand still.
    ///
    /// Your health counts in the sim through Ruby Storm's damage.
    /// </summary>
    [Fact]
    public void Patient_Soul_Gives_5_Percent_Health()
    {
        var plainScenario = StepFights.Of("Rime");
        var patientScenario = StepFights.Of("Rime", augmentations: [new(PatientSoul.SigilId, 1)]);

        plainScenario.Wait(60); // standing still
        patientScenario.Wait(60); // standing still

        AssertRatio(1.05, patientScenario.Health() / plainScenario.Health());
    }
}
