namespace CleanTenant.Domain.Enums;

/// <summary>
/// Kullanıcı hiyerarşi seviyesi.
/// 
/// <para><b>HİYERARŞİK YETKİ MODELİ:</b></para>
/// CleanTenant'ta kullanıcılar 6 seviyede gruplandırılır.
/// Üst seviye, alt seviyenin tüm yetkilerine otomatik sahiptir.
/// Hiçbir alt seviye, üst seviyeye müdahale edemez.
/// 
/// <code>
/// SuperAdmin (100)           → Tüm sisteme tam yetki
///   └── SystemUser (80)      → Tüm tenant'larda rol bazlı yetki
///       └── TenantAdmin (60) → Kendi tenant'ında tam yetki
///           └── TenantUser (40) → Alt şirketlerde tam yetki
///               └── CompanyAdmin (20) → Kendi şirketinde tam yetki
///                   └── CompanyUser (10) → Rol bazlı şirket yetkisi
///                       └── CompanyMember (5) → Sınırlı şirket erişimi
/// </code>
/// 
/// <para><b>NEDEN SAYISAL DEĞERLER?</b></para>
/// Yetki karşılaştırmalarında <c>if (userLevel >= requiredLevel)</c> şeklinde
/// basit sayısal karşılaştırma yapılabilir. Bu, karmaşık switch-case veya
/// if-else zincirlerinden çok daha temiz ve sürdürülebilir bir yaklaşımdır.
/// 
/// Sayılar arasında boşluk bırakılmıştır (5, 10, 20, 40, 60, 80, 100)
/// çünkü gelecekte yeni seviyeler eklenebilir (örn: "CompanyManager = 15").
/// </summary>
public enum UserLevel
{
    /// <summary>
    /// Şirket üyesi — en düşük yetki seviyesi.
    /// Şirket içinde sınırlı erişim (sadece kendine atanan izinler).
    /// Örnek: Bir şirketin dış paydaşı, danışmanı veya sınırlı erişimli çalışanı.
    /// </summary>
    CompanyMember = 5,

    /// <summary>
    /// Şirket kullanıcısı — şirket içinde rol bazlı yetki.
    /// CompanyAdmin'in tanımladığı rollere göre çalışır.
    /// CompanyAdmin'e müdahale edemez.
    /// Örnek: Muhasebe departmanı çalışanı.
    /// </summary>
    CompanyUser = 10,

    /// <summary>
    /// Şirket yöneticisi — şirket içinde tam yetki.
    /// Kendi şirketinde rol ve izin tanımlar, kullanıcılara atar.
    /// Tenant seviyesine müdahale edemez.
    /// Örnek: Bir şirketin IT yöneticisi veya genel müdürü.
    /// </summary>
    CompanyAdmin = 20,

    /// <summary>
    /// Tenant kullanıcısı — tenant'ın tüm alt şirketlerinde tam yetki.
    /// TenantAdmin'in tanımladığı rollere göre çalışır.
    /// TenantAdmin'e müdahale edemez.
    /// Örnek: Mali müşavirlik firmasının muhasebe elemanı.
    /// </summary>
    TenantUser = 40,

    /// <summary>
    /// Tenant yöneticisi — kendi tenant havuzunda tam yetki.
    /// Roller ve izinler tanımlar, kullanıcılara atar.
    /// Sistem seviyesine müdahale edemez.
    /// Örnek: Mali müşavirlik firmasının sahibi.
    /// </summary>
    TenantAdmin = 60,

    /// <summary>
    /// Sistem kullanıcısı — tüm tenant'larda rol bazlı yetki.
    /// SuperAdmin'e müdahale edemez.
    /// Örnek: Platform teknik destek personeli.
    /// </summary>
    SystemUser = 80,

    /// <summary>
    /// Süper yönetici — sistemin tüm katmanlarında sınırsız yetki.
    /// Hiçbir kısıtlamaya tabi değildir.
    /// Üretim ortamında mümkün olduğunca az kullanılmalıdır.
    /// Örnek: Platform kurucusu veya baş sistem yöneticisi.
    /// </summary>
    SuperAdmin = 100
}
