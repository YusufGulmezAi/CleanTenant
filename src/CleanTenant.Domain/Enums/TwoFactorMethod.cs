

namespace CleanTenant.Domain.Enums;

/// <summary>
/// İki faktörlü doğrulama (2FA) metod türü.
/// 
/// <para><b>FALLBACK MEKANİZMASI:</b></para>
/// Kullanıcı birincil metoda (SMS veya Authenticator) erişemediğinde
/// her zaman e-posta ile fallback yapılabilir. Bu yüzden e-posta
/// doğrulaması her kullanıcı için ZORUNLUDUR.
/// </summary>
public enum TwoFactorMethod
{
    /// <summary>
    /// 2FA kapalı — sadece şifre ile giriş.
    /// </summary>
    None = 0,

    /// <summary>
    /// E-posta ile doğrulama kodu.
    /// Temel 2FA metodu ve diğer metodların fallback'i.
    /// Avantaj: Ek cihaz veya uygulama gerektirmez.
    /// Dezavantaj: E-posta hesabı ele geçirilmişse güvenli değildir.
    /// </summary>
    Email = 1,

    /// <summary>
    /// SMS ile doğrulama kodu.
    /// ISmsProvider interface'i üzerinden (Twilio vb.) gönderilir.
    /// Avantaj: Telefonun fiziksel olarak elde olması gerekir.
    /// Dezavantaj: SIM swap saldırılarına açıktır.
    /// </summary>
    Sms = 2,

    /// <summary>
    /// TOTP (Time-based One-Time Password) — Google Authenticator, Authy vb.
    /// QR kod ile kurulur, 30 saniyelik değişen kodlar üretir.
    /// Avantaj: En güvenli yöntem — internet bağlantısı gerektirmez.
    /// Dezavantaj: Telefon kaybedilirse kurtarma kodu gerekir.
    /// </summary>
    Authenticator = 3
}
