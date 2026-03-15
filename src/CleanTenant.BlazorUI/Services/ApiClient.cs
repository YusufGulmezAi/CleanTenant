using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using CleanTenant.Shared.DTOs.Common;

namespace CleanTenant.BlazorUI.Services;

/// <summary>API istemcisi — token yönetimi, header injection, hata yakalama.</summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILocalStorageService _localStorage;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(HttpClient http, ILocalStorageService localStorage)
    {
        _http = http;
        _localStorage = localStorage;
    }

    /// <summary>Token'ı Authorization header'ına ekler.</summary>
    private async Task SetAuthHeaderAsync()
    {
        var token = await _localStorage.GetItemAsStringAsync("accessToken");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim('"'));
    }

    /// <summary>Tenant/Company context header'larını ekler.</summary>
    private async Task SetContextHeadersAsync()
    {
        var tenantId = await _localStorage.GetItemAsStringAsync("activeTenantId");
        var companyId = await _localStorage.GetItemAsStringAsync("activeCompanyId");

        _http.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _http.DefaultRequestHeaders.Remove("X-Company-Id");

        if (!string.IsNullOrEmpty(tenantId))
            _http.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.Trim('"'));
        if (!string.IsNullOrEmpty(companyId))
            _http.DefaultRequestHeaders.Add("X-Company-Id", companyId.Trim('"'));
    }

    public async Task<ApiResponse<T>?> GetAsync<T>(string url)
    {
        await SetAuthHeaderAsync();
        await SetContextHeadersAsync();
        var response = await _http.GetAsync(url);
        return await ParseResponse<T>(response);
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string url, object? body = null)
    {
        await SetAuthHeaderAsync();
        await SetContextHeadersAsync();
        var response = await _http.PostAsJsonAsync(url, body);
        return await ParseResponse<T>(response);
    }

    public async Task<ApiResponse<T>?> PutAsync<T>(string url, object? body = null)
    {
        await SetAuthHeaderAsync();
        await SetContextHeadersAsync();
        var response = await _http.PutAsJsonAsync(url, body);
        return await ParseResponse<T>(response);
    }

    public async Task<ApiResponse<T>?> DeleteAsync<T>(string url)
    {
        await SetAuthHeaderAsync();
        await SetContextHeadersAsync();
        var response = await _http.DeleteAsync(url);
        return await ParseResponse<T>(response);
    }

    /// <summary>Login — token saklamadan raw API çağrısı.</summary>
    public async Task<ApiResponse<T>?> PostAnonymousAsync<T>(string url, object? body = null)
    {
        var response = await _http.PostAsJsonAsync(url, body);
        return await ParseResponse<T>(response);
    }

    private static async Task<ApiResponse<T>?> ParseResponse<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOpts);
        }
        catch
        {
            return new ApiResponse<T> { IsSuccess = false, StatusCode = (int)response.StatusCode, Message = json };
        }
    }
}
