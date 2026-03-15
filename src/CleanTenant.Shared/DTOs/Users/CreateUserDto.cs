

namespace CleanTenant.Shared.DTOs.Users;

/// <summary>
/// Kullanıcı oluşturma isteği.
/// E-posta ile arama yapılır — varsa mevcut kullanıcıya rol eklenir.
/// </summary>
public class CreateUserDto
{
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? PhoneNumber { get; set; }
    public string? Password { get; set; }
}
