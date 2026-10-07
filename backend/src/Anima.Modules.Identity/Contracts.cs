using Anima.SharedKernel;

namespace Anima.Identity.Contracts;

/// <summary>Phát đồng bộ trong transaction đăng ký. Người nhận: Fairness (tạo seed), Wallet (thưởng đăng nhập), Gacha (gói chào mừng).</summary>
public sealed record AccountRegistered(Guid AccountId, string LegalCountry, string Locale) : IDomainEvent;

public sealed record AccountInfo(Guid Id, string Status, bool PhoneVerified, string Locale, string Timezone, string LegalCountry, string? RestrictionReason, DateTimeOffset CreatedAt);

public interface IIdentityApi
{
    Task<AccountInfo> GetAsync(Guid accountId, CancellationToken ct);
}
