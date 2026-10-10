using Anima.SharedKernel;

namespace Anima.Identity.Contracts;

/// <summary>Phát đồng bộ trong transaction đăng ký. Người nhận: Fairness (tạo seed), Wallet (thưởng đăng nhập), Gacha (gói chào mừng).</summary>
public sealed record AccountRegistered(Guid AccountId, string LegalCountry, string Locale) : IDomainEvent;

/// <summary>Phát khi người chơi xác thực SĐT thành công (người nhận: Quest).</summary>
public sealed record PhoneVerified(Guid AccountId) : IDomainEvent;

public sealed record AccountInfo(Guid Id, string Status, bool PhoneVerified, string Locale, string Timezone, string LegalCountry, string? RestrictionReason, DateTimeOffset CreatedAt, bool IsMinor = false, bool GuardianConsent = false);

public interface IIdentityApi
{
    Task<AccountInfo> GetAsync(Guid accountId, CancellationToken ct);
    /// <summary>
    /// BR-WAL-04: bật/tắt hạn chế NEGATIVE_GEM. Bật: Unverified/Verified → Restricted (nhớ trạng thái cũ). Tắt: chỉ khi đang Restricted vì NEGATIVE_GEM
    /// thì trả về trạng thái trước đó. Không đụng tới Restricted vì FRAUD, Banned, PendingDeletion, Deleted. Trả về true nếu trạng thái đổi.
    /// </summary>
    Task<bool> SetNegativeGemRestrictionAsync(Guid accountId, bool restricted, CancellationToken ct);
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
