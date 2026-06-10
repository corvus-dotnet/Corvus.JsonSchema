// <copyright file="Authentication.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Corvus.Text.Json;
using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli;

/// <summary>A persisted set of OAuth2 tokens for a given authority/client.</summary>
internal sealed record TokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    string Authority,
    string ClientId,
    string Scope);

/// <summary>The OAuth2/OIDC configuration an interactive login needs (deployment-chosen).</summary>
internal sealed record OAuthConfig(string Authority, string ClientId, string Scope);

/// <summary>
/// The interactive OAuth2 flows, delegated to Duende.IdentityModel: loopback authorization-code + PKCE
/// (via <see cref="OidcClient"/>) and the device authorization grant, plus token refresh.
/// </summary>
internal static class OAuthFlows
{
    public static Task<TokenSet> LoginAsync(OAuthConfig config, bool useDeviceCode, CancellationToken cancellationToken)
        => useDeviceCode
            ? DeviceCodeAsync(config, cancellationToken)
            : LoopbackPkceAsync(config, cancellationToken);

    public static async Task<TokenSet?> RefreshAsync(TokenSet existing, CancellationToken cancellationToken)
    {
        if (existing.RefreshToken is not { } refreshToken)
        {
            return null;
        }

        using var http = new HttpClient();
        DiscoveryDocumentResponse discovery = await http.GetDiscoveryDocumentAsync(existing.Authority, cancellationToken).ConfigureAwait(false);
        if (discovery.IsError)
        {
            return null;
        }

        TokenResponse token = await http.RequestRefreshTokenAsync(
            new RefreshTokenRequest
            {
                Address = discovery.TokenEndpoint,
                ClientId = existing.ClientId,
                RefreshToken = refreshToken,
            },
            cancellationToken).ConfigureAwait(false);

        return token.IsError
            ? null
            : ToTokenSet(token, new OAuthConfig(existing.Authority, existing.ClientId, existing.Scope), token.RefreshToken ?? existing.RefreshToken);
    }

    private static async Task<TokenSet> LoopbackPkceAsync(OAuthConfig config, CancellationToken cancellationToken)
    {
        using var browser = new LoopbackBrowser();
        var client = new OidcClient(new OidcClientOptions
        {
            Authority = config.Authority,
            ClientId = config.ClientId,
            Scope = config.Scope,
            RedirectUri = browser.RedirectUri,
            Browser = browser,
        });

        LoginResult result = await client.LoginAsync(new LoginRequest(), cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            throw new InvalidOperationException($"Sign-in failed: {result.Error}");
        }

        return new TokenSet(
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiration,
            config.Authority,
            config.ClientId,
            config.Scope);
    }

    private static async Task<TokenSet> DeviceCodeAsync(OAuthConfig config, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        DiscoveryDocumentResponse discovery = await http.GetDiscoveryDocumentAsync(config.Authority, cancellationToken).ConfigureAwait(false);
        if (discovery.IsError)
        {
            throw new InvalidOperationException($"OIDC discovery failed: {discovery.Error}");
        }

        DeviceAuthorizationResponse authorization = await http.RequestDeviceAuthorizationAsync(
            new DeviceAuthorizationRequest { Address = discovery.DeviceAuthorizationEndpoint, ClientId = config.ClientId, Scope = config.Scope },
            cancellationToken).ConfigureAwait(false);
        if (authorization.IsError)
        {
            throw new InvalidOperationException($"Device authorization failed: {authorization.Error}");
        }

        Console.Error.WriteLine("To sign in, visit:");
        Console.Error.WriteLine($"  {authorization.VerificationUri}");
        Console.Error.WriteLine($"and enter the code: {authorization.UserCode}");
        if (authorization.VerificationUriComplete is { } complete)
        {
            Console.Error.WriteLine($"(or open directly: {complete})");
        }

        int interval = authorization.Interval;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);

            TokenResponse token = await http.RequestDeviceTokenAsync(
                new DeviceTokenRequest { Address = discovery.TokenEndpoint, ClientId = config.ClientId, DeviceCode = authorization.DeviceCode! },
                cancellationToken).ConfigureAwait(false);

            if (!token.IsError)
            {
                return ToTokenSet(token, config, token.RefreshToken);
            }

            switch (token.Error)
            {
                case "authorization_pending":
                    break;
                case "slow_down":
                    interval += 5;
                    break;
                default:
                    throw new InvalidOperationException($"Device sign-in failed: {token.Error}");
            }
        }
    }

    private static TokenSet ToTokenSet(TokenResponse token, OAuthConfig config, string? refreshToken)
        => new(
            token.AccessToken ?? throw new InvalidOperationException("The token response contained no access token."),
            refreshToken,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
            config.Authority,
            config.ClientId,
            config.Scope);
}

/// <summary>
/// An <see cref="IBrowser"/> that completes a loopback redirect (RFC 8252): it listens on a free
/// <c>127.0.0.1</c> port, opens the system browser at the authorize URL, and captures the redirect.
/// </summary>
internal sealed class LoopbackBrowser : IBrowser, IDisposable
{
    private readonly HttpListener listener;

    public LoopbackBrowser()
    {
        int port = GetFreePort();
        this.RedirectUri = $"http://127.0.0.1:{port}/";
        this.listener = new HttpListener();
        this.listener.Prefixes.Add(this.RedirectUri);
    }

    public string RedirectUri { get; }

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.listener.Start();
        try
        {
            Console.Error.WriteLine("Opening your browser to sign in. If it does not open, visit:");
            Console.Error.WriteLine($"  {options.StartUrl}");
            OpenBrowser(options.StartUrl);

            using (cancellationToken.Register(this.listener.Stop))
            {
                HttpListenerContext context = await this.listener.GetContextAsync().ConfigureAwait(false);
                string responseUrl = context.Request.Url?.ToString() ?? string.Empty;
                await WriteClosePageAsync(context).ConfigureAwait(false);
                return new BrowserResult { ResultType = BrowserResultType.Success, Response = responseUrl };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
        }
        finally
        {
            this.listener.Stop();
        }
    }

    public void Dispose() => ((IDisposable)this.listener).Dispose();

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task WriteClosePageAsync(HttpListenerContext context)
    {
        byte[] payload = Encoding.UTF8.GetBytes("<html><body><p>Signed in. You can close this tab and return to the terminal.</p></body></html>");
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
        context.Response.OutputStream.Close();
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex)
        {
            // The URL was already printed; the operator can open it manually.
            Debug.WriteLine(ex);
        }
    }
}

/// <summary>A small on-disk cache of the most recent <see cref="TokenSet"/>, under the user's app-data folder.</summary>
internal static class TokenCache
{
    private static string CacheFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "arazzo-runs",
        "token.json");

    public static TokenSet? Load()
    {
        string path = CacheFilePath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(File.ReadAllBytes(path));
            JsonElement root = doc.RootElement;
            return new TokenSet(
                root.GetProperty("accessToken"u8).GetString()!,
                root.TryGetProperty("refreshToken"u8, out JsonElement r) ? r.GetString() : null,
                DateTimeOffset.Parse(root.GetProperty("expiresAtUtc"u8).GetString()!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
                root.GetProperty("authority"u8).GetString()!,
                root.GetProperty("clientId"u8).GetString()!,
                root.GetProperty("scope"u8).GetString()!);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    public static void Save(TokenSet token)
    {
        string path = CacheFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("accessToken"u8, token.AccessToken);
            if (token.RefreshToken is { } refreshToken)
            {
                writer.WriteString("refreshToken"u8, refreshToken);
            }

            writer.WriteString("expiresAtUtc"u8, token.ExpiresAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteString("authority"u8, token.Authority);
            writer.WriteString("clientId"u8, token.ClientId);
            writer.WriteString("scope"u8, token.Scope);
            writer.WriteEndObject();
        }

        File.WriteAllBytes(path, buffer.WrittenSpan.ToArray());
    }

    public static void Clear()
    {
        string path = CacheFilePath;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

/// <summary>Resolves the access token a command should send: explicit flag, environment, then the cache (refreshing if stale).</summary>
internal static class TokenSource
{
    public static async ValueTask<string?> ResolveAsync(string? explicitToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(explicitToken))
        {
            return explicitToken;
        }

        if (Environment.GetEnvironmentVariable("ARAZZO_RUNS_TOKEN") is { Length: > 0 } fromEnvironment)
        {
            return fromEnvironment;
        }

        if (TokenCache.Load() is not { } cached)
        {
            return null;
        }

        if (cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return cached.AccessToken;
        }

        try
        {
            if (await OAuthFlows.RefreshAsync(cached, cancellationToken).ConfigureAwait(false) is { } refreshed)
            {
                TokenCache.Save(refreshed);
                return refreshed.AccessToken;
            }
        }
        catch (Exception ex)
        {
            // Fall back to the (stale) cached token; the server will reject it and the operator can re-login.
            Debug.WriteLine(ex);
        }

        return cached.AccessToken;
    }
}