// <copyright file="EnvironmentNameGrammarTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The environment-name grammar (H18 piece 3 prerequisite): an environment name becomes half the composite
/// <c>(environment, runId)</c> primary key in every backend, including the delimited-key backends (Redis key
/// segments, NATS KV keys, Azure Table partition keys), so an unconstrained name is a key-aliasing surface.
/// The grammar is 1 to 63 characters of lowercase ASCII letters, digits and hyphen, not beginning or ending
/// with a hyphen. It is enforced at the in-process construction leaf (the <c>Draft</c> factories) and by the
/// contract's <c>EnvironmentName</c> schema at the HTTP ingress.
/// </summary>
[TestClass]
public sealed class EnvironmentNameGrammarTests
{
    [TestMethod]
    [DataRow("Production", DisplayName = "uppercase")]
    [DataRow("we:ird", DisplayName = "colon, the Redis key delimiter")]
    [DataRow("prod env", DisplayName = "space")]
    [DataRow("prod/1", DisplayName = "slash, forbidden in Azure Table keys")]
    [DataRow("prod.env", DisplayName = "dot, the NATS subject token separator")]
    [DataRow("-prod", DisplayName = "leading hyphen")]
    [DataRow("prod-", DisplayName = "trailing hyphen")]
    [DataRow("", DisplayName = "empty")]
    public void Drafting_an_environment_with_a_name_outside_the_grammar_is_refused(string name)
    {
        Should.Throw<ArgumentException>(() => Environment.Draft(name, null, null, default).Dispose());
    }

    [TestMethod]
    public void A_name_longer_than_sixty_three_characters_is_refused()
    {
        Should.Throw<ArgumentException>(() => Environment.Draft(new string('a', 64), null, null, default).Dispose());
    }

    [TestMethod]
    [DataRow("We:Ird", DisplayName = "colon and uppercase")]
    [DataRow("sys tem", DisplayName = "space")]
    public void Drafting_a_platform_environment_with_a_name_outside_the_grammar_is_refused(string name)
    {
        Should.Throw<ArgumentException>(() => Environment.DraftPlatform(name, null, null, default).Dispose());
    }

    [TestMethod]
    [DataRow("production")]
    [DataRow("dev-1")]
    [DataRow("a", DisplayName = "single character")]
    [DataRow("system")]
    public void Drafting_an_environment_with_a_conforming_name_succeeds(string name)
    {
        Should.NotThrow(() => Environment.Draft(name, null, null, default).Dispose());
    }

    [TestMethod]
    public void A_sixty_three_character_name_is_accepted()
    {
        Should.NotThrow(() => Environment.Draft(new string('a', 63), null, null, default).Dispose());
    }
}