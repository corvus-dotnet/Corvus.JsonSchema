// <copyright file="SecurityBindingReadBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the security-binding write input seam (POST/PUT /security/bindings → the draft the store completes): the
/// per-request work the handler's <c>ReadBinding</c> does between "the framework parsed the body" and "the draft the store
/// reads", starting from an already-parsed body.
/// <list type="bullet">
/// <item><see cref="ReadBinding_FromGrants"/> — the old seam: each verb grant is converted <c>Models.VerbGrant</c> →
/// <c>VerbGrantInfo</c> (a <see cref="List{T}"/> of managed rule-name strings per grant, then <c>VerbGrantInfo.Rules</c>
/// rebuilds the grant through a pooled workspace/builder/clone), the scalars are realised via <c>(string)</c>, and
/// <c>SecurityBindingDocument.Draft</c> builds a fresh pooled draft document.</item>
/// <item><see cref="ReadBinding_FromBody"/> — the new seam: <c>SecurityBindingDocument.From(body)</c> is a free,
/// zero-copy element view over the already-parsed body — no grant rebuild, no per-field strings, and no pooled draft
/// document at all (the store reads the body view directly and defaults any omitted verb grant to None).</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class SecurityBindingReadBenchmarks
{
    // A representative POST /security/bindings body: a groups→rules binding with a multi-rule read grant (exercises the
    // VerbGrantInfo.Rules rebuild), full write, none purge.
    private static readonly byte[] BodyJson =
        """
        {
          "claimType": "groups",
          "claimValue": "platform-engineers",
          "read": { "ruleNames": [ "team-scope", "env-prod" ] },
          "write": { "unrestricted": true },
          "purge": { "unrestricted": false },
          "order": 10,
          "description": "Platform engineers binding."
        }
        """u8.ToArray();

    private ParsedJsonDocument<Models.SecurityBindingWrite> body = null!;

    [GlobalSetup]
    public void Setup() => this.body = ParsedJsonDocument<Models.SecurityBindingWrite>.Parse(BodyJson);

    [GlobalCleanup]
    public void Cleanup() => this.body.Dispose();

    /// <summary>The old seam: convert each grant (managed rule-name list + Rules rebuild), realise the scalars, build a pooled draft.</summary>
    /// <returns>The draft order (returned to defeat dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int ReadBinding_FromGrants()
    {
        Models.SecurityBindingWrite b = this.body.RootElement;
        using ParsedJsonDocument<SecurityBindingDocument> draft = SecurityBindingDocument.Draft(
            (string)b.ClaimType,
            b.ClaimValue.IsNotUndefined() ? (string)b.ClaimValue : null,
            ToGrant(b.Read),
            ToGrant(b.Write),
            ToGrant(b.Purge),
            b.Order.IsNotUndefined() ? (int)b.Order : 0,
            b.Description.IsNotUndefined() ? (string)b.Description : null);
        return (int)draft.RootElement.Order;
    }

    /// <summary>The new seam: a free zero-copy element view over the already-parsed body (no list, no rebuild, no pooled draft).</summary>
    /// <returns>The draft order.</returns>
    [Benchmark]
    public int ReadBinding_FromBody()
    {
        Models.SecurityBindingWrite b = this.body.RootElement;
        SecurityBindingDocument draft = SecurityBindingDocument.From(b);
        return (int)draft.Order;
    }

    // The handler's current Models.VerbGrant → VerbGrantInfo conversion (realises a managed rule-name list, then Rules
    // rebuilds the grant through a pooled workspace/builder/clone).
    private static SecurityBindingDocument.VerbGrantInfo ToGrant(Models.VerbGrant grant)
    {
        if (grant.IsUndefined())
        {
            return SecurityBindingDocument.VerbGrantInfo.None;
        }

        if (grant.Unrestricted.IsNotUndefined() && (bool)grant.Unrestricted)
        {
            return SecurityBindingDocument.VerbGrantInfo.Full;
        }

        var names = new List<string>();
        if (grant.RuleNames.IsNotUndefined())
        {
            foreach (Models.JsonString name in grant.RuleNames.EnumerateArray())
            {
                names.Add((string)name);
            }
        }

        return names.Count == 0 ? SecurityBindingDocument.VerbGrantInfo.None : SecurityBindingDocument.VerbGrantInfo.Rules([.. names]);
    }
}
