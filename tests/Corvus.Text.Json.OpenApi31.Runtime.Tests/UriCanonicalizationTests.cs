// <copyright file="UriCanonicalizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using CanonTests31.Client;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi31.Runtime.Tests;

/// <summary>
/// Runtime tests that verify the generated <c>WriteResolvedPath</c> and
/// <c>WriteQueryString</c> methods produce correctly URI-encoded UTF-8 bytes.
/// </summary>
[TestClass]
public class UriCanonicalizationTests
{
    [TestMethod]
    public void PathStringParam_SimpleAscii_NoEscaping()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"abc123\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/abc123", path);
    }

    [TestMethod]
    public void PathStringParam_WithSpaces_PercentEncoded()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"hello world\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/hello%20world", path);
    }

    [TestMethod]
    public void PathStringParam_WithSlash_PercentEncoded()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"foo/bar\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/foo%2Fbar", path);
    }

    [TestMethod]
    public void PathStringParam_WithPercent_DoubleEncoded()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"100%\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/100%25", path);
    }

    [TestMethod]
    public void PathStringParam_WithAmpersandEqualsQuestionHash_AllEncoded()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"a&b=c?d#e\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/a%26b%3Dc%3Fd%23e", path);
    }

    [TestMethod]
    public void PathStringParam_WithUnicode_PercentEncodedUtf8()
    {
        // é = U+00E9 = UTF-8 bytes C3 A9
        GetItemRequest request = new(JsonString.ParseValue("\"caf\\u00e9\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/caf%C3%A9", path);
    }

    [TestMethod]
    public void PathStringParam_WithPlus_PercentEncoded()
    {
        // '+' is NOT safe in path segments (RFC 3986 unreserved = ALPHA/DIGIT/-._~)
        GetItemRequest request = new(JsonString.ParseValue("\"a+b\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/a%2Bb", path);
    }

    [TestMethod]
    public void PathInt64Param_PositiveNumber_NoEscaping()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("42"u8),
            JsonString.ParseValue("\"test\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/42/tags/test", path);
    }

    [TestMethod]
    public void PathInt64Param_NegativeNumber_NoEscaping()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("-9223372036854775808"u8),
            JsonString.ParseValue("\"x\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/-9223372036854775808/tags/x", path);
    }

    [TestMethod]
    public void PathInt64Param_Zero_NoEscaping()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("0"u8),
            JsonString.ParseValue("\"tag\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/0/tags/tag", path);
    }

    [TestMethod]
    public void PathMultipleParams_MixedTypes_CorrectOrder()
    {
        // int64 path param + string path param with escaping
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("99"u8),
            JsonString.ParseValue("\"my tag\""u8));
        string path = WritePath(request);
        Assert.AreEqual("/items/99/tags/my%20tag", path);
    }

    [TestMethod]
    public void QueryStringParam_SimpleAscii_NoEscaping()
    {
        SearchRequest request = new(JsonString.ParseValue("\"hello\""u8));
        string query = WriteQuery(request);
        Assert.AreEqual("q=hello", query);
    }

    [TestMethod]
    public void QueryStringParam_WithSpaces_PercentEncoded()
    {
        SearchRequest request = new(JsonString.ParseValue("\"hello world\""u8));
        string query = WriteQuery(request);
        Assert.AreEqual("q=hello%20world", query);
    }

    [TestMethod]
    public void QueryStringParam_WithAmpersand_PercentEncoded()
    {
        SearchRequest request = new(JsonString.ParseValue("\"foo&bar\""u8));
        string query = WriteQuery(request);
        Assert.AreEqual("q=foo%26bar", query);
    }

    [TestMethod]
    public void QueryStringParam_WithEquals_PercentEncoded()
    {
        SearchRequest request = new(JsonString.ParseValue("\"a=b\""u8));
        string query = WriteQuery(request);
        Assert.AreEqual("q=a%3Db", query);
    }

    [TestMethod]
    public void QueryStringParam_WithPlus_PercentEncoded()
    {
        SearchRequest request = new(JsonString.ParseValue("\"a+b\""u8));
        string query = WriteQuery(request);
        Assert.AreEqual("q=a%2Bb", query);
    }

    [TestMethod]
    public void QueryStringParam_WithUnicode_PercentEncodedUtf8()
    {
        // ü = U+00FC = UTF-8 bytes C3 BC
        SearchRequest request = new(JsonString.ParseValue("\"\\u00fcber\""u8));
        string query = WriteQuery(request);
        Assert.AreEqual("q=%C3%BCber", query);
    }

    [TestMethod]
    public void QueryInt32Param_PositiveNumber_NoEscaping()
    {
        SearchRequest request = new(JsonString.ParseValue("\"x\""u8))
        {
            Page = JsonInt32.ParseValue("42"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("q=x&page=42", query);
    }

    [TestMethod]
    public void QueryInt32Param_NegativeNumber_NoEscaping()
    {
        SearchRequest request = new(JsonString.ParseValue("\"x\""u8))
        {
            Page = JsonInt32.ParseValue("-1"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("q=x&page=-1", query);
    }

    [TestMethod]
    public void QueryBooleanParam_True()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"id\""u8))
        {
            Verbose = JsonBoolean.ParseValue("true"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("verbose=true", query);
    }

    [TestMethod]
    public void QueryBooleanParam_False()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"id\""u8))
        {
            Verbose = JsonBoolean.ParseValue("false"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("verbose=false", query);
    }

    [TestMethod]
    public void QueryDoubleParam_Fractional()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("1"u8),
            JsonString.ParseValue("\"t\""u8))
        {
            Score = JsonDouble.ParseValue("3.14"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("score=3.14", query);
    }

    [TestMethod]
    public void QueryDoubleParam_NegativeExponent()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("1"u8),
            JsonString.ParseValue("\"t\""u8))
        {
            Score = JsonDouble.ParseValue("-0.001"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("score=-0.001", query);
    }

    [TestMethod]
    public void QueryFloatParam_Value()
    {
        SearchRequest request = new(JsonString.ParseValue("\"x\""u8))
        {
            Rating = JsonSingle.ParseValue("2.5"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("q=x&rating=2.5", query);
    }

    [TestMethod]
    public void QueryMultipleOptionalParams_AllSet()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"id\""u8))
        {
            Filter = JsonString.ParseValue("\"a b\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
            Verbose = JsonBoolean.ParseValue("true"u8),
        };
        string query = WriteQuery(request);
        Assert.AreEqual("filter=a%20b&limit=10&verbose=true", query);
    }

    [TestMethod]
    public void QueryNoOptionalParams_ReturnsZeroBytes()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"id\""u8));
        ArrayBufferWriter<byte> writer = new(256);
        int bytesWritten = request.WriteQueryString(writer);
        Assert.AreEqual(0, bytesWritten);
        Assert.AreEqual(0, writer.WrittenCount);
    }

    [TestMethod]
    public void QueryWrittenCount_MatchesActualBytes()
    {
        SearchRequest request = new(JsonString.ParseValue("\"hello world\""u8))
        {
            Page = JsonInt32.ParseValue("5"u8),
            Rating = JsonSingle.ParseValue("1.5"u8),
        };

        ArrayBufferWriter<byte> writer = new(256);
        int reported = request.WriteQueryString(writer);
        Assert.AreEqual(writer.WrittenCount, reported);
    }

    [TestMethod]
    public void HeaderStringParam_NoEscaping()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"id\""u8))
        {
            XRequestId = JsonString.ParseValue("\"hello world & more\""u8),
        };

        List<(string Name, string Value)> headers = [];
        request.WriteHeaders(
            static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, List<(string, string)> state) =>
            {
                state.Add((Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(value)));
            },
            headers);

        Assert.AreEqual(2, headers.Count);
        Assert.AreEqual("Accept", headers[0].Name);
        Assert.AreEqual("application/json", headers[0].Value);
        Assert.AreEqual("X-Request-Id", headers[1].Name);
        Assert.AreEqual("hello world & more", headers[1].Value);
    }

    [TestMethod]
    public void HeaderParam_NotSet_OnlyAcceptCallback()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"id\""u8));

        List<(string Name, string Value)> headers = [];
        request.WriteHeaders(
            static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, List<(string, string)> state) =>
            {
                state.Add((Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(value)));
            },
            headers);

        Assert.AreEqual(1, headers.Count);
        Assert.AreEqual("Accept", headers[0].Name);
        Assert.AreEqual("application/json", headers[0].Value);
    }

    [TestMethod]
    public void FullUri_PathAndQuery_Combined()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"hello world\""u8))
        {
            Filter = JsonString.ParseValue("\"a&b\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
        };

        string uri = BuildFullUri(request);
        Assert.AreEqual("/items/hello%20world?filter=a%26b&limit=10", uri);
    }

    [TestMethod]
    public void FullUri_NoQueryParams_NoQuestionMark()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"test\""u8));
        string uri = BuildFullUri(request);
        Assert.AreEqual("/items/test", uri);
    }

    [TestMethod]
    public void FullUri_NoPathParams_UsesTemplateDirect()
    {
        SearchRequest request = new(JsonString.ParseValue("\"hello\""u8));
        string uri = BuildFullUri(request);
        Assert.AreEqual("/search?q=hello", uri);
    }

    private static string WritePath<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        ArrayBufferWriter<byte> writer = new(256);
        request.WriteResolvedPath(writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private static string WriteQuery<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        ArrayBufferWriter<byte> writer = new(256);
        request.WriteQueryString(writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>
    /// Simulates the transport's <c>BuildUri</c> logic: path + optional ?query.
    /// </summary>
    private static string BuildFullUri<TRequest>(in TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        ArrayBufferWriter<byte> writer = new(512);

        if (TRequest.HasPathParameters)
        {
            request.WriteResolvedPath(writer);
        }
        else
        {
            writer.Write(TRequest.PathTemplateUtf8);
        }

        if (TRequest.HasQueryParameters)
        {
            int pathEnd = writer.WrittenCount;
            writer.Write("?"u8);
            int queryBytes = request.WriteQueryString(writer);

            if (queryBytes == 0)
            {
                return Encoding.UTF8.GetString(writer.WrittenSpan[..pathEnd]);
            }
        }

        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}