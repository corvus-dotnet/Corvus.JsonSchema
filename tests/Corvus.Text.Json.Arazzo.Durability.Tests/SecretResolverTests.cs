// <copyright file="SecretResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for the runner-side <see cref="ISecretResolver"/> implementations (env/file) and their dispatch.</summary>
[TestClass]
public sealed class SecretResolverTests
{
    [TestMethod]
    public async Task Env_resolver_reads_an_environment_variable()
    {
        const string name = "CORVUS_ARAZZO_TEST_SECRET_ENV";
        Environment.SetEnvironmentVariable(name, "s3cr3t");
        try
        {
            var resolver = new EnvSecretResolver();
            resolver.CanResolve(SecretScheme.Environment).ShouldBeTrue();
            resolver.CanResolve(SecretScheme.File).ShouldBeFalse();

            using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse($"env://{name}"), default);
            material.Reveal().ShouldBe("s3cr3t");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestMethod]
    public async Task Env_resolver_fails_for_a_missing_variable_or_a_version()
    {
        var resolver = new EnvSecretResolver();
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("env://CORVUS_ARAZZO_DEFINITELY_NOT_SET"), default));
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse("env://SOME_VAR#2"), default));
    }

    [TestMethod]
    public async Task File_resolver_reads_file_content_verbatim()
    {
        string path = Path.Combine(Path.GetTempPath(), $"corvus-secret-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("file-secret-bytes"));
        try
        {
            var resolver = new FileSecretResolver();
            resolver.CanResolve(SecretScheme.File).ShouldBeTrue();

            using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse($"file://{path}"), default);
            material.Reveal().ShouldBe("file-secret-bytes");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task File_resolver_fails_for_a_missing_file()
    {
        var resolver = new FileSecretResolver();
        string path = Path.Combine(Path.GetTempPath(), $"corvus-missing-{Guid.NewGuid():N}");
        await Should.ThrowAsync<SecretResolutionException>(async () =>
            await resolver.ResolveAsync(SecretRef.Parse($"file://{path}"), default));
    }

    [TestMethod]
    public async Task File_resolver_confined_to_a_root_reads_a_locator_relative_to_it()
    {
        // §17.5/F6: a configured root resolves locators relative to it (the locator is not the absolute path).
        string root = Path.Combine(Path.GetTempPath(), $"corvus-secret-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "db"));
        await File.WriteAllBytesAsync(Path.Combine(root, "db", "password"), Encoding.UTF8.GetBytes("confined-secret"));
        try
        {
            var resolver = new FileSecretResolver(root);
            using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse("file://db/password"), default);
            material.Reveal().ShouldBe("confined-secret");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task File_resolver_confined_to_a_root_rejects_traversal_and_absolute_escapes()
    {
        // §17.5/F6: neither a `..` traversal nor an absolute locator may escape the configured root.
        string root = Path.Combine(Path.GetTempPath(), $"corvus-secret-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var resolver = new FileSecretResolver(root);

            SecretResolutionException traversal = await Should.ThrowAsync<SecretResolutionException>(async () =>
                await resolver.ResolveAsync(SecretRef.Parse("file://../../etc/passwd"), default));
            traversal.Message.ShouldContain("escapes");

            await Should.ThrowAsync<SecretResolutionException>(async () =>
                await resolver.ResolveAsync(SecretRef.Parse("file:///etc/shadow"), default));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Composite_dispatches_by_scheme_and_fails_closed_for_an_unregistered_scheme()
    {
        const string name = "CORVUS_ARAZZO_TEST_SECRET_COMPOSITE";
        Environment.SetEnvironmentVariable(name, "via-composite");
        try
        {
            var resolver = new CompositeSecretResolver(new EnvSecretResolver(), new FileSecretResolver());
            resolver.CanResolve(SecretScheme.Environment).ShouldBeTrue();
            resolver.CanResolve(SecretScheme.KeyVault).ShouldBeFalse();

            using (SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse($"env://{name}"), default))
            {
                material.Reveal().ShouldBe("via-composite");
            }

            // No Key Vault resolver registered → fail closed, never silently empty.
            await Should.ThrowAsync<SecretResolutionException>(async () =>
                await resolver.ResolveAsync(SecretRef.Parse("keyvault://something"), default));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [TestMethod]
    public void Secret_material_scrubs_on_dispose()
    {
        var material = SecretMaterial.FromString("scrub-me");
        material.Utf8.Length.ShouldBe(8);
        material.Dispose();
        Should.Throw<ObjectDisposedException>(() => material.Reveal());
        material.Dispose(); // idempotent
    }
}