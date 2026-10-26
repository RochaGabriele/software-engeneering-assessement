using Demo.Application.Common;

namespace Demo.UnitTests.Application.Common;

public class ResultTests
{
    [Fact]
    public void Success_ExposesValue()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ExposesError()
    {
        var error = Error.NotFound("thing.not_found", "No thing.");

        var result = Result<int>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Failure_AccessingValue_ThrowsInvalidOperationException()
    {
        var result = Result<int>.Failure(Error.Validation("x", "y"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversionFromError_CreatesFailure()
    {
        Result<string> result = Error.Unavailable("x", "y");

        Assert.False(result.IsSuccess);
        Assert.Equal("x", result.Error!.Code);
    }

    [Fact]
    public void ErrorFactories_SetCodeMessageAndType()
    {
        Assert.Equal(new Error("a", "b", ErrorType.Validation), Error.Validation("a", "b"));
        Assert.Equal(new Error("a", "b", ErrorType.NotFound), Error.NotFound("a", "b"));
        Assert.Equal(new Error("a", "b", ErrorType.Unavailable), Error.Unavailable("a", "b"));
    }
}
