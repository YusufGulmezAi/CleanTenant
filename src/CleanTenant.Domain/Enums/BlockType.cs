

namespace CleanTenant.Domain.Enums;

/// <summary>
/// Kullanıcı bloklama türü.
/// Yetkili bir kullanıcı, başka bir kullanıcıyı farklı şekillerde engelleyebilir.
/// </summary>
public enum BlockType
{
    /// <summary>
    /// Zorunlu çıkış — Kullanıcının mevcut token'ı geçersiz kılınır.
    /// Kullanıcı tekrar login olabilir, ama mevcut oturumu sonlanır.
    /// Kullanım: Yetki değişikliği sonrası güncel rollerin yüklenmesi için.
    /// </summary>
    ForceLogout = 1,

    /// <summary>
    /// Geçici bloke — Belirli bir süre boyunca login yapılamaz.
    /// ExpiresAt alanı ile süre belirlenir.
    /// Kullanım: Şüpheli aktivite tespit edildiğinde.
    /// </summary>
    Temporary = 2,

    /// <summary>
    /// Kalıcı bloke — Süresiz olarak login yapılamaz.
    /// Sadece üst seviye yönetici kaldırabilir.
    /// Kullanım: Kötüye kullanım veya güvenlik ihlali durumunda.
    /// </summary>
    Permanent = 3
}
