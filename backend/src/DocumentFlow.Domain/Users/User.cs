using DocumentFlow.Domain.Common;

namespace DocumentFlow.Domain.Users;

public enum UserRole
{
    Member = 0,
    Admin = 1,
}

public sealed class User : Entity, IAuditable
{
    public const int NameMaxLength = 100;
    public const int DepartmentMaxLength = 100;

    private User()
    {
        // Required by EF Core.
    }

    public User(string email, string fullName, string department, UserRole role = UserRole.Member)
    {
        Email = NormalizeEmail(email);
        UpdateProfile(fullName, department);
        Role = role;
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string Department { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }

    public void UpdateProfile(string fullName, string department)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > NameMaxLength)
        {
            throw new DomainException($"Full name is required and must be at most {NameMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(department) || department.Trim().Length > DepartmentMaxLength)
        {
            throw new DomainException($"Department is required and must be at most {DepartmentMaxLength} characters.");
        }

        FullName = fullName.Trim();
        Department = department.Trim();
    }

    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new DomainException("Unknown role.");
        }

        Role = role;
    }

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;
}
