using System.Net;
using System.Text.Json;
using CleanTenant.Application.Common.Interfaces;
using CleanTenant.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CleanTenant.Infrastructure.Security;

/// <summary>
/// Erişim politikası servisi — kullanıcı bazlı IP ve zaman kısıtlamaları.
/// 
/// <para><b>KONTROL SIRASI (Login anında):</b></para>
/// <list type="number">
///   <item>IP whitelist: Kullanıcının izinli IP'leri tanımlıysa, gelen IP bu listede mi?</item>
///   <item>Zaman kısıtlaması: İzinli günler ve saatler tanımlıysa, şu an uygun mu?</item>
/// </list>
/// 
/// <para><b>PARAMETRİK:</b></para>
/// appsettings.json'dan global olarak aktif/pasif edilir.
/// Aktif edildiğinde kullanıcı bazında detay tanımlanır.
/// </summary>
public class AccessPolicyService
{
    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public AccessPolicyService(IApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    /// <summary>
    /// Kullanıcının erişim politikasını kontrol eder.
    /// Login anında çağrılır — şifre doğrulandıktan sonra, token üretilmeden önce.
    /// </summary>
    public async Task<Result<bool>> ValidateAccessAsync(
        Guid userId, string ipAddress, CancellationToken ct)
    {
        var policy = await _db.UserAccessPolicies
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsEnabled, ct);

        // Politika yoksa veya devre dışıysa → erişim serbest
        if (policy is null)
            return Result<bool>.Success(true);

        // IP whitelist kontrolü
        var enableIpWhitelist = bool.Parse(
            _configuration["CleanTenant:AccessPolicy:EnableIpWhitelist"] ?? "false");

        if (enableIpWhitelist)
        {
            var ipResult = ValidateIpAddress(policy.AllowedIpRanges, ipAddress);
            if (ipResult.IsFailure)
                return ipResult;
        }

        // Zaman kısıtlaması kontrolü
        var enableTimeRestriction = bool.Parse(
            _configuration["CleanTenant:AccessPolicy:EnableTimeRestriction"] ?? "false");

        if (enableTimeRestriction)
        {
            var timeResult = ValidateTimeRestriction(
                policy.AllowedDays,
                policy.AllowedTimeStart,
                policy.AllowedTimeEnd);
            if (timeResult.IsFailure)
                return timeResult;
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Gelen IP adresinin izinli listede olup olmadığını kontrol eder.
    /// CIDR notasyonu desteklenir (192.168.1.0/24).
    /// </summary>
    private static Result<bool> ValidateIpAddress(string allowedIpRangesJson, string ipAddress)
    {
        if (string.IsNullOrEmpty(allowedIpRangesJson) || allowedIpRangesJson == "[]")
            return Result<bool>.Success(true);  // Liste boş → kısıtlama yok

        try
        {
            var allowedRanges = JsonSerializer.Deserialize<List<string>>(allowedIpRangesJson);
            if (allowedRanges is null || allowedRanges.Count == 0)
                return Result<bool>.Success(true);

            if (!IPAddress.TryParse(ipAddress, out var clientIp))
                return Result<bool>.Failure("Geçersiz IP adresi.", 400);

            foreach (var range in allowedRanges)
            {
                if (range.Contains('/'))
                {
                    // CIDR notasyonu: "192.168.1.0/24"
                    if (IsIpInCidrRange(clientIp, range))
                        return Result<bool>.Success(true);
                }
                else
                {
                    // Tekil IP: "192.168.1.100"
                    if (IPAddress.TryParse(range, out var allowedIp) &&
                        clientIp.Equals(allowedIp))
                        return Result<bool>.Success(true);
                }
            }

            return Result<bool>.Failure(
                $"IP adresiniz ({ipAddress}) izinli IP listesinde bulunmamaktadır.", 403);
        }
        catch
        {
            return Result<bool>.Success(true);  // Parse hatası → erişime izin ver (fail-open)
        }
    }

    /// <summary>
    /// Mevcut zaman diliminin izinli gün ve saatlerde olup olmadığını kontrol eder.
    /// </summary>
    private static Result<bool> ValidateTimeRestriction(
        string allowedDaysJson, TimeOnly? allowedTimeStart, TimeOnly? allowedTimeEnd)
    {
        var now = DateTime.UtcNow;

        // Gün kontrolü
        if (!string.IsNullOrEmpty(allowedDaysJson) && allowedDaysJson != "[]")
        {
            try
            {
                var allowedDays = JsonSerializer.Deserialize<List<int>>(allowedDaysJson);
                if (allowedDays is not null && allowedDays.Count > 0)
                {
                    var currentDay = (int)now.DayOfWeek;  // 0=Sunday, 1=Monday, ...
                    if (!allowedDays.Contains(currentDay))
                        return Result<bool>.Failure(
                            "Bugün bu hesapla giriş yapılmasına izin verilmemektedir.", 403);
                }
            }
            catch { /* Parse hatası → erişime izin ver */ }
        }

        // Saat kontrolü
        if (allowedTimeStart.HasValue && allowedTimeEnd.HasValue)
        {
            var currentTime = TimeOnly.FromDateTime(now);

            if (currentTime < allowedTimeStart.Value || currentTime > allowedTimeEnd.Value)
                return Result<bool>.Failure(
                    $"Bu saatte ({currentTime:HH:mm} UTC) giriş yapılmasına izin verilmemektedir. " +
                    $"İzinli saat aralığı: {allowedTimeStart.Value:HH:mm} - {allowedTimeEnd.Value:HH:mm} UTC",
                    403);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>CIDR aralığında IP kontrolü. Örnek: 192.168.1.50 in 192.168.1.0/24?</summary>
    private static bool IsIpInCidrRange(IPAddress address, string cidrRange)
    {
        try
        {
            var parts = cidrRange.Split('/');
            if (parts.Length != 2) return false;

            var baseAddress = IPAddress.Parse(parts[0]);
            var prefixLength = int.Parse(parts[1]);

            var baseBytes = baseAddress.GetAddressBytes();
            var addressBytes = address.GetAddressBytes();

            if (baseBytes.Length != addressBytes.Length)
                return false;

            var totalBits = baseBytes.Length * 8;
            for (var i = 0; i < totalBits; i++)
            {
                if (i >= prefixLength) break;

                var byteIndex = i / 8;
                var bitIndex = 7 - (i % 8);

                var baseBit = (baseBytes[byteIndex] >> bitIndex) & 1;
                var addrBit = (addressBytes[byteIndex] >> bitIndex) & 1;

                if (baseBit != addrBit)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
