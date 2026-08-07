// <copyright file="SchemaResolutionReachTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using Corvus.Text.Json.Validator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// What a compiled schema is allowed to reach for a <c>$ref</c> it cannot satisfy locally.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="JsonSchema.Options"/> registers a file-system and an HTTP document resolver when
/// <c>allowFileSystemAndHttpResolution</c> is set, and that option defaults to <see langword="true"/>. For a tool
/// compiling schemas its operator chose that is the useful behaviour. For a host compiling a schema that arrived over
/// an API it means a <c>$ref</c> in someone else's document decides what the host fetches, which is a local file read
/// and a request to anything the host's network can reach.
/// </para>
/// <para>
/// These tests pin the mechanism in both directions, so the permissive default is a stated behaviour with a test on it
/// rather than an accident, and so a host that turns it off can prove it stayed off.
/// </para>
/// </remarks>
[TestClass]
public class SchemaResolutionReachTests
{
    [TestMethod]
    public void The_default_options_resolve_an_external_reference()
    {
        // Not an assertion that this is desirable — it is an assertion that it is what the default does, which is the
        // fact a host has to know before it decides whether to accept it.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var connected = new ManualResetEventSlim(false);
            Accept(listener, connected);

            TryCompile(SchemaReferencing("corvus:test/default", port));

            Assert.IsTrue(
                connected.Wait(TimeSpan.FromSeconds(5)),
                "the default options did not reach the network, so this test no longer describes the default.");
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public void Resolution_can_be_confined_to_what_the_caller_supplied()
    {
        // The posture a host compiling someone else's schema needs: the reference is refused rather than fetched.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var connected = new ManualResetEventSlim(false);
            Accept(listener, connected);

            var options = new JsonSchema.Options(allowFileSystemAndHttpResolution: false);
            TryCompile(SchemaReferencing("corvus:test/confined", port), options);

            Assert.IsFalse(
                connected.Wait(TimeSpan.FromSeconds(2)),
                "the schema reached a host outside the documents the caller supplied.");
        }
        finally
        {
            listener.Stop();
        }
    }

    [TestMethod]
    public void Confined_resolution_still_resolves_the_metaschema()
    {
        // The reason turning the flag off is safe: the metaschema resolver is registered independently of it, so a
        // schema that references the JSON Schema metaschema still compiles with no network involved.
        var options = new JsonSchema.Options(allowFileSystemAndHttpResolution: false);

        Exception failure = TryCompile(
            """{"$id":"corvus:test/meta","$schema":"https://json-schema.org/draft/2020-12/schema","type":"object"}""",
            options);

        Assert.IsNull(failure, "confining resolution must not break metaschema resolution.");
    }

    // Built by substitution rather than interpolation: the JSON's own closing braces collide with the raw-string
    // interpolation delimiters, and the escaping needed to write it inline obscures the schema being tested.
    private static string SchemaReferencing(string id, int port)
        => """{"$id":"ID","type":"object","properties":{"x":{"$ref":"http://127.0.0.1:PORT/leak.json"}}}"""
            .Replace("ID", id)
            .Replace("PORT", port.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static void Accept(TcpListener listener, ManualResetEventSlim connected)
        => _ = Task.Run(() =>
        {
            try
            {
                using TcpClient accepted = listener.AcceptTcpClient();
                connected.Set();
            }
            catch (SocketException)
            {
                // The listener was stopped without a connection arriving, which is the pass for the confined case.
            }
            catch (InvalidOperationException)
            {
                // Same, when the stop races the accept.
            }
        });

    // Compilation of an unresolvable reference is expected to fail; what these tests measure is whether the attempt
    // reached the network, not whether it succeeded.
    private static Exception TryCompile(string schema, JsonSchema.Options options = null)
    {
        try
        {
            JsonSchema.FromText(schema, options: options);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}