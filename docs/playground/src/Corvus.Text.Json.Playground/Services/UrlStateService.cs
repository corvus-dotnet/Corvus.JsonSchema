using System.IO.Compression;
using System.Text;
using Microsoft.JSInterop;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Encodes/decodes playground state (schema + user code) in the URL hash
/// for shareable links. Uses deflate compression + base64url encoding.
/// </summary>
public class UrlStateService
{
    private readonly IJSRuntime js;

    public UrlStateService(IJSRuntime js)
    {
        this.js = js;
    }

    /// <summary>
    /// Encode schema and user code into a URL hash fragment.
    /// </summary>
    public string Encode(string schema, string userCode)
    {
        string combined = $"{schema}\0{userCode}";
        byte[] bytes = Encoding.UTF8.GetBytes(combined);

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize))
        {
            deflate.Write(bytes, 0, bytes.Length);
        }

        return Base64UrlEncode(output.ToArray());
    }

    /// <summary>
    /// Decode schema and user code from a URL hash fragment.
    /// </summary>
    public (string Schema, string UserCode)? Decode(string hash)
    {
        try
        {
            byte[] compressed = Base64UrlDecode(hash);
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);

            string combined = Encoding.UTF8.GetString(output.ToArray());
            int sep = combined.IndexOf('\0');
            if (sep < 0)
            {
                return (combined, string.Empty);
            }

            return (combined[..sep], combined[(sep + 1)..]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Read the current URL hash fragment.
    /// </summary>
    public async Task<string?> GetHashAsync()
    {
        return await js.InvokeAsync<string?>("eval", "location.hash?.substring(1) || null");
    }

    /// <summary>
    /// Set the URL hash fragment without triggering navigation.
    /// </summary>
    public async Task SetHashAsync(string hash)
    {
        await js.InvokeVoidAsync("eval", $"history.replaceState(null, '', '#' + '{hash}')");
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string data)
    {
        string base64 = data.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}
