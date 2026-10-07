using Anima.SharedKernel;

namespace Anima.Identity.Contracts;

/// <summary>Phát đồng bộ trong transaction đăng ký. Người nhận: Fairness (tạo seed), Wallet (thưởng đăng nhập), Gacha (gói chào mừng).</summary>
public sealed record AccountRegistered(Guid AccountId, string LegalCountry, string Locale) : IDomainEvent;

/// <summary>Phát khi người chơi xác thực SĐT thành công (người nhận: Quest).</summary>
public sealed record PhoneVerified(Guid AccountId) : IDomainEvent;

public sealed record AccountInfo(Guid Id, string Status, bool PhoneVerified, string Locale, string Timezone, string LegalCountry, string? RestrictionReason, DateTimeOffset CreatedAt);

public interface IIdentityApi
{
    Task<AccountInfo> GetAsync(Guid accountId, CancellationToken ct);
}

public sealed record AdminAccountRow(Guid Id, string EmailMasked, string? PhoneMasked, string Status, string? RestrictionReason, string LegalCountry, string Locale, DateTimeOffset CreatedAt);
public sealed record PiiView(string Email, string? Phone, string BirthDate);

/// <summary>Thao tác quản trị trên tài khoản người chơi. Chỉ module Admin gọi; Admin chịu trách nhiệm phân quyền và audit.</summary>
public interface IIdentityAdminApi
{
    /// <summary>Tìm theo ID, email chính xác hoặc SĐT E.164 chính xác; không có từ khóa thì trả các tài khoản mới nhất. Dữ liệu cá nhân đã che.</summary>
    Task<IReadOnlyList<AdminAccountRow>> SearchAsync(string? query, int limit, CancellationToken ct);
    Task<AdminAccountRow?> GetAsync(Guid id, CancellationToken ct);
    Task<PiiView> RevealPiiAsync(Guid id, CancellationToken ct);
    /// <summary>action: restrict | unrestrict | ban | unban (BRD 8.1). Trả về trạng thái trước và sau.</summary>
    Task<(string Before, string After)> ChangeStatusAsync(Guid id, string action, CancellationToken ct);
}
