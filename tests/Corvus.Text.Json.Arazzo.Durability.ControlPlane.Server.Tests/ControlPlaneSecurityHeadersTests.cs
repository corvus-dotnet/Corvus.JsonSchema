// <copyright file="ControlPlaneSecurityHeadersTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the browser security headers (ADR 0073): every response a host serves carries them, a page cannot be framed,
/// configuration adds origins and never weakens the policy, and HSTS is sent only where a browser should keep it.
/// </summary>
[TestClass]
public sealed class ControlPlaneSecurityHeadersTests
{
    [TestMethod]
    public void The_policy_with_no_sources_is_the_documented_default()
        => new ControlPlaneSecurityHeaders(new ControlPlaneSecurityHeadersOptions()).ContentSecurityPolicy
            .ShouldBe(ControlPlaneSecurityHeaders.DefaultContentSecurityPolicy);

    [TestMethod]
    [DataRow("/page")]
    [DataRow("/arazzo/v1/catalog")]
    [DataRow("/nowhere")]
    public async Task Every_response_carries_the_headers(string path)
    {
        await using WebApplication app = await StartAsync();
        using HttpResponseMessage response = await app.GetTestClient().GetAsync(path);

        Header(response, "Content-Security-Policy").ShouldBe(ControlPlaneSecurityHeaders.DefaultContentSecurityPolicy);
        Header(response, "X-Frame-Options").ShouldBe("DENY");
        Header(response, "X-Content-Type-Options").ShouldBe("nosniff");
        Header(response, "Referrer-Policy").ShouldBe("no-referrer");
        Header(response, "Cross-Origin-Opener-Policy").ShouldBe("same-origin-allow-popups");
        Header(response, "Cross-Origin-Resource-Policy").ShouldBe("same-origin");
    }

    [TestMethod]
    public async Task The_headers_survive_an_exception_handler_that_clears_the_response()
    {
        await using WebApplication app = await StartAsync();
        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/throws");

        ((int)response.StatusCode).ShouldBe(StatusCodes.Status500InternalServerError);
        (await response.Content.ReadAsStringAsync()).ShouldBe("handled");
        Header(response, "Content-Security-Policy").ShouldBe(ControlPlaneSecurityHeaders.DefaultContentSecurityPolicy);
        Header(response, "X-Frame-Options").ShouldBe("DENY");
    }

    [TestMethod]
    public async Task An_endpoint_that_sets_a_policy_of_its_own_keeps_it()
    {
        await using WebApplication app = await StartAsync();
        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/own-policy");

        Header(response, "Content-Security-Policy").ShouldBe("default-src 'none'");
        Header(response, "X-Frame-Options").ShouldBe("DENY");
    }

    [TestMethod]
    public async Task Configured_origins_join_their_directives_and_an_allowed_framer_drops_x_frame_options()
    {
        await using WebApplication app = await StartAsync(options =>
        {
            options.ConnectSources.Add("https://api.example.com");
            options.FormActionSources.Add("https://id.example.com:8443/");
            options.FrameAncestors.Add("https://portal.example.com");
        });
        using HttpResponseMessage response = await app.GetTestClient().GetAsync("/page");

        Header(response, "Content-Security-Policy").ShouldBe(
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; "
            + "connect-src 'self' https://api.example.com; object-src 'none'; base-uri 'none'; "
            + "form-action 'self' https://id.example.com:8443; frame-ancestors https://portal.example.com");
        response.Headers.Contains("X-Frame-Options").ShouldBeFalse();
    }

    [TestMethod]
    [DataRow("'unsafe-inline'")]
    [DataRow("*")]
    [DataRow("https://*.example.com")]
    [DataRow("https://a.example.com/path")]
    [DataRow("https://a.example.com?x=1")]
    [DataRow("https://a.example.com#x")]
    [DataRow("https://user@a.example.com")]
    [DataRow("https://a.example.com; script-src *")]
    [DataRow("javascript:alert(1)")]
    [DataRow("ftp://a.example.com")]
    [DataRow("a.example.com")]
    public void A_source_that_is_not_one_origin_is_refused(string source)
    {
        foreach (Action<ControlPlaneSecurityHeadersOptions> add in new Action<ControlPlaneSecurityHeadersOptions>[]
        {
            o => o.ConnectSources.Add(source),
            o => o.FormActionSources.Add(source),
            o => o.FrameAncestors.Add(source),
        })
        {
            var options = new ControlPlaneSecurityHeadersOptions();
            add(options);
            ArgumentException refused = Should.Throw<ArgumentException>(() => new ControlPlaneSecurityHeaders(options));
            refused.Message.ShouldContain(source);
        }
    }

    [TestMethod]
    [DataRow("https://control.example.com", true)]
    [DataRow("http://control.example.com", false)]
    [DataRow("https://localhost", false)]
    [DataRow("https://127.0.0.1", false)]
    [DataRow("https://[::1]", false)]
    public async Task Hsts_is_sent_over_https_to_a_host_that_is_not_loopback(string baseAddress, bool expected)
    {
        await using WebApplication app = await StartAsync();
        HttpClient client = app.GetTestClient();
        client.BaseAddress = new Uri(baseAddress);
        using HttpResponseMessage response = await client.GetAsync("/page");

        if (expected)
        {
            Header(response, "Strict-Transport-Security").ShouldBe("max-age=31536000");
        }
        else
        {
            response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task Hsts_follows_its_options()
    {
        await using WebApplication subdomains = await StartAsync(o => { o.HstsMaxAge = TimeSpan.FromDays(1); o.HstsIncludeSubDomains = true; });
        HttpClient client = subdomains.GetTestClient();
        client.BaseAddress = new Uri("https://control.example.com");
        using (HttpResponseMessage response = await client.GetAsync("/page"))
        {
            Header(response, "Strict-Transport-Security").ShouldBe("max-age=86400; includeSubDomains");
        }

        await using WebApplication off = await StartAsync(o => o.HstsMaxAge = TimeSpan.Zero);
        client = off.GetTestClient();
        client.BaseAddress = new Uri("https://control.example.com");
        using (HttpResponseMessage response = await client.GetAsync("/page"))
        {
            response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
        }
    }

    private static string Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out IEnumerable<string>? values) || response.Content.Headers.TryGetValues(name, out values)
            ? string.Join(",", values)
            : throw new ShouldAssertException($"The response carries no {name} header.");

    private static async Task<WebApplication> StartAsync(Action<ControlPlaneSecurityHeadersOptions>? configure = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddArazzoSecurityHeaders(configure);

        WebApplication app = builder.Build();

        // The production exception handler clears the response, headers included, before it writes the error.
        app.UseExceptionHandler(error => error.Run(async context => await context.Response.WriteAsync("handled")));
        app.MapGet("/page", () => Results.Content("<!doctype html><title>page</title>", "text/html"));
        app.MapGet("/arazzo/v1/catalog", () => Results.Json(new { items = Array.Empty<string>() }));
        app.MapGet("/throws", static string () => throw new InvalidOperationException("boom"));
        app.MapGet("/own-policy", (HttpContext context) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'";
            return Results.Text("own");
        });
        await app.StartAsync();
        return app;
    }
}