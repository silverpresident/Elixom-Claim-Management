using System.ComponentModel.DataAnnotations;
using ElixomClaim.Lib.Configuration;
using Xunit;

namespace ElixomClaim.Lib.Tests.Configuration;

public class RetentionOptionsTests
{
    [Fact]
    public void RetentionFloor_IsFourYears()
    {
        var options = new RetentionOptions { FinancialRecordRetentionYears = 3 };
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(options, new ValidationContext(options), results, true);
        Assert.False(valid);
        Assert.Contains(results, result => result.ErrorMessage!.Contains("at least four years"));
    }
}
