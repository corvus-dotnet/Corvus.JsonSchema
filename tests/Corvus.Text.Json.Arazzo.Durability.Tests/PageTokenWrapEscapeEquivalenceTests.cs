// <copyright file="PageTokenWrapEscapeEquivalenceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Pins the escape-equivalence invariant the paging-token wrappers rely on (Create() adoption row 1.11): the
/// in-process token wrappers (<c>SecuredWorkflowManagement.WrapContinuationToken</c> and the two
/// <c>WrapPageToken</c> siblings) re-present a store's opaque continuation token via the generated
/// <see cref="JsonString"/> <c>Create()</c>, which escapes with the default encoder — whereas the hand wrap they
/// replaced spliced the bytes between bare quotes without escaping. The two are byte-identical exactly when the
/// token contains no character the default encoder escapes; our keyset pagers emit base64url tokens, whose whole
/// alphabet is escape-invariant. This test pins that alphabet-level invariant, so a future pager emitting
/// escapable token bytes fails here rather than silently changing the wire bytes.
/// </summary>
[TestClass]
public sealed class PageTokenWrapEscapeEquivalenceTests
{
    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [TestMethod]
    public void The_full_base64url_alphabet_wraps_byte_identically_to_the_bare_quote_wrap()
    {
        // One token exercising every character the pagers may emit, plus '=' padding for the non-url-safe edge.
        byte[] token = Encoding.UTF8.GetBytes(Base64UrlAlphabet + "=");

        byte[] bareWrap = new byte[token.Length + 2];
        bareWrap[0] = (byte)'"';
        token.CopyTo(bareWrap.AsSpan(1));
        bareWrap[^1] = (byte)'"';

        using ParsedJsonDocument<JsonString> wrapped = JsonString.Create(token.AsSpan());
        byte[] created = PersistedJson.ToArray(
            wrapped.RootElement,
            static (Utf8JsonWriter writer, in JsonString v) => v.WriteTo(writer));

        created.ShouldBe(bareWrap);
    }

    [TestMethod]
    public void An_escapable_token_byte_round_trips_as_a_value_where_the_bare_wrap_emitted_invalid_json()
    {
        // Not a token our pagers produce — this documents the behavioural upgrade: the old bare wrap would have
        // spliced the quote straight into the JSON text (invalid document); Create() escapes it, and the value read
        // back through the seam is the original token.
        const string token = "prefix\"suffix";
        using ParsedJsonDocument<JsonString> wrapped = JsonString.Create(token);
        ((string)wrapped.RootElement).ShouldBe(token);
    }
}