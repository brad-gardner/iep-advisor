using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace IepAssistant.Services.Implementations;

public class MfaSecretProtector
{
    private readonly byte[] _key;

    public MfaSecretProtector(IConfiguration configuration)
    {
        var keyString = configuration["Security:EncryptionKey"]
            ?? throw new InvalidOperationException("Security:EncryptionKey not configured");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
    }

    public string Protect(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    public string Unprotect(string ciphertext)
    {
        var fullBytes = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = fullBytes[..16];
        var cipher = fullBytes[16..];
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
