using ElixomClaim.Lib.Entities;

namespace ElixomClaim.Lib.Services;

public enum SalaryGenerationEligibility
{
    Eligible,
    Inactive,
    BeforeStartDate,
    AfterEndDate,
    NotDue,
    AlreadyGenerated
}

public sealed record SalaryDueDatePlan(DateOnly DueDate, SalaryGenerationEligibility Eligibility)
{
    public bool CanGenerate => Eligibility == SalaryGenerationEligibility.Eligible;
}

public interface ISalaryRecurrencePlanner
{
    SalaryDueDatePlan Plan(SalaryDefinition definition, DateOnly asOfDate, IEnumerable<DateOnly> generatedDueDates);
}

public sealed class SalaryRecurrencePlanner : ISalaryRecurrencePlanner
{
    public const string NearestWeekdayTieBreak = "Earlier occurrence";

    public SalaryDueDatePlan Plan(SalaryDefinition definition, DateOnly asOfDate, IEnumerable<DateOnly> generatedDueDates)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(generatedDueDates);

        var dueDate = CalculateDueDate(definition);
        if (!definition.IsActive)
            return new SalaryDueDatePlan(dueDate, SalaryGenerationEligibility.Inactive);
        if (dueDate < definition.StartDate)
            return new SalaryDueDatePlan(dueDate, SalaryGenerationEligibility.BeforeStartDate);
        if (definition.EndDate is { } endDate && dueDate > endDate)
            return new SalaryDueDatePlan(dueDate, SalaryGenerationEligibility.AfterEndDate);
        if (dueDate > asOfDate)
            return new SalaryDueDatePlan(dueDate, SalaryGenerationEligibility.NotDue);
        if (generatedDueDates.Contains(dueDate))
            return new SalaryDueDatePlan(dueDate, SalaryGenerationEligibility.AlreadyGenerated);

        return new SalaryDueDatePlan(dueDate, SalaryGenerationEligibility.Eligible);
    }

    private static DateOnly CalculateDueDate(SalaryDefinition definition)
    {
        var candidate = definition.LastSalaryDate
            .AddMonths(definition.RecurrenceMonths)
            .AddDays(definition.RecurrenceDays);

        var daysBackward = ((int)candidate.DayOfWeek - (int)definition.NearestWeekday + 7) % 7;
        var daysForward = ((int)definition.NearestWeekday - (int)candidate.DayOfWeek + 7) % 7;

        return daysBackward <= daysForward
            ? candidate.AddDays(-daysBackward)
            : candidate.AddDays(daysForward);
    }
}
