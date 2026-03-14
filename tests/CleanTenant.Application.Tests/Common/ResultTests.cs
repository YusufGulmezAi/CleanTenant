using CleanTenant.Application.Common.Models;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Application.Tests.Common;

/// <summary>
/// Result pattern birim testleri.
/// Result&lt;T&gt; sınıfının factory method'larını doğrular.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_ShouldReturnSuccessResult()
    {
        // Arrange & Act
        var result = Result<string>.Success("test data");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("test data");
        result.StatusCode.Should().Be(200);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldReturnFailureResult()
    {
        // Arrange & Act
        var result = Result<string>.Failure("Hata oluştu.", 400);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Hata oluştu.");
        result.StatusCode.Should().Be(400);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void NotFound_ShouldReturn404()
    {
        // Arrange & Act
        var result = Result<string>.NotFound("Kayıt bulunamadı.");

        // Assert
        result.StatusCode.Should().Be(404);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ValidationFailure_ShouldReturnMultipleErrors()
    {
        // Arrange
        var errors = new List<string> { "İsim boş olamaz.", "E-posta geçersiz." };

        // Act
        var result = Result<string>.ValidationFailure(errors);

        // Assert
        result.StatusCode.Should().Be(422);
        result.Errors.Should().HaveCount(2);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Created_ShouldReturn201()
    {
        // Arrange & Act
        var result = Result<string>.Created("new item");

        // Assert
        result.StatusCode.Should().Be(201);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("new item");
    }
}
