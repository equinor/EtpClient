using EtpClient.Models;
using Xunit;

namespace EtpClient.UnitTests.Models;

public sealed class EtpConnectionExceptionTests
{
    [Theory]
    [InlineData(EtpConnectionFailureCategory.Validation)]
    [InlineData(EtpConnectionFailureCategory.Authentication)]
    [InlineData(EtpConnectionFailureCategory.Transport)]
    [InlineData(EtpConnectionFailureCategory.Protocol)]
    [InlineData(EtpConnectionFailureCategory.Cancellation)]
    public void Category_IsPreserved(EtpConnectionFailureCategory category)
    {
        var ex = new EtpConnectionException(category, "something failed");
        Assert.Equal(category, ex.Category);
    }

    [Fact]
    public void Message_DoesNotContain_LiteralPassword()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Authentication, "Authentication failed");
        Assert.DoesNotContain("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Message_DoesNotContain_AuthorizationHeader()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Transport, "Transport error occurred");
        Assert.DoesNotContain("Authorization", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HttpStatusCode_IsNullByDefault()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Transport, "error");
        Assert.Null(ex.HttpStatusCode);
    }

    [Fact]
    public void HttpStatusCode_IsPreservedWhenProvided()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Authentication, "error",
            httpStatusCode: 401);
        Assert.Equal(401, ex.HttpStatusCode);
    }

    [Fact]
    public void EtpErrorCode_IsNullByDefault()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Protocol, "error");
        Assert.Null(ex.EtpErrorCode);
    }

    [Fact]
    public void EtpErrorCode_IsPreservedWhenProvided()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Protocol, "protocol error",
            etpErrorCode: 14);
        Assert.Equal(14, ex.EtpErrorCode);
    }

    [Fact]
    public void InnerException_IsPreservedWhenProvided()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Transport, "outer", innerException: inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void IsExceptionSubclass()
    {
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Validation, "x");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
