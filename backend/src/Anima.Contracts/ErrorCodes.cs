namespace Anima.Contracts
{
    /// <summary>Mã lỗi nghiệp vụ. Tên khớp docs/BDD_ANIMA.md; không đổi theo ngôn ngữ (BR-I18N-04).</summary>
    public static class ErrorCodes
    {
        // Chung
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string NotFound = "NOT_FOUND";
        public const string IdempotencyKeyRequired = "IDEMPOTENCY_KEY_REQUIRED";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string FeatureNotAvailableInRegion = "FEATURE_NOT_AVAILABLE_IN_REGION";
        public const string PlatformNotSupported = "PLATFORM_NOT_SUPPORTED";

        // Tài khoản
        public const string AgeBelowMinimum = "AGE_BELOW_MINIMUM";
        public const string RegionBlocked = "REGION_BLOCKED";
        public const string EmailAlreadyUsed = "EMAIL_ALREADY_USED";
        public const string PhoneAlreadyUsed = "PHONE_ALREADY_USED";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string LoginLocked = "LOGIN_LOCKED";
        public const string OtpInvalid = "OTP_INVALID";
        public const string OtpExpired = "OTP_EXPIRED";
        public const string OtpLocked = "OTP_LOCKED";
        public const string OtpDailyLimit = "OTP_DAILY_LIMIT";
        public const string PhoneVerificationRequired = "PHONE_VERIFICATION_REQUIRED";
        public const string AccountRestricted = "ACCOUNT_RESTRICTED";

        // Ví
        public const string InsufficientBalance = "INSUFFICIENT_BALANCE";
        public const string InvalidAmount = "INVALID_AMOUNT";
        public const string CoinToGemDailyLimit = "COIN_TO_GEM_DAILY_LIMIT";

        // Pack
        public const string PackNotOnSale = "PACK_NOT_ON_SALE";
        public const string PackNotOwned = "PACK_NOT_OWNED";
        public const string PackAlreadyOpened = "PACK_ALREADY_OPENED";
        public const string PackSupplyExhausted = "PACK_SUPPLY_EXHAUSTED";

        // Công bằng
        public const string SeedNotRevealed = "SEED_NOT_REVEALED";

        // Lò rèn
        public const string ForgeRequiresTwoCards = "FORGE_REQUIRES_TWO_CARDS";
        public const string ForgeDuplicateInput = "FORGE_DUPLICATE_INPUT";
        public const string CardNotForgeable = "CARD_NOT_FORGEABLE";
        public const string ForgeDailyLimit = "FORGE_DAILY_LIMIT";
        public const string SealedCardNotTradable = "SEALED_CARD_NOT_TRADABLE";
        public const string SealedCardAlreadyRevealed = "SEALED_CARD_ALREADY_REVEALED";

        // Quản trị
        public const string Forbidden = "FORBIDDEN";
        public const string SelfApprovalForbidden = "SELF_APPROVAL_FORBIDDEN";
        public const string DropRateSumInvalid = "DROP_RATE_SUM_INVALID";
        public const string VersionLocked = "VERSION_LOCKED";
        public const string GemGrantForbidden = "GEM_GRANT_FORBIDDEN";
        public const string TicketRequired = "TICKET_REQUIRED";
        public const string AuditImmutable = "AUDIT_IMMUTABLE";
        public const string CardHasInstances = "CARD_HAS_INSTANCES";
        public const string CardPublishedImmutable = "CARD_PUBLISHED_IMMUTABLE";
        public const string InvalidState = "INVALID_STATE";
        public const string SelfRoleChangeForbidden = "SELF_ROLE_CHANGE_FORBIDDEN";
        public const string ReasonRequired = "REASON_REQUIRED";
        public const string PaymentUnavailable = "PAYMENT_UNAVAILABLE";
        public const string InvalidSignature = "INVALID_SIGNATURE";
        public const string PaymentAmountMismatch = "PAYMENT_AMOUNT_MISMATCH";
        public const string DeckInvalid = "DECK_INVALID";
        public const string DeckLimitReached = "DECK_LIMIT_REACHED";
        public const string AttackNotAllowed = "ATTACK_NOT_ALLOWED";
        public const string DirectAttackNotAllowed = "DIRECT_ATTACK_NOT_ALLOWED";
        public const string MulliganUsed = "MULLIGAN_USED";
        public const string NotYourTurn = "NOT_YOUR_TURN";
        public const string InsufficientResonance = "INSUFFICIENT_RESONANCE";
        public const string MatchFinished = "MATCH_FINISHED";
        public const string RateLimited = "RATE_LIMITED";
        public const string GuardianConsentRequired = "GUARDIAN_CONSENT_REQUIRED";
        public const string GuardianTokenInvalid = "GUARDIAN_TOKEN_INVALID";
        public const string QuestExpired = "QUEST_EXPIRED";
        public const string QuestNotAvailable = "QUEST_NOT_AVAILABLE";
        public const string QuestNotComplete = "QUEST_NOT_COMPLETE";
        public const string QuestAlreadyClaimed = "QUEST_ALREADY_CLAIMED";

        // Thẻ
        public const string CardNotOwned = "CARD_NOT_OWNED";
        public const string CardLocked = "CARD_LOCKED";
        public const string CardNotInAccount = "CARD_NOT_IN_ACCOUNT";
        public const string CardNotAvailable = "CARD_NOT_AVAILABLE";
    }

    public static class Currencies
    {
        public const string Gem = "GEM";
        public const string Coin = "COIN";
        public static bool IsValid(string? c) => c == Gem || c == Coin;
    }

    /// <summary>Rarity theo thứ tự tăng dần; chỉ số = thứ tự lật (BR-PACK-06).</summary>
    public static class Rarities
    {
        public static readonly string[] Order = { "common", "uncommon", "rare", "epic", "legendary", "secret" };
        public static int Index(string rarity) => System.Array.IndexOf(Order, rarity);
        public static bool IsLegendaryPlus(string rarity) => Index(rarity) >= 4;
        public static bool IsEpicPlus(string rarity) => Index(rarity) >= 3;
    }

    public static class CardTypes
    {
        public const string Anima = "anima";
        public const string Echo = "echo";
        public const string Seal = "seal";
    }

    public static class Elements
    {
        /// <summary>Vòng sinh (BR-ELM-01): hệ trước sinh ra hệ sau.</summary>
        public static readonly string[] Cycle = { "Umbryx", "Pyraxis", "Aqualis", "Terrakin", "Ventara", "Voltaris", "Luminara" };
        public const string Nihilum = "Nihilum";
        public static readonly string[] All = { "Umbryx", "Pyraxis", "Aqualis", "Terrakin", "Ventara", "Voltaris", "Luminara", "Nihilum" };
    }

    public static class Locales
    {
        public static readonly string[] Supported = { "vi", "en", "zh-Hans", "zh-Hant" };
        public const string Fallback = "en";
        public static string Normalize(string? l) => l != null && System.Array.IndexOf(Supported, l) >= 0 ? l : Fallback;
    }
}
