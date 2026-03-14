using System.Security.Cryptography;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

public class TotpService : ITotpService
{
    private const int TimeStep = 30;
    private const int CodeDigits = 6;
    private const int SecretLength = 20;
    private static readonly int[] Pow10 = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000 };

    public string GenerateSecret()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(SecretLength);
        return Base32Encode(secretBytes);
    }

    public string GenerateCode(string base32Secret, DateTimeOffset? timestamp = null)
    {
        var key = Base32Decode(base32Secret);
        var timeStep = GetTimestamp(timestamp);
        return ComputeCode(key, timeStep);
    }

    public bool ValidateCode(string base32Secret, string code, int driftSteps = 1)
    {
        var key = Base32Decode(base32Secret);
        var currentStep = GetTimestamp();

        for (var i = -driftSteps; i <= driftSteps; i++)
        {
            var testStep = currentStep + i;
            if (testStep < 0) continue;

            var testCode = ComputeCode(key, testStep);
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(testCode),
                System.Text.Encoding.UTF8.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    public long GetTimestamp(DateTimeOffset? timestamp = null)
    {
        var time = timestamp ?? DateTimeOffset.UtcNow;
        return time.ToUnixTimeSeconds() / TimeStep;
    }

    private static string ComputeCode(byte[] key, long timeStep)
    {
        var timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(timeBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % Pow10[CodeDigits];
        return otp.ToString().PadLeft(CodeDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new char[(data.Length * 8 + 4) / 5];
        var index = 0;
        int buffer = 0, bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result[index++] = alphabet[(buffer >> bitsLeft) & 0x1F];
            }
        }

        if (bitsLeft > 0)
        {
            result[index++] = alphabet[(buffer << (5 - bitsLeft)) & 0x1F];
        }

        return new string(result, 0, index);
    }

    private static byte[] Base32Decode(string base32)
    {
        var input = base32.TrimEnd('=').ToUpperInvariant();
        var output = new byte[input.Length * 5 / 8];
        int buffer = 0, bitsLeft = 0, index = 0;

        foreach (var c in input)
        {
            int value;
            if (c >= 'A' && c <= 'Z')
                value = c - 'A';
            else if (c >= '2' && c <= '7')
                value = c - '2' + 26;
            else
                throw new FormatException($"Invalid Base32 character: {c}");

            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output[index++] = (byte)(buffer >> bitsLeft);
            }
        }

        return output[..index];
    }
}
