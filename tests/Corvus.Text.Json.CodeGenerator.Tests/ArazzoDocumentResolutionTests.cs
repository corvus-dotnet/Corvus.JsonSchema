// <copyright file="ArazzoDocumentResolutionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Text;
using Corvus.Text.Json.Arazzo.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Covers what a document loader is allowed to reach for a reference it cannot satisfy from the registered documents.
/// The control plane compiles attacker-authored packages in its own process, so a reference naming a URI outside the
/// package must fail closed rather than being retrieved: retrieving it turns <c>$self</c> or a
/// <c>sourceDescriptions[].url</c> into a request the control plane makes on the author's behalf, which is a read of
/// local files and of anything the control plane's network can reach, including a cloud instance-metadata endpoint.
/// </summary>
/// <remarks>
/// A developer tool resolving a document tree the operator chose is a different case, and keeps retrieval. The point of
/// these tests is that the two are now a stated choice rather than a consequence of whether a registry happened to be
/// supplied.
/// </remarks>
[TestClass]
public class ArazzoDocumentResolutionTests : IDisposable
{
    private readonly string scratchDir;

    public ArazzoDocumentResolutionTests()
    {
        this.scratchDir = CodeGeneratorRunner.CreateTempOutputDirectory();
    }

    public void Dispose()
    {
        CodeGeneratorRunner.CleanupTempDirectory(this.scratchDir);
        GC.SuppressFinalize(this);
    }

    [TestMethod]
    public void Registered_only_resolution_does_not_read_the_local_file_system()
    {
        string path = Path.Combine(this.scratchDir, "outside-the-package.json");
        File.WriteAllText(path, """{"openapi":"3.1.0","info":{"title":"t","version":"1"},"paths":{}}""");

        var loader = ArazzoGenerationDriver.CreateDocumentLoader(
            registeredDocuments: null,
            ArazzoDocumentResolution.RegisteredOnly);

        Assert.IsNull(loader(new Uri(path)), "the loader read a file outside the package.");
    }

    [TestMethod]
    public void Registered_only_resolution_does_not_reach_the_network()
    {
        // Asserting only that the loader returns null is not enough, and an earlier version of this test made exactly
        // that mistake: a loader that DOES attempt the fetch also returns null once the request fails, so the assertion
        // passed against the very behaviour it exists to forbid — betrayed only by the hundred seconds it took. What is
        // asserted here is that no connection is opened at all, observed by a listener that would record one.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var connected = new ManualResetEventSlim(false);
            _ = Task.Run(() =>
            {
                try
                {
                    using TcpClient accepted = listener.AcceptTcpClient();
                    connected.Set();
                }
                catch (SocketException)
                {
                    // The listener was stopped at the end of the test without a connection arriving, which is the pass.
                }
                catch (InvalidOperationException)
                {
                    // Same, when the stop races the accept.
                }
            });

            var loader = ArazzoGenerationDriver.CreateDocumentLoader(
                registeredDocuments: null,
                ArazzoDocumentResolution.RegisteredOnly);

            byte[] resolved = loader(new Uri($"http://127.0.0.1:{port}/pets.openapi.json"));

            Assert.IsNull(resolved, "the loader resolved a document from outside the package.");
            Assert.IsFalse(
                connected.Wait(TimeSpan.FromSeconds(2)),
                "the loader opened a connection to a host outside the package.");
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public void Registered_only_resolution_still_returns_a_registered_document()
    {
        byte[] content = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"t","version":"1"},"paths":{}}""");
        var uri = new Uri("https://specs.example.test/pets.openapi.json");

        var loader = ArazzoGenerationDriver.CreateDocumentLoader(
            [new RegisteredDocument(uri, content)],
            ArazzoDocumentResolution.RegisteredOnly);

        Assert.IsNotNull(loader(uri));
    }

    [TestMethod]
    public void The_default_resolution_is_the_closed_one()
    {
        // The zero value must be RegisteredOnly, because that is what `default` and an unset field resolve to. An
        // option whose default is the permissive value is how a control ends up off wherever someone forgot to name it,
        // which is the trap the YAML reader options fell into (H6). Asserted by name against the underlying zero so a
        // later reordering of the members fails here rather than silently inverting the default.
        Assert.AreEqual("RegisteredOnly", Enum.GetName(typeof(ArazzoDocumentResolution), 0));
    }

    [TestMethod]
    public void Retrieval_resolution_reads_the_local_file_system()
    {
        // The developer-tool case, unchanged: the CLI resolves the document tree the operator pointed it at, and the
        // Arazzo lock file re-resolves each recorded source to decide whether a regeneration can be skipped.
        string path = Path.Combine(this.scratchDir, "local.json");
        File.WriteAllText(path, """{"openapi":"3.1.0","info":{"title":"t","version":"1"},"paths":{}}""");

        var loader = ArazzoGenerationDriver.CreateFileSystemDocumentLoader();

        Assert.IsNotNull(loader(new Uri(path)));
    }
}