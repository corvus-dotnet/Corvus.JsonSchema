// <copyright file="SecretRefTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="SecretRef"/> parsing of <c>scheme://locator[#version]</c> references.</summary>
[TestClass]
public sealed class SecretRefTests
{
    [TestMethod]
    [DataRow("keyvault://my-secret", SecretScheme.KeyVault, "my-secret", null)]
    [DataRow("keyvault:my-secret", SecretScheme.KeyVault, "my-secret", null)]
    [DataRow("awssm://prod/petstore/apikey", SecretScheme.AwsSecretsManager, "prod/petstore/apikey", null)]
    [DataRow("vault://kv/data/petstore#client", SecretScheme.HashiCorpVault, "kv/data/petstore", "client")]
    [DataRow("env://PETSTORE_KEY", SecretScheme.Environment, "PETSTORE_KEY", null)]
    [DataRow("env:PETSTORE_KEY", SecretScheme.Environment, "PETSTORE_KEY", null)]
    [DataRow("file:///run/secrets/token", SecretScheme.File, "/run/secrets/token", null)]
    [DataRow("keyvault://my-secret#7", SecretScheme.KeyVault, "my-secret", "7")]
    public void Parses_well_formed_references(string reference, SecretScheme scheme, string locator, string? version)
    {
        SecretRef secretRef = SecretRef.Parse(reference);
        secretRef.Scheme.ShouldBe(scheme);
        secretRef.Locator.ShouldBe(locator);
        secretRef.Version.ShouldBe(version);
        secretRef.Raw.ShouldBe(reference);
    }

    [TestMethod]
    [DataRow("no-scheme")]
    [DataRow("unknown://thing")]
    [DataRow("keyvault://")]
    [DataRow("keyvault://secret#")]
    [DataRow("keyvault://#version")]
    public void Rejects_malformed_references(string reference)
    {
        SecretRef.TryParse(reference, out _).ShouldBeFalse();
        Should.Throw<FormatException>(() => SecretRef.Parse(reference));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Rejects_null_or_empty_references(string? reference)
    {
        SecretRef.TryParse(reference, out _).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => SecretRef.Parse(reference!));
    }

    [TestMethod]
    public void Equality_is_by_raw_value()
    {
        SecretRef a = SecretRef.Parse("keyvault://secret#1");
        SecretRef b = SecretRef.Parse("keyvault://secret#1");
        SecretRef c = SecretRef.Parse("keyvault://secret#2");
        a.ShouldBe(b);
        a.ShouldNotBe(c);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}