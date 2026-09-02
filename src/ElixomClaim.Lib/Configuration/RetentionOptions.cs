using System.ComponentModel.DataAnnotations;

namespace ElixomClaim.Lib.Configuration;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    [Range(4, 100, ErrorMessage = "Retention must be at least four years.")]
    public int FinancialRecordRetentionYears { get; set; } = 9;
}
