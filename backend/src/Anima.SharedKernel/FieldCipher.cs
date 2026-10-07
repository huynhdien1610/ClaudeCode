using System.Security.Cryptography;
using System.Text;

namespace Anima.SharedKernel;

/// <summary>
/// Mã hóa trường nhạy cảm (PII, server seed) ở tầng ứng dụng bằng AES-256-GCM, kèm HMAC để tra cứu/kiểm tra trùng (SAD 5.2, NFR-03).
/// Khóa lấy từ cấu hình (secret manager khi lên cloud). Môi trường Development có khóa mặc định để chạy ngay.
/// </summary>
public interface IFieldCipher
{
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] blob);
    byte[] LookupHash(string normalized);
}

public sealed class FieldCipher : IFieldCipher
{
    private readonly byte[] _encKey;
    private readonly byte[] _macKey;

    public FieldCipher(byte[] encKey, byte[] macKey)
    {
        if (encKey.Length != 32 || macKey.Length < 32) throw new ArgumentException("Keys must be 32 bytes");
        _encKey = encKey; _macKey = macKey;
    }

    public static FieldCipher FromConfig(IConfiguration c, bool isDevelopment)
    {
        var enc = c["Security:FieldKey"]; var mac = c["Security:LookupKey"];
        if (string.IsNullOrEmpty(enc) || string.IsNullOrEmpty(mac))
        {
            if (!isDevelopment) throw new InvalidOperationException("Security:FieldKey and Security:LookupKey must be configured outside Development");
            enc = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("anima-dev-field-key")));
            mac = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("anima-dev-lookup-key")));
        }
        return new FieldCipher(Convert.FromBase64String(enc), Convert.FromBase64String(mac));
    }

    public byte[] Encrypt(string plaintext)
    {
        var pt = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ct = new byte[pt.Length]; var tag = new byte[16];
        using var gcm = new AesGcm(_encKey, 16);
        gcm.Encrypt(nonce, pt, ct, tag);
        return [.. nonce, .. tag, .. ct];
    }

    public string Decrypt(byte[] blob)
    {
        var nonce = blob.AsSpan(0, 12); var tag = blob.AsSpan(12, 16); var ct = blob.AsSpan(28);
        var pt = new byte[ct.Length];
        using var gcm = new AesGcm(_encKey, 16);
        gcm.Decrypt(nonce, ct, tag, pt);
        return Encoding.UTF8.GetString(pt);
    }

    public byte[] LookupHash(string normalized) => HMACSHA256.HashData(_macKey, Encoding.UTF8.GetBytes(normalized));
}
