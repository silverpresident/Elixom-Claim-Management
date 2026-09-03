using ElixomClaim.Lib.Entities;
using ElixomClaim.Lib.Services;

namespace ElixomClaim.Lib.Tests.Services;

public class SalaryRecurrencePlannerTests
{
    private readonly SalaryRecurrencePlanner _planner = new();

    [Fact]
    public void Plan_UsesMonthEndAndNearestWeekday()
    {
        var definition = Definition(lastSalaryDate: new DateOnly(2025, 1, 31), recurrenceMonths: 1, weekday: DayOfWeek.Friday);

        var plan = _planner.Plan(definition, new DateOnly(2025, 2, 28), []);

        Assert.Equal(new DateOnly(2025, 2, 28), plan.DueDate);
        Assert.Equal(SalaryGenerationEligibility.Eligible, plan.Eligibility);
    }

    [Fact]
    public void Plan_HandlesLeapYearMonthEnd()
    {
        var definition = Definition(lastSalaryDate: new DateOnly(2024, 1, 31), recurrenceMonths: 1, weekday: DayOfWeek.Thursday);

        var plan = _planner.Plan(definition, new DateOnly(2024, 2, 29), []);

        Assert.Equal(new DateOnly(2024, 2, 29), plan.DueDate);
        Assert.True(plan.CanGenerate);
    }

    [Fact]
    public void Plan_SelectsTheClosestConfiguredWeekday()
    {
        var definition = Definition(lastSalaryDate: new DateOnly(2025, 1, 1), recurrenceDays: 3, weekday: DayOfWeek.Monday);

        var plan = _planner.Plan(definition, new DateOnly(2025, 1, 6), []);

        Assert.Equal(new DateOnly(2025, 1, 6), plan.DueDate);
        Assert.Equal("Earlier occurrence", SalaryRecurrencePlanner.NearestWeekdayTieBreak);
    }

    [Theory]
    [InlineData("2025-01-03", "2025-01-03", SalaryGenerationEligibility.Eligible)]
    [InlineData("2025-01-02", "2025-01-03", SalaryGenerationEligibility.AfterEndDate)]
    public void Plan_TreatsEffectiveBoundsAsInclusive(string endDate, string dueDate, SalaryGenerationEligibility expected)
    {
        var definition = Definition(lastSalaryDate: new DateOnly(2025, 1, 2), recurrenceDays: 1, weekday: DayOfWeek.Friday, endDate: DateOnly.Parse(endDate));

        var plan = _planner.Plan(definition, DateOnly.Parse(dueDate), []);

        Assert.Equal(expected, plan.Eligibility);
    }

    [Fact]
    public void Plan_TreatsStartDateAsInclusive()
    {
        var definition = Definition(lastSalaryDate: new DateOnly(2025, 1, 2), recurrenceDays: 1, weekday: DayOfWeek.Friday, startDate: new DateOnly(2025, 1, 3));

        var plan = _planner.Plan(definition, new DateOnly(2025, 1, 3), []);

        Assert.Equal(SalaryGenerationEligibility.Eligible, plan.Eligibility);
    }

    [Fact]
    public void Plan_RejectsInactiveDefinitionAndExistingDuePeriod()
    {
        var inactive = Definition(lastSalaryDate: new DateOnly(2025, 1, 2), recurrenceDays: 1, weekday: DayOfWeek.Friday, isActive: false);
        var active = Definition(lastSalaryDate: new DateOnly(2025, 1, 2), recurrenceDays: 1, weekday: DayOfWeek.Friday);

        Assert.Equal(SalaryGenerationEligibility.Inactive, _planner.Plan(inactive, new DateOnly(2025, 1, 3), []).Eligibility);
        Assert.Equal(SalaryGenerationEligibility.AlreadyGenerated, _planner.Plan(active, new DateOnly(2025, 1, 3), [new DateOnly(2025, 1, 3)]).Eligibility);
    }

    private static SalaryDefinition Definition(DateOnly lastSalaryDate, int recurrenceDays = 0, int recurrenceMonths = 0, DayOfWeek weekday = DayOfWeek.Friday, DateOnly? endDate = null, bool isActive = true, DateOnly? startDate = null) => new()
    {
        LastSalaryDate = lastSalaryDate,
        FirstSalaryDate = lastSalaryDate,
        StartDate = startDate ?? new DateOnly(2024, 1, 1),
        EndDate = endDate,
        RecurrenceDays = recurrenceDays,
        RecurrenceMonths = recurrenceMonths,
        NearestWeekday = weekday,
        IsActive = isActive
    };
}
