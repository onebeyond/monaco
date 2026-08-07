using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Monaco.Template.Backend.Common.Observability;

internal static class HmacSha256Identity
{
	private static readonly byte[] VersionPrefix = "user.id:v1"u8.ToArray();

	internal static string Compute(byte[] keyBytes, string issuer, string subject)
	{
		var issBytes = Encoding.UTF8.GetBytes(issuer);
		var subBytes = Encoding.UTF8.GetBytes(subject);

		var preimage = new byte[VersionPrefix.Length + 4 + issBytes.Length + 4 + subBytes.Length];
		VersionPrefix.CopyTo(preimage, 0);
		WriteLengthPrefixed(preimage,
							WriteLengthPrefixed(preimage,
												VersionPrefix.Length,
												issBytes),
							subBytes);

		return EncodeBase64Url(HMACSHA256.HashData(keyBytes, preimage));
	}

	internal static bool TryDecodeKey(string? keyValue, out byte[]? keyBytes, out string? diagnosticCode)
	{
		keyBytes = null;
		diagnosticCode = null;

		if (string.IsNullOrEmpty(keyValue) || keyValue.Contains('-') || keyValue.Contains('_') || keyValue.Any(char.IsWhiteSpace))
			return Fail(out diagnosticCode);

		byte[] decoded;
		try
		{
			decoded = Convert.FromBase64String(keyValue);
		}
		catch (FormatException)
		{
			return Fail(out diagnosticCode);
		}

		if (decoded.Length < 32)
			return Fail(out diagnosticCode);

		keyBytes = decoded;
		return true;
	}

	private static int WriteLengthPrefixed(byte[] buffer, int offset, byte[] data)
	{
		BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), (uint)data.Length);
		data.CopyTo(buffer, offset + 4);
		return offset + 4 + data.Length;
	}

	private static bool Fail(out string? diagnosticCode)
	{
		diagnosticCode = ObservabilityConfigurationDiagnosticCodes.InvalidHmacKey;
		return false;
	}

	private static string EncodeBase64Url(byte[] data) =>
		Convert.ToBase64String(data)
			   .TrimEnd('=')
			   .Replace('+', '-')
			   .Replace('/', '_');
}