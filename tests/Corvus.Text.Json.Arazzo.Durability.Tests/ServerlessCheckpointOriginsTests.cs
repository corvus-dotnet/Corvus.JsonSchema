// <copyright file="ServerlessCheckpointOriginsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves <see cref="ServerlessCheckpointOrigins"/>: a deployed function takes a checkpoint URL only from an origin
/// (scheme, host and port) it was deployed with, the list is required, and it has no wildcard (ADR 0059 decision 4).
/// </summary>
[TestClass]
public sealed class ServerlessCheckpointOriginsTests
{
    [TestMethod]
    public void A_url_at_a_listed_origin_is_allowed_whatever_its_path()
    {
        ServerlessCheckpointOrigins origins = ServerlessCheckpointOrigins.Parse("https://runner-a.example/checkpoints/; http://10.0.0.5:8199");

        origins.Count.ShouldBe(2);
        origins.Allows(new Uri("https://runner-a.example/")).ShouldBeTrue();
        origins.Allows(new Uri("https://RUNNER-A.example:443/other/path/")).ShouldBeTrue();
        origins.Allows(new Uri("http://10.0.0.5:8199/checkpoints/")).ShouldBeTrue();
    }

    [TestMethod]
    public void A_different_scheme_host_or_port_is_refused()
    {
        ServerlessCheckpointOrigins origins = ServerlessCheckpointOrigins.Parse("https://runner-a.example/");

        origins.Allows(new Uri("http://runner-a.example/")).ShouldBeFalse();
        origins.Allows(new Uri("https://runner-a.example:8443/")).ShouldBeFalse();
        origins.Allows(new Uri("https://runner-b.example/")).ShouldBeFalse();

        // Neither a suffix, a prefix, nor a sub-domain of a listed host is that host.
        origins.Allows(new Uri("https://runner-a.example.attacker.test/")).ShouldBeFalse();
        origins.Allows(new Uri("https://evil-runner-a.example/")).ShouldBeFalse();
        origins.Allows(new Uri("https://sub.runner-a.example/")).ShouldBeFalse();
    }

    [TestMethod]
    public void A_url_with_user_info_or_another_scheme_or_no_authority_is_refused()
    {
        ServerlessCheckpointOrigins origins = ServerlessCheckpointOrigins.Parse("https://runner-a.example/");

        // The classic confusion: the listed host as the user-info of another host, and the reverse.
        origins.Allows(new Uri("https://runner-a.example@attacker.test/")).ShouldBeFalse();
        origins.Allows(new Uri("https://attacker.test@runner-a.example/")).ShouldBeFalse();
        origins.Allows(new Uri("ftp://runner-a.example/")).ShouldBeFalse();
        origins.Allows(new Uri("file:///etc/passwd")).ShouldBeFalse();
        origins.Allows(new Uri("/relative", UriKind.Relative)).ShouldBeFalse();
    }

    [TestMethod]
    public void The_list_is_required_and_every_entry_must_be_an_absolute_http_url()
    {
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.Parse(null)).Message.ShouldContain(ServerlessCheckpointOrigins.SettingName);
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.Parse("  "));
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.Parse(" ; ; "));
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.Parse("https://runner-a.example/;*")).Message.ShouldContain("'*'");
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.Parse("runner-a.example"));
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.Parse("ftp://runner-a.example/"));
    }

    [TestMethod]
    public void A_deployers_function_settings_must_carry_the_list()
    {
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.RequireIn(null));
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.RequireIn(new Dictionary<string, string> { ["ARAZZO_SOURCE__echo"] = "https://echo.example" }));
        Should.Throw<FormatException>(() => ServerlessCheckpointOrigins.RequireIn(new Dictionary<string, string> { [ServerlessCheckpointOrigins.SettingName] = "not a url" }));

        Should.NotThrow(() => ServerlessCheckpointOrigins.RequireIn(new Dictionary<string, string> { [ServerlessCheckpointOrigins.SettingName] = "https://runner-a.example/" }));
    }
}