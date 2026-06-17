// <copyright file="ControlPlaneAntiForgeryTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the BFF anti-forgery predicate (<see cref="ControlPlaneAntiForgery.RequiresAntiForgery"/>): a
/// cookie-authenticated, state-changing call to the guarded API must carry the anti-forgery header.
/// </summary>
[TestClass]
public class ControlPlaneAntiForgeryTests
{
    private const string Cookie = "arazzo.session";
    private const string Header = ControlPlaneAntiForgery.DefaultHeaderName;
    private const string Prefix = ControlPlaneAntiForgery.DefaultApiPathPrefix;

    [TestMethod]
    public void Safe_methods_never_require_the_header()
        => Requires("GET", "/arazzo/v1/catalog", cookie: true, header: false).ShouldBeFalse();

    [TestMethod]
    public void Cookie_state_change_without_the_header_is_rejected()
        => Requires("POST", "/arazzo/v1/catalog", cookie: true, header: false).ShouldBeTrue();

    [TestMethod]
    public void Cookie_state_change_with_the_header_passes()
        => Requires("POST", "/arazzo/v1/catalog", cookie: true, header: true).ShouldBeFalse();

    [TestMethod]
    public void Delete_and_purge_are_also_guarded()
    {
        Requires("DELETE", "/arazzo/v1/runs/abc", cookie: true, header: false).ShouldBeTrue();
        Requires("PURGE", "/arazzo/v1/runs", cookie: true, header: false).ShouldBeTrue();
    }

    [TestMethod]
    public void Without_the_session_cookie_a_caller_is_exempt()   // bearer / mTLS clients
        => Requires("POST", "/arazzo/v1/catalog", cookie: false, header: false).ShouldBeFalse();

    [TestMethod]
    public void Outside_the_api_path_a_request_is_exempt()        // e.g. the BFF's own POST /logout
        => Requires("POST", "/logout", cookie: true, header: false).ShouldBeFalse();

    private static bool Requires(string method, string path, bool cookie, bool header)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (cookie)
        {
            context.Request.Headers.Cookie = $"{Cookie}=opaque-session-value";
        }

        if (header)
        {
            context.Request.Headers[Header] = "1";
        }

        return ControlPlaneAntiForgery.RequiresAntiForgery(context.Request, Cookie, Header, Prefix);
    }
}