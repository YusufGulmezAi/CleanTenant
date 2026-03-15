

namespace CleanTenant.Application.Common.Interfaces;

/// <summary>
/// Şirket bazlı yedekleme servis sözleşmesi.
/// Background job tarafından çalıştırılır.
/// </summary>
public interface IBackupService
{
    /// <summary>Belirli bir şirketin verilerini yedekler.</summary>
    Task<string> BackupCompanyAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>Yedekten geri yükleme yapar.</summary>
    Task RestoreCompanyAsync(Guid companyId, string backupPath, CancellationToken ct = default);

    /// <summary>Süresi geçmiş yedekleri temizler.</summary>
    Task CleanupOldBackupsAsync(int retentionDays, CancellationToken ct = default);
}
