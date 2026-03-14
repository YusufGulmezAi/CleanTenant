using CleanTenant.Shared.DTOs.Common;
using CleanTenant.Shared.Helpers;
using FluentAssertions;
using Xunit;

namespace CleanTenant.Application.Tests.Common;

// ============================================================================
// SecurityHelper Tests
// ============================================================================

public class SecurityHelperTests
{
    [Fact]
    public void HashToken_ShouldReturnConsistentHash()
    {
        var hash1 = SecurityHelper.HashToken("test-token");
        var hash2 = SecurityHelper.HashToken("test-token");

        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashToken_DifferentInputs_ShouldReturnDifferentHashes()
    {
        var hash1 = SecurityHelper.HashToken("token-a");
        var hash2 = SecurityHelper.HashToken("token-b");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPassword_ShouldReturnBase64String()
    {
        var hash = SecurityHelper.HashPassword("MyPassword123!");

        hash.Should().NotBeNullOrEmpty();
        // Base64 decode edilebilmeli
        var bytes = Convert.FromBase64String(hash);
        bytes.Length.Should().Be(48); // 16 salt + 32 hash
    }

    [Fact]
    public void HashPassword_SamePasword_ShouldReturnDifferentHashes()
    {
        // Her seferinde farklı salt üretildiği için hash'ler farklı olmalı
        var hash1 = SecurityHelper.HashPassword("SamePassword!");
        var hash2 = SecurityHelper.HashPassword("SamePassword!");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        var password = "MySecurePassword123!";
        var hash = SecurityHelper.HashPassword(password);

        var result = SecurityHelper.VerifyPassword(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        var hash = SecurityHelper.HashPassword("CorrectPassword!");

        var result = SecurityHelper.VerifyPassword("WrongPassword!", hash);

        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithInvalidHash_ShouldReturnFalse()
    {
        var result = SecurityHelper.VerifyPassword("test", "not-a-valid-base64-hash!!!");
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateVerificationCode_ShouldReturn6Digits()
    {
        var code = SecurityHelper.GenerateVerificationCode(6);

        code.Should().HaveLength(6);
        code.Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void GenerateVerificationCode_ShouldBePadded()
    {
        // 100 kez üret — hepsi doğru uzunlukta olmalı
        for (var i = 0; i < 100; i++)
        {
            var code = SecurityHelper.GenerateVerificationCode(6);
            code.Should().HaveLength(6);
        }
    }

    [Fact]
    public void GenerateRandomToken_ShouldReturnBase64()
    {
        var token = SecurityHelper.GenerateRandomToken(64);

        token.Should().NotBeNullOrEmpty();
        // Base64 decode edilebilmeli
        var bytes = Convert.FromBase64String(token);
        bytes.Length.Should().Be(64);
    }

    [Fact]
    public void GenerateRandomToken_ShouldBeUnique()
    {
        var token1 = SecurityHelper.GenerateRandomToken();
        var token2 = SecurityHelper.GenerateRandomToken();

        token1.Should().NotBe(token2);
    }
}

// ============================================================================
// DateTimeHelper Tests
// ============================================================================

public class DateTimeHelperTests
{
    [Fact]
    public void ToLocal_ShouldConvertFromUtcToIstanbul()
    {
        var utc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var local = DateTimeHelper.ToLocal(utc, "Europe/Istanbul");

        // Yaz saati: UTC+3
        local.Hour.Should().Be(13);
    }

    [Fact]
    public void ToUtc_ShouldConvertFromIstanbulToUtc()
    {
        var local = new DateTime(2026, 6, 15, 13, 0, 0);

        var utc = DateTimeHelper.ToUtc(local, "Europe/Istanbul");

        utc.Hour.Should().Be(10);
    }

    [Fact]
    public void ToLocal_WithNullValue_ShouldReturnNull()
    {
        DateTime? nullDate = null;
        var result = DateTimeHelper.ToLocal(nullDate, "Europe/Istanbul");
        result.Should().BeNull();
    }

    [Fact]
    public void ToUtc_WithNullValue_ShouldReturnNull()
    {
        DateTime? nullDate = null;
        var result = DateTimeHelper.ToUtc(nullDate, "Europe/Istanbul");
        result.Should().BeNull();
    }

    [Fact]
    public void NowInTimeZone_ShouldReturnCurrentLocalTime()
    {
        var now = DateTimeHelper.NowInTimeZone("Europe/Istanbul");
        now.Should().BeCloseTo(DateTime.UtcNow.AddHours(3), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Format_ShouldReturnFormattedString()
    {
        var utc = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc);
        var formatted = DateTimeHelper.Format(utc, "Europe/Istanbul", "dd.MM.yyyy HH:mm");
        formatted.Should().Be("15.03.2026 13:30");
    }

    [Fact]
    public void ToLocal_WithInvalidTimezone_ShouldFallbackToDefault()
    {
        var utc = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        // Geçersiz timezone → varsayılan (Europe/Istanbul) kullanılmalı
        var local = DateTimeHelper.ToLocal(utc, "Invalid/Timezone");
        local.Hour.Should().Be(13); // UTC+3
    }
}

// ============================================================================
// ApiResponse Tests
// ============================================================================

public class ApiResponseTests
{
    [Fact]
    public void Success_ShouldSetCorrectFields()
    {
        var response = ApiResponse<string>.Success("data", "İşlem başarılı.", 200);

        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(200);
        response.Data.Should().Be("data");
        response.Message.Should().Be("İşlem başarılı.");
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void Created_ShouldReturn201()
    {
        var response = ApiResponse<string>.Created("new item");

        response.IsSuccess.Should().BeTrue();
        response.StatusCode.Should().Be(201);
    }

    [Fact]
    public void Failure_ShouldSetErrorFields()
    {
        var response = ApiResponse<string>.Failure("Hata mesajı", 400);

        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Hata mesajı");
        response.Errors.Should().Contain("Hata mesajı");
    }

    [Fact]
    public void ValidationFailure_ShouldReturn422WithMultipleErrors()
    {
        var errors = new List<string> { "İsim boş", "E-posta geçersiz" };
        var response = ApiResponse<string>.ValidationFailure(errors);

        response.IsSuccess.Should().BeFalse();
        response.StatusCode.Should().Be(422);
        response.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void NotFound_ShouldReturn404()
    {
        var response = ApiResponse<string>.NotFound("Kayıt bulunamadı.");

        response.StatusCode.Should().Be(404);
        response.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Forbidden_ShouldReturn403()
    {
        var response = ApiResponse<string>.Forbidden();

        response.StatusCode.Should().Be(403);
        response.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void PaginatedResult_ShouldCalculatePages()
    {
        var result = new PaginatedResult<string>(
            ["a", "b", "c"], totalCount: 10, pageNumber: 1, pageSize: 3);

        result.TotalPages.Should().Be(4); // ceil(10/3) = 4
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void PaginatedResult_LastPage_ShouldNotHaveNext()
    {
        var result = new PaginatedResult<string>(
            ["a"], totalCount: 10, pageNumber: 4, pageSize: 3);

        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();
    }
}
