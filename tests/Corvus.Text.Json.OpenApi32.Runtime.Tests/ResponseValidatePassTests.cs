// <copyright file="ResponseValidatePassTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Client;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests that exercise the Validate() pass-through paths (valid body)
/// and MatchResult dispatches for all response types.
/// </summary>
[TestClass]
public class ResponseValidatePassTests
{
    // ── CookieArrayNonexplode ──
    [TestMethod]
    public async Task CookieArrayNonexplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieArrayNonexplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CookieArrayNonexplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieArrayNonexplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CookieArrayNonexplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await CookieArrayNonexplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task CookieArrayNonexplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await CookieArrayNonexplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── CookieArray ──
    [TestMethod]
    public async Task CookieArray_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieArrayResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CookieArray_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieArrayResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CookieArray_MatchResult_DispatchesToDefault()
    {
        await using var response = await CookieArrayResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task CookieArray_MatchResultContext_DispatchesToOk()
    {
        await using var response = await CookieArrayResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── CookieObjectNonexplode ──
    [TestMethod]
    public async Task CookieObjectNonexplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieObjectNonexplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CookieObjectNonexplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieObjectNonexplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CookieObjectNonexplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await CookieObjectNonexplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task CookieObjectNonexplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await CookieObjectNonexplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── CookieObject ──
    [TestMethod]
    public async Task CookieObject_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieObjectResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CookieObject_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await CookieObjectResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CookieObject_MatchResult_DispatchesToDefault()
    {
        await using var response = await CookieObjectResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task CookieObject_MatchResultContext_DispatchesToOk()
    {
        await using var response = await CookieObjectResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── HeaderArray ──
    [TestMethod]
    public async Task HeaderArray_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await HeaderArrayResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task HeaderArray_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await HeaderArrayResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task HeaderArray_MatchResult_DispatchesToDefault()
    {
        await using var response = await HeaderArrayResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task HeaderArray_MatchResultContext_DispatchesToOk()
    {
        await using var response = await HeaderArrayResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── HeaderObject ──
    [TestMethod]
    public async Task HeaderObject_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await HeaderObjectResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task HeaderObject_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await HeaderObjectResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task HeaderObject_MatchResult_DispatchesToDefault()
    {
        await using var response = await HeaderObjectResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task HeaderObject_MatchResultContext_DispatchesToOk()
    {
        await using var response = await HeaderObjectResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── HeaderObjectExplode ──
    [TestMethod]
    public async Task HeaderObjectExplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await HeaderObjectExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task HeaderObjectExplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await HeaderObjectExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task HeaderObjectExplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await HeaderObjectExplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task HeaderObjectExplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await HeaderObjectExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathArraySimple ──
    [TestMethod]
    public async Task PathArraySimple_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathArraySimpleResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathArraySimple_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathArraySimpleResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathArraySimple_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathArraySimpleResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathArraySimple_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathArraySimpleResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathArrayLabel ──
    [TestMethod]
    public async Task PathArrayLabel_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathArrayLabelResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathArrayLabel_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathArrayLabelResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathArrayLabel_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathArrayLabelResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathArrayLabel_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathArrayLabelResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathArrayMatrix ──
    [TestMethod]
    public async Task PathArrayMatrix_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathArrayMatrixResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathArrayMatrix_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathArrayMatrixResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathArrayMatrix_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathArrayMatrixResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathArrayMatrix_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathArrayMatrixResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathObjectSimple ──
    [TestMethod]
    public async Task PathObjectSimple_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectSimpleResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathObjectSimple_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectSimpleResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathObjectSimple_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathObjectSimpleResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathObjectSimple_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathObjectSimpleResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathObjectSimpleExplode ──
    [TestMethod]
    public async Task PathObjectSimpleExplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectSimpleExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectSimpleExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathObjectSimpleExplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathObjectSimpleExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathObjectLabel ──
    [TestMethod]
    public async Task PathObjectLabel_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectLabelResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathObjectLabel_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectLabelResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathObjectLabel_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathObjectLabelResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathObjectLabel_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathObjectLabelResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathObjectLabelExplode ──
    [TestMethod]
    public async Task PathObjectLabelExplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectLabelExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectLabelExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathObjectLabelExplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathObjectLabelExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathObjectMatrix ──
    [TestMethod]
    public async Task PathObjectMatrix_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectMatrixResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathObjectMatrix_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectMatrixResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathObjectMatrix_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathObjectMatrixResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathObjectMatrix_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathObjectMatrixResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── PathObjectMatrixExplode ──
    [TestMethod]
    public async Task PathObjectMatrixExplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectMatrixExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await PathObjectMatrixExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await PathObjectMatrixExplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await PathObjectMatrixExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── QueryArrayExplode ──
    [TestMethod]
    public async Task QueryArrayExplode_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await QueryArrayExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task QueryArrayExplode_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await QueryArrayExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task QueryArrayExplode_MatchResult_DispatchesToDefault()
    {
        await using var response = await QueryArrayExplodeResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task QueryArrayExplode_MatchResultContext_DispatchesToOk()
    {
        await using var response = await QueryArrayExplodeResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── QuerySearch ──
    [TestMethod]
    public async Task QuerySearch_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await QuerySearchResponse.CreateAsync(
            200, MakeStream("""{"total":0,"results":[]}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task QuerySearch_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await QuerySearchResponse.CreateAsync(
            200, MakeStream("""{"total":0,"results":[]}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task QuerySearch_MatchResult_DispatchesToDefault()
    {
        await using var response = await QuerySearchResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task QuerySearch_MatchResultContext_DispatchesToOk()
    {
        await using var response = await QuerySearchResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── GetPreferences ──
    [TestMethod]
    public async Task GetPreferences_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await GetPreferencesResponse.CreateAsync(
            200, MakeStream("""{"theme":"dark","language":"en"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetPreferences_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await GetPreferencesResponse.CreateAsync(
            200, MakeStream("""{"theme":"dark","language":"en"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task GetPreferences_MatchResult_DispatchesToDefault()
    {
        await using var response = await GetPreferencesResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task GetPreferences_MatchResultContext_DispatchesToOk()
    {
        await using var response = await GetPreferencesResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── GetDocument ──
    [TestMethod]
    public async Task GetDocument_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await GetDocumentResponse.CreateAsync(
            200, MakeStream("""{"path":"/doc","content":"hello"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetDocument_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await GetDocumentResponse.CreateAsync(
            200, MakeStream("""{"path":"/doc","content":"hello"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task GetDocument_MatchResult_DispatchesToDefault()
    {
        await using var response = await GetDocumentResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task GetDocument_MatchResultContext_DispatchesToOk()
    {
        await using var response = await GetDocumentResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── GetByFlag ──
    [TestMethod]
    public async Task GetByFlag_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await GetByFlagResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetByFlag_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await GetByFlagResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task GetByFlag_MatchResult_DispatchesToDefault()
    {
        await using var response = await GetByFlagResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task GetByFlag_MatchResultContext_DispatchesToOk()
    {
        await using var response = await GetByFlagResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    // ── GetSessionProfile ──
    [TestMethod]
    public async Task GetSessionProfile_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using var response = await GetSessionProfileResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetSessionProfile_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using var response = await GetSessionProfileResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task GetSessionProfile_MatchResult_DispatchesToDefault()
    {
        await using var response = await GetSessionProfileResponse.CreateAsync(
            503, MakeStream(string.Empty));
        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static code => $"default:{code}");
        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task GetSessionProfile_MatchResultContext_DispatchesToOk()
    {
        await using var response = await GetSessionProfileResponse.CreateAsync(
            200, MakeStream("""{"key":"value"}"""), "application/json");
        string result = response.MatchResult(
            "ctx",
            matchOk: static (_, in ctx) => $"ok:{ctx}",
            matchDefault: static (code, in ctx) => $"default:{code}:{ctx}");
        Assert.AreEqual("ok:ctx", result);
    }

    private static MemoryStream MakeStream(string content)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
    }
}