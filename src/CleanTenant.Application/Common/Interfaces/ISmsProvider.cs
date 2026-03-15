

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// SMS gönderici servis sözleşmesi.
/// Twilio, Vonage veya yerli SMS provider'lar için implementasyon yazılır.
/// 
/// <para><b>NEDEN INTERFACE?</b></para>
/// SMS provider değiştiğinde (Twilio → İleti Merkezi gibi) sadece
/// Infrastructure katmanındaki implementasyon değişir. Application
/// katmanı ve Domain katmanı hiç etkilenmez.
/// </para>
/// </summary>
public interface ISmsProvider
{
    /// <summary>SMS gönderir.</summary>
    /// <param name="phoneNumber">Alıcı telefon numarası (uluslararası format)</param>
    /// <param name="message">SMS içeriği</param>
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}
