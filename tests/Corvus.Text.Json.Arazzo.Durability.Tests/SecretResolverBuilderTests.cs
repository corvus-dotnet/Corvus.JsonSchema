// <copyright file="SecretResolverBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="SecretResolverBuilder"/> — the composition seam that assembles the runner's resolver set.</summary>
[TestClass]
public sealed class SecretResolverBuilderTests
{
    [TestMethod]
    public void Builds_a_composite_that_dispatches_the_registered_built_in_schemes()
    {
        ISecretResolver resolver = new SecretResolverBuilder()
            .AddEnvironmentAndFile()
            .Build();

        resolver.CanResolve(SecretScheme.Environment).ShouldBeTrue();
        resolver.CanResolve(SecretScheme.File).ShouldBeTrue();
        resolver.CanResolve(SecretScheme.HashiCorpVault).ShouldBeFalse();
    }

    [TestMethod]
    public void AddEnvironment_and_AddFile_register_only_their_own_scheme()
    {
        new SecretResolverBuilder().AddEnvironment().Build().CanResolve(SecretScheme.File).ShouldBeFalse();
        new SecretResolverBuilder().AddFile().Build().CanResolve(SecretScheme.Environment).ShouldBeFalse();
    }

    [TestMethod]
    public void A_custom_resolver_can_be_registered_via_Add()
    {
        ISecretResolver resolver = new SecretResolverBuilder()
            .Add(new EnvSecretResolver())
            .Build();

        resolver.CanResolve(SecretScheme.Environment).ShouldBeTrue();
    }

    [TestMethod]
    public void Building_with_no_resolver_registered_throws()
    {
        Should.Throw<InvalidOperationException>(() => new SecretResolverBuilder().Build());
    }

    [TestMethod]
    public void Registering_two_resolvers_for_the_same_scheme_throws_rather_than_silently_shadowing()
    {
        SecretResolverBuilder builder = new SecretResolverBuilder()
            .AddEnvironment()
            .AddEnvironment();

        Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [TestMethod]
    public void Add_rejects_a_null_resolver()
    {
        Should.Throw<ArgumentNullException>(() => new SecretResolverBuilder().Add(null!));
    }
}