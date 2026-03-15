using CleanTenant.Domain.Security;
using CleanTenant.Domain.Settings;
using CleanTenant.Domain.Email;
using Xunit;

namespace CleanTenant.Domain.Tests.Security;

/// <summary>AccessPolicy entity testleri.</summary>
public class AccessPolicyTests
{
    [Fact]
    public void CreateDefault_ShouldSetDenyAll()
    {
        var policy = AccessPolicy.CreateDefault(PolicyLevel.System);

        Assert.True(policy.IsDefault);
        Assert.True(policy.DenyAllIps);
        Assert.True(policy.DenyAllTimes);
        Assert.True(policy.IsActive);
        Assert.Equal(PolicyLevel.System, policy.Level);
        Assert.Contains("Varsayılan", policy.Name);
    }

    [Fact]
    public void CreateDefault_ForTenant_ShouldSetTenantId()
    {
        var tenantId = Guid.NewGuid();
        var policy = AccessPolicy.CreateDefault(PolicyLevel.Tenant, tenantId);

        Assert.Equal(PolicyLevel.Tenant, policy.Level);
        Assert.Equal(tenantId, policy.TenantId);
        Assert.True(policy.IsDefault);
    }

    [Fact]
    public void CreateCustom_ShouldNotBeDefault()
    {
        var policy = AccessPolicy.CreateCustom("Ofis Politikası", PolicyLevel.Tenant);

        Assert.False(policy.IsDefault);
        Assert.Equal("Ofis Politikası", policy.Name);
        Assert.False(policy.DenyAllIps);
        Assert.False(policy.DenyAllTimes);
    }

    [Fact]
    public void CreateFullAccess_ShouldAllowEverything()
    {
        var policy = AccessPolicy.CreateFullAccess(PolicyLevel.System);

        Assert.False(policy.DenyAllIps);
        Assert.False(policy.DenyAllTimes);
        Assert.Contains("0.0.0.0/0", policy.AllowedIpRanges);
        Assert.Contains("1", policy.AllowedDays);
        Assert.Contains("7", policy.AllowedDays);
    }

    [Fact]
    public void UpdateIpRules_Default_ShouldThrowIfRelaxing()
    {
        var policy = AccessPolicy.CreateDefault(PolicyLevel.System);

        Assert.Throws<InvalidOperationException>(() =>
            policy.UpdateIpRules(false, "[\"0.0.0.0/0\"]", "admin"));
    }

    [Fact]
    public void UpdateInfo_Default_ShouldThrow()
    {
        var policy = AccessPolicy.CreateDefault(PolicyLevel.System);

        Assert.Throws<InvalidOperationException>(() =>
            policy.UpdateInfo("Yeni Ad", null, "admin"));
    }

    [Fact]
    public void SetActive_Default_ShouldNotDeactivate()
    {
        var policy = AccessPolicy.CreateDefault(PolicyLevel.System);

        Assert.Throws<InvalidOperationException>(() =>
            policy.SetActive(false, "admin"));
    }

    [Fact]
    public void UpdateIpRules_Custom_ShouldUpdate()
    {
        var policy = AccessPolicy.CreateCustom("Test", PolicyLevel.Tenant);

        policy.UpdateIpRules(false, "[\"192.168.1.0/24\"]", "admin");

        Assert.False(policy.DenyAllIps);
        Assert.Contains("192.168.1.0/24", policy.AllowedIpRanges);
    }

    [Fact]
    public void UpdateTimeRules_Custom_ShouldUpdate()
    {
        var policy = AccessPolicy.CreateCustom("Test", PolicyLevel.Tenant);

        policy.UpdateTimeRules(false, "[1,2,3,4,5]",
            new TimeOnly(8, 0), new TimeOnly(18, 0), "admin");

        Assert.False(policy.DenyAllTimes);
        Assert.Contains("1", policy.AllowedDays);
        Assert.Equal(new TimeOnly(8, 0), policy.AllowedTimeStart);
        Assert.Equal(new TimeOnly(18, 0), policy.AllowedTimeEnd);
    }
}

/// <summary>SystemSetting entity testleri.</summary>
public class SystemSettingTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var setting = SystemSetting.Create(
            "Jwt.AccessTokenExpirationMinutes", "15", "JWT",
            SettingValueType.Int, SettingLevel.System,
            description: "Token süresi");

        Assert.Equal("Jwt.AccessTokenExpirationMinutes", setting.Key);
        Assert.Equal("15", setting.Value);
        Assert.Equal("JWT", setting.Category);
        Assert.Equal(SettingValueType.Int, setting.ValueType);
        Assert.Equal(SettingLevel.System, setting.Level);
        Assert.False(setting.IsReadOnly);
    }

    [Fact]
    public void UpdateValue_ShouldChangeValue()
    {
        var setting = SystemSetting.Create("Test.Key", "old", "Test");

        setting.UpdateValue("new", "admin");

        Assert.Equal("new", setting.Value);
        Assert.Equal("admin", setting.UpdatedBy);
        Assert.NotNull(setting.UpdatedAt);
    }

    [Fact]
    public void UpdateValue_ReadOnly_ShouldThrow()
    {
        var setting = SystemSetting.Create("Test.Key", "old", "Test",
            isReadOnly: true);

        Assert.Throws<InvalidOperationException>(() =>
            setting.UpdateValue("new", "admin"));
    }

    [Fact]
    public void Create_WithTenant_ShouldSetTenantId()
    {
        var tenantId = Guid.NewGuid();
        var setting = SystemSetting.Create(
            "Jwt.AccessTokenExpirationMinutes", "30", "JWT",
            SettingValueType.Int, SettingLevel.Tenant, tenantId);

        Assert.Equal(SettingLevel.Tenant, setting.Level);
        Assert.Equal(tenantId, setting.TenantId);
    }

    [Fact]
    public void Create_EmptyKey_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            SystemSetting.Create("", "value", "Test"));
    }
}

/// <summary>EmailLog entity testleri.</summary>
public class EmailLogTests
{
    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var log = EmailLog.Create("test@test.com", "Konu", "<h1>Body</h1>");

        Assert.Equal("test@test.com", log.To);
        Assert.Equal("Konu", log.Subject);
        Assert.Equal(EmailStatus.Queued, log.Status);
        Assert.Equal(0, log.AttemptCount);
    }

    [Fact]
    public void Create_WithCcBcc_ShouldSet()
    {
        var log = EmailLog.Create("to@test.com", "Konu",
            cc: "cc@test.com", bcc: "bcc@test.com");

        Assert.Equal("cc@test.com", log.Cc);
        Assert.Equal("bcc@test.com", log.Bcc);
    }

    [Fact]
    public void MarkSending_ShouldIncrementAttempt()
    {
        var log = EmailLog.Create("test@test.com", "Konu");

        log.MarkSending();

        Assert.Equal(EmailStatus.Sending, log.Status);
        Assert.Equal(1, log.AttemptCount);
        Assert.NotNull(log.LastAttemptAt);
    }

    [Fact]
    public void MarkSent_ShouldSetSentAt()
    {
        var log = EmailLog.Create("test@test.com", "Konu");
        log.MarkSending();

        log.MarkSent();

        Assert.Equal(EmailStatus.Sent, log.Status);
        Assert.NotNull(log.SentAt);
    }

    [Fact]
    public void MarkFailed_ShouldSetErrorMessage()
    {
        var log = EmailLog.Create("test@test.com", "Konu");
        log.MarkSending();

        log.MarkFailed("SMTP timeout");

        Assert.Equal(EmailStatus.Failed, log.Status);
        Assert.Equal("SMTP timeout", log.ErrorMessage);
    }

    [Fact]
    public void Create_WithAttachments_ShouldTrackSize()
    {
        var log = EmailLog.Create("test@test.com", "Konu",
            attachmentNames: "doc.pdf, img.png", attachmentSize: 1024);

        Assert.Equal("doc.pdf, img.png", log.AttachmentNames);
        Assert.Equal(1024, log.AttachmentTotalSize);
    }
}

/// <summary>IpBlacklist entity testleri.</summary>
public class IpBlacklistTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var bl = IpBlacklist.Create("192.168.1.100", "Brute force");

        Assert.Equal("192.168.1.100", bl.IpAddressOrRange);
        Assert.Equal("Brute force", bl.Reason);
        Assert.True(bl.IsActive);
        Assert.Null(bl.ExpiresAt);
    }

    [Fact]
    public void Create_EmptyIp_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            IpBlacklist.Create("", "reason"));
    }

    [Fact]
    public void Deactivate_ShouldSetInactive()
    {
        var bl = IpBlacklist.Create("10.0.0.1", "test");

        bl.Deactivate();

        Assert.False(bl.IsActive);
    }

    [Fact]
    public void Activate_ShouldSetActive()
    {
        var bl = IpBlacklist.Create("10.0.0.1", "test");
        bl.Deactivate();

        bl.Activate();

        Assert.True(bl.IsActive);
    }
}
