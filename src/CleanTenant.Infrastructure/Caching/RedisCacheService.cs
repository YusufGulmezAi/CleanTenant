using System.Text.Json;
using CleanTenant.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CleanTenant.Infrastructure.Caching;

/// <summary>
/// Redis tabanlı cache servisi — ICacheService implementasyonu.
/// 
/// <para><b>REDIS'İN CleanTenant'TAKİ GÖREVLERİ:</b></para>
/// <list type="bullet">
///   <item>Oturum bilgileri (session:{userId})</item>
///   <item>Kullanıcı rolleri/izinleri (user:{userId}:roles)</item>
///   <item>IP kara listesi (blacklist:ips — SET tipi)</item>
///   <item>Rate limiting (sliding window)</item>
///   <item>TempToken saklama (2FA akışı)</item>
///   <item>RefreshToken saklama (dual storage)</item>
///   <item>Query cache (ICacheableQuery)</item>
/// </list>
/// 
/// <para><b>BAĞLANTI YÖNETİMİ:</b></para>
/// ConnectionMultiplexer Singleton olarak DI'da kayıtlıdır.
/// Thread-safe'dir ve tüm istekler tek bağlantı üzerinden çalışır.
/// Redis bağlantısı koptuğunda otomatik yeniden bağlanır.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var value = await _db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString(), _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET hatası. Key: {CacheKey}", key);
            return default;  // Cache hatası uygulamayı kırmamalı
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);

            if (expiration.HasValue)
                await _db.StringSetAsync(key, json, expiry: expiration.Value);
            else
                await _db.StringSetAsync(key, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET hatası. Key: {CacheKey}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DELETE hatası. Key: {CacheKey}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        try
        {
            // SCAN komutu ile prefix'e uyan key'leri bul ve sil
            // KEYS komutu production'da KULLANILMAZ (blocking)
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());

            var keys = server.Keys(pattern: $"{prefix}*").ToArray();

            if (keys.Length > 0)
            {
                await _db.KeyDeleteAsync(keys);
                _logger.LogDebug("Redis PREFIX DELETE: {Prefix}* → {Count} key silindi", prefix, keys.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis PREFIX DELETE hatası. Prefix: {Prefix}", prefix);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis EXISTS hatası. Key: {CacheKey}", key);
            return false;
        }
    }

    // ========================================================================
    // SET OPERASYONLARI — IP Blacklist için
    // Redis SET: Benzersiz elemanlar, O(1) lookup
    // ========================================================================

    /// <inheritdoc />
    public async Task SetAddAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            await _db.SetAddAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET ADD hatası. Key: {CacheKey}, Value: {Value}", key, value);
        }
    }

    /// <inheritdoc />
    public async Task SetRemoveAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            await _db.SetRemoveAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET REMOVE hatası. Key: {CacheKey}", key);
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetContainsAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            return await _db.SetContainsAsync(key, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET CONTAINS hatası. Key: {CacheKey}", key);
            return false;  // Hata durumunda erişime izin ver (fail-open)
        }
    }
}
