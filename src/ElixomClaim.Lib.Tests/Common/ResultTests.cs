using ElixomClaim.Lib.Common;
using Xunit;

namespace ElixomClaim.Lib.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void Failure_CreatesFailureResultWithError()
    {
        var result = Result.Failure("An error occurred");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("An error occurred", result.Error);
    }

    [Fact]
    public void GenericSuccess_WithValue_CreatesTypedSuccessResult()
    {
        var result = Result.Success("Sample Data");

        Assert.True(result.IsSuccess);
        Assert.Equal("Sample Data", result.Value);
    }

    [Fact]
    public void GenericFailure_CreatesTypedFailureResult()
    {
        var result = Result.Failure<string>("Invalid input");

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal("Invalid input", result.Error);
    }
}
