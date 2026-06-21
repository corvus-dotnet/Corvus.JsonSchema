// <copyright file="ContinuationTokenBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the keyset continuation-token codecs (design §13 / §16.5.4) — the per-page <c>Encode</c> and per-request
/// <c>Decode</c> on the list/search warm path. The legacy <c>string</c> path (<c>Separator.ToString()</c> + <c>Concat</c>
/// + <c>GetBytes</c> on encode; <c>DecodeFromChars</c> + <c>GetString</c> + <c>Split</c> on decode) is contrasted with the
/// bytes-native path: encode assembles the key into a pooled/stack UTF-8 buffer and Base64URL-writes it straight into a
/// caller buffer (the response writer / a pooled page buffer in production; a <c>stackalloc</c> here), and decode reads
/// straight from the request's UTF-8. The only managed output the bytes path keeps is the decode cursor's genuine string
/// leaves (the stores bind them as ordering/seek parameters).
/// </summary>
public class ContinuationTokenBenchmarks
{
    private const string RunId = "0c2b8a5e-6f4d-4c7a-9b1e-2d3f4a5b6c7d";
    private const string SubjectValue = "alice.example@contoso.com";
    private const string SubjectKind = "person";
    private const string SourceName = "petstore-prod";
    private const string Environment = "production";
    private const string TieBreaker = "t:contoso|a:read";

    private string runToken = null!;
    private string identityToken = null!;
    private string credentialToken = null!;
    private byte[] runTokenUtf8 = null!;
    private byte[] identityTokenUtf8 = null!;
    private byte[] credentialTokenUtf8 = null!;

    /// <summary>Precomputes the encoded tokens (string + UTF-8) the decode arms consume.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.runToken = WorkflowContinuationToken.Encode(RunId);
        this.identityToken = ObservedIdentityContinuationToken.Encode(SubjectValue, SubjectKind);
        this.credentialToken = SourceCredentialContinuationToken.Encode(SourceName, Environment, TieBreaker);
        this.runTokenUtf8 = Encoding.UTF8.GetBytes(this.runToken);
        this.identityTokenUtf8 = Encoding.UTF8.GetBytes(this.identityToken);
        this.credentialTokenUtf8 = Encoding.UTF8.GetBytes(this.credentialToken);
    }

    /// <summary>Single-key encode, legacy string result.</summary>
    [Benchmark]
    public int Run_Encode_String() => WorkflowContinuationToken.Encode(RunId).Length;

    /// <summary>Single-key encode, bytes-native into a caller buffer.</summary>
    [Benchmark]
    public int Run_Encode_Utf8()
    {
        Span<byte> buffer = stackalloc byte[WorkflowContinuationToken.GetEncodedLength(RunId)];
        return WorkflowContinuationToken.EncodeToUtf8(RunId, buffer);
    }

    /// <summary>Two-part encode, legacy string result.</summary>
    [Benchmark]
    public int Identity_Encode_String() => ObservedIdentityContinuationToken.Encode(SubjectValue, SubjectKind).Length;

    /// <summary>Two-part encode, bytes-native into a caller buffer.</summary>
    [Benchmark]
    public int Identity_Encode_Utf8()
    {
        Span<byte> buffer = stackalloc byte[ObservedIdentityContinuationToken.GetEncodedLength(SubjectValue, SubjectKind)];
        return ObservedIdentityContinuationToken.EncodeToUtf8(SubjectValue, SubjectKind, buffer);
    }

    /// <summary>Three-part encode, legacy string result.</summary>
    [Benchmark]
    public int Credential_Encode_String() => SourceCredentialContinuationToken.Encode(SourceName, Environment, TieBreaker).Length;

    /// <summary>Three-part encode, bytes-native into a caller buffer.</summary>
    [Benchmark]
    public int Credential_Encode_Utf8()
    {
        Span<byte> buffer = stackalloc byte[SourceCredentialContinuationToken.GetEncodedLength(SourceName, Environment, TieBreaker)];
        return SourceCredentialContinuationToken.EncodeToUtf8(SourceName, Environment, TieBreaker, buffer);
    }

    /// <summary>Single-key decode from a token string.</summary>
    [Benchmark]
    public int Run_Decode_String() => WorkflowContinuationToken.Decode(this.runToken)!.Length;

    /// <summary>Single-key decode straight from the request's UTF-8.</summary>
    [Benchmark]
    public int Run_Decode_Utf8() => WorkflowContinuationToken.Decode(this.runTokenUtf8)!.Length;

    /// <summary>Two-part decode from a token string.</summary>
    [Benchmark]
    public int Identity_Decode_String()
    {
        ObservedIdentityContinuationToken.TryDecode(this.identityToken, out (string SubjectValue, string SubjectKind) cursor);
        return cursor.SubjectValue.Length;
    }

    /// <summary>Two-part decode straight from the request's UTF-8.</summary>
    [Benchmark]
    public int Identity_Decode_Utf8()
    {
        ObservedIdentityContinuationToken.TryDecode(this.identityTokenUtf8, out (string SubjectValue, string SubjectKind) cursor);
        return cursor.SubjectValue.Length;
    }

    /// <summary>Three-part decode from a token string.</summary>
    [Benchmark]
    public int Credential_Decode_String()
    {
        SourceCredentialContinuationToken.TryDecode(this.credentialToken, out (string SourceName, string Environment, string TieBreaker) cursor);
        return cursor.SourceName.Length;
    }

    /// <summary>Three-part decode straight from the request's UTF-8.</summary>
    [Benchmark]
    public int Credential_Decode_Utf8()
    {
        SourceCredentialContinuationToken.TryDecode(this.credentialTokenUtf8, out (string SourceName, string Environment, string TieBreaker) cursor);
        return cursor.SourceName.Length;
    }
}