using System.Security.Claims;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace CleanTenant.BlazorUI.Services;

public class CleanTenantAuthStateProvider : AuthenticationStateProvider
{
	private readonly ILocalStorageService _localStorage;
	private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

	public CleanTenantAuthStateProvider(ILocalStorageService localStorage)
	{
		_localStorage = localStorage;
	}

	public override async Task<AuthenticationState> GetAuthenticationStateAsync()
	{
		try
		{
			var token = await _localStorage.GetItemAsStringAsync("accessToken");
			if (string.IsNullOrEmpty(token)) return Anonymous;

			token = token.Trim('"');
			var claims = ParseClaimsFromJwt(token);

			// Token süresi dolmuş mu?
			var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
			if (expClaim is not null && long.TryParse(expClaim.Value, out var exp))
			{
				var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
				if (expDate < DateTimeOffset.UtcNow)
				{
					await _localStorage.RemoveItemAsync("accessToken");
					return Anonymous;
				}
			}

			var identity = new ClaimsIdentity(claims, "jwt");
			return new AuthenticationState(new ClaimsPrincipal(identity));
		}
		catch
		{
			return Anonymous;
		}
	}

	public void NotifyAuthenticationStateChanged()
	{
		NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
	}

	private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
	{
		var parts = jwt.Split('.');
		if (parts.Length != 3) return [];

		var payload = parts[1];
		var jsonBytes = ParseBase64WithoutPadding(payload);
		var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

		return keyValuePairs?.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString() ?? "")) ?? [];
	}

	private static byte[] ParseBase64WithoutPadding(string base64)
	{
		switch (base64.Length % 4)
		{
			case 2: base64 += "=="; break;
			case 3: base64 += "="; break;
		}
		return Convert.FromBase64String(base64.Replace('-', '+').Replace('_', '/'));
	}
}