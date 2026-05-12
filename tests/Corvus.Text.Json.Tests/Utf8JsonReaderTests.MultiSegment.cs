// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

public partial class Utf8JsonReaderTests
{
    [TestMethod]
    [DataRow("[123, 456]", "123456", "123456")]
    [DataRow("/*a*/[{\"testA\":[{\"testB\":[{\"testC\":123}]}]}]", "testAtestBtestC123", "atestAtestBtestC123")]
    [DataRow("{\"testA\":[1/*hi*//*bye*/, 2, 3], \"testB\": 4}", "testA123testB4", "testA1hibye23testB4")]
    [DataRow("{\"test\":[[[123,456]]]}", "test123456", "test123456")]
    [DataRow("/*a*//*z*/[/*b*//*z*/123/*c*//*z*/,/*d*//*z*/456/*e*//*z*/]/*f*//*z*/", "123456", "azbz123czdz456ezfz")]
    [DataRow("[123,/*hi*/456/*bye*/]", "123456", "123hi456bye")]
    [DataRow("[123,//hi\n456//bye\n]", "123456", "123hi456bye")]
    [DataRow("[123,//hi\r456//bye\r]", "123456", "123hi456bye")]
    [DataRow("[123,//hi\r\n456]", "123456", "123hi456")]
    [DataRow("/*a*//*z*/{/*b*//*z*/\"test\":/*c*//*z*/[/*d*//*z*/[/*e*//*z*/[/*f*//*z*/123/*g*//*z*/,/*h*//*z*/456/*i*//*z*/]/*j*//*z*/]/*k*//*z*/]/*l*//*z*/}/*m*//*z*/",
    "test123456", "azbztestczdzezfz123gzhz456izjzkzlzmz")]
    [DataRow("//a\n//z\n{//b\n//z\n\"test\"://c\n//z\n[//d\n//z\n[//e\n//z\n[//f\n//z\n123//g\n//z\n,//h\n//z\n456//i\n//z\n]//j\n//z\n]//k\n//z\n]//l\n//z\n}//m\n//z\n",
    "test123456", "azbztestczdzezfz123gzhz456izjzkzlzmz")]
    public void AllowCommentStackMismatchMultiSegment(string jsonString, string expectedWithoutComments, string expectedWithComments)
    {
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        var sequence = new ReadOnlySequence<byte>(data);
        TestReadingJsonWithComments(data, sequence, expectedWithoutComments, expectedWithComments);

        sequence = JsonTestHelper.GetSequence(data, 1);
        TestReadingJsonWithComments(data, sequence, expectedWithoutComments, expectedWithComments);

        sequence = JsonTestHelper.GetSequence(data, 6);
        TestReadingJsonWithComments(data, sequence, expectedWithoutComments, expectedWithComments);

        var firstSegment = new BufferSegment<byte>(ReadOnlyMemory<byte>.Empty);
        ReadOnlyMemory<byte> secondMem = data;
        BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
        sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);
        TestReadingJsonWithComments(data, sequence, expectedWithoutComments, expectedWithComments);
    }

    [TestMethod]
    [DataRow("null", JsonTokenType.Null)]
    [DataRow("false", JsonTokenType.False)]
    [DataRow("true", JsonTokenType.True)]
    [DataRow("42", JsonTokenType.Number)]
    [DataRow("\"string\"", JsonTokenType.String)]
    [DataRow("""{ "key" : "value" }""", JsonTokenType.StartObject)]
    [DataRow("""[{},1,"string",false, true]""", JsonTokenType.StartArray)]
    public void AllowMultipleValues_Comments_PartialData_MultiSegment(string jsonValue, JsonTokenType firstTokenType)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true, CommentHandling = JsonCommentHandling.Skip };
        JsonReaderState state = new(options);
        Utf8JsonReader reader = new(JsonTestHelper.GetSequence(jsonValue + "/* comment */", 1), isFinalBlock: false, state);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        Assert.IsTrue(reader.TrySkip());
        Assert.IsFalse(reader.Read());

        reader = new Utf8JsonReader(JsonTestHelper.GetSequence(jsonValue, 1), isFinalBlock: true, reader.CurrentState);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        reader.Skip();
        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow(JsonCommentHandling.Allow)]
    [DataRow(JsonCommentHandling.Skip)]
    public void AllowMultipleValues_CommentSeparated_MultiSegment(JsonCommentHandling commentHandling)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true, CommentHandling = commentHandling };
        var reader = new Utf8JsonReader(JsonTestHelper.GetSequence("{ }    /* I'm a comment */       1", 1), options);
        Assert.AreEqual(JsonTokenType.None, reader.TokenType);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.EndObject, reader.TokenType);

        Assert.IsTrue(reader.Read());

        if (commentHandling is JsonCommentHandling.Allow)
        {
            Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
            Assert.IsTrue(reader.Read());
        }

        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow("{ } 1", new[] { JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.Number })]
    [DataRow("{ }1", new[] { JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.Number })]
    [DataRow("{ }\t\r\n           1", new[] { JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.Number })]
    [DataRow("1 3.14 false null", new[] { JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.False, JsonTokenType.Null })]
    [DataRow("42", new[] { JsonTokenType.Number })]
    [DataRow("\"str\"\"str\"null", new[] { JsonTokenType.String, JsonTokenType.String, JsonTokenType.Null })]
    [DataRow("[]{}[]", new[] { JsonTokenType.StartArray, JsonTokenType.EndArray, JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.StartArray, JsonTokenType.EndArray })]
    public void AllowMultipleValues_MultiSegment(string json, JsonTokenType[] expectedSequence)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new(JsonTestHelper.GetSequence(json, 1), options);

        Assert.AreEqual(JsonTokenType.None, reader.TokenType);

        foreach (JsonTokenType expected in expectedSequence)
        {
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expected, reader.TokenType);
        }

        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\t\r\n")]
    [DataRow("    \t\t                        ")]
    public void AllowMultipleValues_NoJsonContent_ReturnsFalse_MultiSegment(string json)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new(JsonTestHelper.GetSequence(json, 1), options);

        Assert.IsTrue(reader.IsFinalBlock);
        Assert.IsFalse(reader.Read());
        Assert.AreEqual(JsonTokenType.None, reader.TokenType);
    }

    [TestMethod]
    public void AllowMultipleValues_NonJsonTrailingData_ThrowsJsonException_MultiSegment()
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new(JsonTestHelper.GetSequence("{ }      not JSON", 1), options);
        Assert.AreEqual(JsonTokenType.None, reader.TokenType);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.EndObject, reader.TokenType);

        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref reader) => reader.Read());
    }

    [TestMethod]
    [DataRow("null", JsonTokenType.Null)]
    [DataRow("false", JsonTokenType.False)]
    [DataRow("true", JsonTokenType.True)]
    [DataRow("42", JsonTokenType.Number)]
    [DataRow("\"string\"", JsonTokenType.String)]
    [DataRow("""{ "key" : "value" }""", JsonTokenType.StartObject)]
    [DataRow("""[{},1,"string",false, true]""", JsonTokenType.StartArray)]
    public void AllowMultipleValues_PartialData_MultiSegment(string jsonValue, JsonTokenType firstTokenType)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        JsonReaderState state = new(options);
        Utf8JsonReader reader = new(JsonTestHelper.GetSequence(jsonValue + " ", 1), isFinalBlock: false, state);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        Assert.IsTrue(reader.TrySkip());
        Assert.IsFalse(reader.Read());

        reader = new Utf8JsonReader(JsonTestHelper.GetSequence(jsonValue, 1), isFinalBlock: true, reader.CurrentState);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        reader.Skip();
        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow("null", JsonTokenType.Null)]
    [DataRow("false", JsonTokenType.False)]
    [DataRow("true", JsonTokenType.True)]
    [DataRow("42", JsonTokenType.Number)]
    [DataRow("\"string\"", JsonTokenType.String)]
    [DataRow("""{ "key" : "value" }""", JsonTokenType.StartObject)]
    [DataRow("""[{},1,"string",false, true]""", JsonTokenType.StartArray)]
    public void AllowMultipleValues_SkipMultipleRepeatingValues_MultiSegment(string jsonValue, JsonTokenType firstTokenType)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        string payload = string.Join("\r\n", Enumerable.Repeat(jsonValue, 10));
        var reader = new Utf8JsonReader(JsonTestHelper.GetSequence(payload, 1), options);

        for (int i = 0; i < 10; i++)
        {
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(firstTokenType, reader.TokenType);
            reader.Skip();
        }
    }

    // For first line in each case:
    //     . represents contiguous characters in input.
    //     | represents end of a segment in the sequence. Character below | is last character of particular segment.
    //
    //     Note: \ for escape sequence has neither a . nor a |
    //
    // Second line in each case represents whether the resulting token after parsing has a value sequence or not.
    //     T(rue) indicates presence of value sequence and F(alse) indicates otherwise. T or F is written above first
    //       character of partiular token.
    //     - indicates that token that begins at character right below it has been skipped during parsing and hence there
    //       is no truth value representation of the same in the expectedValueSequence* in each case.
    [TestMethod]
    //              . .........|.... .... ........ ... ........|............... ...............|......................|||
    //              F T                 F F            T                           F      T           F     F      F   FF
    [DataRow(0, "{\"property name\": [\"value 1\", \"value 2 across sequence\", 12345, 1234567890, true, false, null]}")]

    //              .. .............. .... ........ ... .......|................ .....|.................|......|......|..||
    //              FF F                 F F            T                           T      F           T     T      T   FFF
    [DataRow(1, "[{\"property name\": [\"value 1\", \"value 2 across sequence\", 12345, 1234567890, true, false, null]}]")]

    //              . .............. .................... | . ...........|............ ..................|...... . |..
    // Skip:        F F                 F-                    T                         -                           FF
    // Allow:       F F                 FF                    T                         T                           FF
    [DataRow(2, "{\"property name\": [// comment value\r\n\"value 2 across sequence\"// another comment value\r\n]}")]
    public void CheckOnlyOneOfValueSpanOrSequenceIsSet(int testCase, string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        bool[] expectedHasValueSequence = null;
        bool[] expectedHasValueSequenceSkip = null;
        bool[] expectedHasValueSequenceAllow = null;

        ReadOnlySequence<byte> sequence;
        switch (testCase)
        {
            case 0:
                Debug.Assert(dataUtf8.Length == 95);
                byte[][] buffers = new byte[6][];
                buffers[0] = dataUtf8.AsSpan(0, 10).ToArray();
                buffers[1] = dataUtf8.AsSpan(10, 28).ToArray();
                buffers[2] = dataUtf8.AsSpan(38, 32).ToArray();
                buffers[3] = dataUtf8.AsSpan(70, 23).ToArray();
                buffers[4] = dataUtf8.AsSpan(93, 1).ToArray();
                buffers[5] = dataUtf8.AsSpan(94, 1).ToArray();
                sequence = BufferFactory.Create(buffers);
                expectedHasValueSequence = new bool[] { false, true, false, false, true, false, true, false, false, false, false, false };
                break;

            case 1:
                Debug.Assert(dataUtf8.Length == 97);
                buffers = new byte[7][];
                buffers[0] = dataUtf8.AsSpan(0, 39).ToArray();
                buffers[1] = dataUtf8.AsSpan(39, 22).ToArray();
                buffers[2] = dataUtf8.AsSpan(61, 18).ToArray();
                buffers[3] = dataUtf8.AsSpan(79, 7).ToArray();
                buffers[4] = dataUtf8.AsSpan(86, 7).ToArray();
                buffers[5] = dataUtf8.AsSpan(93, 3).ToArray();
                buffers[6] = dataUtf8.AsSpan(96, 1).ToArray();
                sequence = BufferFactory.Create(buffers);
                expectedHasValueSequence = new bool[] { false, false, false, false, false, true, true, false, true, true, true, false, false, false };
                break;

            case 2:
                Debug.Assert(dataUtf8.Length == 90);
                buffers = new byte[5][];
                buffers[0] = dataUtf8.AsSpan(0, 36).ToArray();
                buffers[1] = dataUtf8.AsSpan(36, 13).ToArray();
                buffers[2] = dataUtf8.AsSpan(49, 30).ToArray();
                buffers[3] = dataUtf8.AsSpan(79, 9).ToArray();
                buffers[4] = dataUtf8.AsSpan(88, 2).ToArray();
                sequence = BufferFactory.Create(buffers);
                expectedHasValueSequenceSkip = new bool[] { false, false, false, true, false, false };
                expectedHasValueSequenceAllow = new bool[] { false, false, false, false, true, true, false, false };
                break;

            default:
                return;
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow && testCase == 2)
            {
                continue;
            }
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

            int index = 0;

            while (json.Read())
            {
                if (testCase == 0 || testCase == 1)
                {
                    Assert.IsTrue(expectedHasValueSequence[index] == json.HasValueSequence, $"{commentHandling}, {testCase}, {index}, {json.HasValueSequence}");
                }
                else
                {
                    if (commentHandling == JsonCommentHandling.Skip)
                    {
                        Assert.IsTrue(expectedHasValueSequenceSkip[index] == json.HasValueSequence, $"{commentHandling}, {testCase}, {index}, {json.HasValueSequence}");
                    }
                    else
                    {
                        Assert.IsTrue(expectedHasValueSequenceAllow[index] == json.HasValueSequence, $"{commentHandling}, {testCase}, {index}, {json.HasValueSequence}");
                    }
                }
                if (json.HasValueSequence)
                {
                    Assert.IsTrue(json.ValueSpan == default, $"Escaped ValueSpan to be empty when HasValueSequence is true. Test case: {testCase}");
                    Assert.IsFalse(json.ValueSequence.IsEmpty, $"Escaped ValueSequence to not be empty when HasValueSequence is true. Test case: {testCase}");
                }
                else
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty, $"Escaped ValueSequence to be empty when HasValueSequence is false. Test case: {testCase}");
                    Assert.IsFalse(json.ValueSpan == default, $"Escaped ValueSpan to not be empty when HasValueSequence is false. Test case: {testCase}");
                }

                index++;
            }
        }
    }

    [TestMethod]
    [DataRow("\"abcdefg\"")]
    [DataRow("12345")]
    [DataRow("12345.0e-3")]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("null")]
    public void CheckOnlyOneOfValueSpanOrSequenceIsSetSingleValue(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

            Assert.IsFalse(json.HasValueSequence);
            Assert.IsTrue(json.ValueSpan == default);
            Assert.IsTrue(json.ValueSequence.IsEmpty);

            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.HasValueSequence);
            Assert.IsTrue(json.ValueSpan == default);
            Assert.IsFalse(json.ValueSequence.IsEmpty);

            // Subsequent calls to Read clears the value properties since Read returned false.
            Assert.IsFalse(json.Read());
            Assert.IsFalse(json.HasValueSequence);
            Assert.IsTrue(json.ValueSpan == default);
            Assert.IsTrue(json.ValueSequence.IsEmpty);
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var json = new Utf8JsonReader(sequence, new JsonReaderOptions { CommentHandling = commentHandling });

            Assert.IsFalse(json.HasValueSequence);
            Assert.IsTrue(json.ValueSpan == default);
            Assert.IsTrue(json.ValueSequence.IsEmpty);

            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.HasValueSequence);
            Assert.IsTrue(json.ValueSpan == default);
            Assert.IsFalse(json.ValueSequence.IsEmpty);

            // Subsequent calls to Read clears the value properties since Read returned false.
            Assert.IsFalse(json.Read());
            Assert.IsFalse(json.HasValueSequence);
            Assert.IsTrue(json.ValueSpan == default);
            Assert.IsTrue(json.ValueSequence.IsEmpty);
        }
    }

    [TestMethod]
    [DynamicData(nameof(CommentTestLineSeparators))]
    public void ConsumeSingleLineCommentMultiSpanTest(string lineSeparator)
    {
        string expected = "Comment";
        string jsonData = "{//" + expected + lineSeparator + "}";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonData);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

        for (int i = 0; i < jsonData.Length; i++)
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });

            var json = new Utf8JsonReader(sequence.Slice(0, i), isFinalBlock: false, state);
            VerifyReadLoop(ref json, expected);

            json = new Utf8JsonReader(sequence.Slice(json.BytesConsumed), isFinalBlock: true, json.CurrentState);
            VerifyReadLoop(ref json, expected);
        }
    }

    [TestMethod]
    public void EmptyJsonMultiSegmentIsInvalid()
    {
        ReadOnlyMemory<byte> dataMemory = Array.Empty<byte>();

        var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, 0));
        ReadOnlyMemory<byte> secondMem = dataMemory.Slice(0, 0);
        BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);

        var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);

        Assert.Throws<JsonException>(() =>
        {
            var json = new Utf8JsonReader(sequence, isFinalBlock: true, state: default);

            Assert.AreEqual(0, json.BytesConsumed);
            Assert.AreEqual(0, json.TokenStartIndex);
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.None, json.TokenType);
            Assert.AreNotEqual(default, json.Position);
            Assert.IsFalse(json.HasValueSequence);
            Assert.IsFalse(json.ValueIsEscaped);
            Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
            Assert.IsTrue(json.ValueSequence.IsEmpty);

            Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
            Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
            Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

            json.Read(); // this should throw
        });
    }

    [TestMethod]
    public void EmptyJsonWithinSequenceIsInvalid()
    {
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(new byte[0], 1);
        var json = new Utf8JsonReader(sequence, isFinalBlock: true, state: default);

        try
        {
            while (json.Read())
                ;
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(0, ex.LineNumber);
            Assert.AreEqual(0, ex.BytePositionInLine);
        }
    }

    [TestMethod]
    public void InitialStateMultiSegment()
    {
        byte[] utf8 = "1"u8.ToArray();
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        var json = new Utf8JsonReader(sequence, isFinalBlock: true, state: default);

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreNotEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    public void InitialStateSimpleCtorMultiSegment()
    {
        byte[] utf8 = "1"u8.ToArray();
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        var json = new Utf8JsonReader(sequence);

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreNotEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidJsonStrings))]
    public void InvalidJsonMultiSegmentByOne(string jsonString, int expectedlineNumber, int expectedBytePosition, int maxDepth = 64)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

            SpanSequenceStatesAreEqualInvalidJson(dataUtf8, sequence, maxDepth, commentHandling);

            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException for multi-segment data was not thrown.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(InvalidJsonStrings))]
    public void InvalidJsonMultiSegmentWithEmptyFirst(string jsonString, int expectedlineNumber, int expectedBytePosition, int maxDepth = 64)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

            ReadOnlyMemory<byte> dataMemory = dataUtf8;
            var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, 0));
            ReadOnlyMemory<byte> secondMem = dataMemory;
            BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
            var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);

            SpanSequenceStatesAreEqualInvalidJson(dataUtf8, sequence, maxDepth, commentHandling);

            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException for multi-segment data was not thrown.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
            }
        }
    }

    [TestMethod]
    [DataRow("0{", 1, false, 0, '{')]
    [DataRow("0e+1b", 4, false, 0, 'b')]
    [DataRow("1e+0}", 4, true, 4, '}')]
    [DataRow("12345.1. ", 7, false, 0, '.')]
    [DataRow("- ", 1, false, 0, ' ')]
    [DataRow("-f ", 1, false, 0, 'f')]
    [DataRow("-} ", 1, false, 0, '}')]
    [DataRow("1.f ", 2, false, 0, 'f')]
    [DataRow("1.} ", 2, false, 0, '}')]
    [DataRow("0. ", 2, false, 0, ' ')]
    [DataRow("0.1f ", 3, false, 0, 'f')]
    [DataRow("0.1} ", 3, true, 3, '}')]
    [DataRow("0.1e1f ", 5, false, 0, 'f')]
    [DataRow("+0 ", 0, false, 0, '+')]
    [DataRow("+1 ", 0, false, 0, '+')]
    [DataRow("0e ", 2, false, 0, ' ')]
    [DataRow("0.1e ", 4, false, 0, ' ')]
    [DataRow("01 ", 1, false, 0, '1')]
    [DataRow("1a ", 1, false, 0, 'a')]
    [DataRow("-01 ", 2, false, 0, '1')]
    [DataRow("10.5e ", 5, false, 0, ' ')]
    [DataRow("10.5e- ", 6, false, 0, ' ')]
    [DataRow("10.5e+ ", 6, false, 0, ' ')]
    [DataRow("10.5e-0.2 ", 7, false, 0, '.')]
    public void InvalidJsonNumberVariousSegmentSizes(string input, int expectedBytePositionInLine, bool firstReadSuccessful, int expectedConsumed, char invalidChar)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);

        var jsonReader = new Utf8JsonReader(utf8);
        InvalidReadNumberHelper(ref jsonReader, expectedBytePositionInLine, firstReadSuccessful, expectedConsumed, invalidChar);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        jsonReader = new Utf8JsonReader(sequence);
        InvalidReadNumberHelper(ref jsonReader, expectedBytePositionInLine, firstReadSuccessful, expectedConsumed, invalidChar);

        for (int splitLocation = 0; splitLocation < utf8.Length; splitLocation++)
        {
            sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            jsonReader = new Utf8JsonReader(sequence);
            InvalidReadNumberHelper(ref jsonReader, expectedBytePositionInLine, firstReadSuccessful, expectedConsumed, invalidChar);
        }

        for (int firstSplit = 0; firstSplit < utf8.Length; firstSplit++)
        {
            for (int secondSplit = firstSplit; secondSplit < utf8.Length; secondSplit++)
            {
                sequence = JsonTestHelper.CreateSegments(utf8, firstSplit, secondSplit);
                jsonReader = new Utf8JsonReader(sequence);
                InvalidReadNumberHelper(ref jsonReader, expectedBytePositionInLine, firstReadSuccessful, expectedConsumed, invalidChar);
            }
        }
    }

    [TestMethod]
    [DataRow("\"abc\\t", 6)]
    [DataRow("\"abc\\t\\\"", 8)]
    [DataRow("\"abc\\n", 6)]
    [DataRow("\"abc\\nabc", 9)]
    [DataRow("\"abc\\n\\\"", 8)]
    [DataRow("\"hi\\n\\n\\n\\q\"", 10)]
    [DataRow("\"abc\u0010\"", 4)]
    [DataRow("\"abc\u0010abc\"", 4)]
    [DataRow("\"abc\\p\"", 5)]
    [DataRow("\"abc\\p", 5)]
    [DataRow("\"abc\\u\"", 6)]
    [DataRow("\"abc\\uX\"", 6)]
    [DataRow("\"abc\\u", 6)]
    [DataRow("\"abc\\u1\"", 7)]
    [DataRow("\"abc\\u1X\"", 7)]
    [DataRow("\"abc\\u1", 7)]
    [DataRow("\"abc\\u12\"", 8)]
    [DataRow("\"abc\\u12X\"", 8)]
    [DataRow("\"abc\\u12", 8)]
    [DataRow("\"abc\\u123\"", 9)]
    [DataRow("\"abc\\u123X\"", 9)]
    [DataRow("\"abc\\u123", 9)]
    [DataRow("\"abc\\u1234\\\"", 12)]
    [DataRow("\"abc\\u1234X\\\"", 13)]
    [DataRow("\"abc\\u1234", 10)]
    [DataRow("\"abcdefg", 8)]
    [DataRow("\"\\u1234", 7)]
    [DataRow("{\"name\": \"abc\\t", 6 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\t\\\"", 8 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\n", 6 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\nabc", 9 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\n\\\"", 8 + 9, 2, 9)]
    [DataRow("{\"name\": \"hi\\n\\n\\n\\q\"", 10 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\u0010\"", 4 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\u0010abc\"", 4 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\p\"", 5 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\p", 5 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u\"", 6 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\uX\"", 6 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u", 6 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u1\"", 7 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u1X\"", 7 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u1", 7 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u12\"", 8 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u12X\"", 8 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u12", 8 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u123\"", 9 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u123X\"", 9 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u123", 9 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u1234\\\"", 12 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u1234X\\\"", 13 + 9, 2, 9)]
    [DataRow("{\"name\": \"abc\\u1234", 10 + 9, 2, 9)]
    [DataRow("{\"name\": \"abcdefg", 8 + 9, 2, 9)]
    [DataRow("{\"name\": \"\\u1234", 7 + 9, 2, 9)]
    public void InvalidJsonStringVariousSegmentSizes(string input, int expectedBytePositionInLine, int numberOfSuccessfulReads = 0, int expectedConsumed = 0)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);

        var jsonReader = new Utf8JsonReader(utf8);
        InvalidReadStringHelper(ref jsonReader, expectedBytePositionInLine, numberOfSuccessfulReads, expectedConsumed);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        jsonReader = new Utf8JsonReader(sequence);
        InvalidReadStringHelper(ref jsonReader, expectedBytePositionInLine, numberOfSuccessfulReads, expectedConsumed);

        for (int splitLocation = 0; splitLocation < utf8.Length; splitLocation++)
        {
            sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            jsonReader = new Utf8JsonReader(sequence);
            InvalidReadStringHelper(ref jsonReader, expectedBytePositionInLine, numberOfSuccessfulReads, expectedConsumed);
        }

        for (int firstSplit = 0; firstSplit < utf8.Length; firstSplit++)
        {
            for (int secondSplit = firstSplit; secondSplit < utf8.Length; secondSplit++)
            {
                sequence = JsonTestHelper.CreateSegments(utf8, firstSplit, secondSplit);
                jsonReader = new Utf8JsonReader(sequence);
                InvalidReadStringHelper(ref jsonReader, expectedBytePositionInLine, numberOfSuccessfulReads, expectedConsumed);
            }
        }
    }

    [TestMethod]
    [DataRow("/**/", "", 1, 4)]
    [DataRow("/**/", "", 2, 4)]
    [DataRow("/**/", "", 3, 4)]
    [DataRow("/**/", "", 100, 4)]
    [DataRow("/*a*/", "a", 1, 5)]
    [DataRow("/*a*/", "a", 2, 5)]
    [DataRow("/*a*/", "a", 3, 5)]
    [DataRow("/*a*/", "a", 100, 5)]
    [DataRow("/*abc*/", "abc", 1, 7)]
    [DataRow("/*abc*/", "abc", 2, 7)]
    [DataRow("/*abc*/", "abc", 3, 7)]
    [DataRow("/*abc*/", "abc", 100, 7)]
    [DataRow("/*\u2028*/", "\u2028", 1, 7)]
    [DataRow("/*\u2028*/", "\u2028", 2, 7)]
    [DataRow("/*\u2028*/", "\u2028", 3, 7)]
    [DataRow("/*\u2028*/", "\u2028", 100, 7)]
    [DataRow("/*\u2029*/", "\u2029", 1, 7)]
    [DataRow("/*\u2029*/", "\u2029", 2, 7)]
    [DataRow("/*\u2029*/", "\u2029", 3, 7)]
    [DataRow("/*\u2029*/", "\u2029", 100, 7)]
    public void JsonWithMultiLineCommentWithNoLineEndingsFinalBlockMultiSegment(string jsonString, string expectedComment, int segmentSize, int expectedBytesConsumed)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);
        var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

        Assert.IsTrue(json.Read());
        Assert.AreEqual(JsonTokenType.Comment, json.TokenType);
        Assert.AreEqual(expectedComment, json.GetComment());
        Assert.IsFalse(json.Read());
        Assert.AreEqual(expectedBytesConsumed, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("//\u2028", 1)]
    [DataRow("//\u2028", 2)]
    [DataRow("//\u2028", 3)]
    [DataRow("//\u2028", 100)]
    [DataRow("//\u2029", 1)]
    [DataRow("//\u2029", 2)]
    [DataRow("//\u2029", 3)]
    [DataRow("//\u2029", 100)]
    [DataRow("// \u2028", 1)]
    [DataRow("// \u2028", 2)]
    [DataRow("// \u2028", 3)]
    [DataRow("// \u2028", 100)]
    [DataRow("//   \u2028", 1)]
    [DataRow("//   \u2028", 2)]
    [DataRow("//   \u2028", 3)]
    [DataRow("//   \u2028", 100)]
    [DataRow("//  \u2029 ", 1)]
    [DataRow("//  \u2029 ", 2)]
    [DataRow("//  \u2029 ", 3)]
    [DataRow("//  \u2029 ", 100)]
    [DataRow("//  \u2029  ", 1)]
    [DataRow("//  \u2029  ", 2)]
    [DataRow("//  \u2029  ", 3)]
    [DataRow("//  \u2029  ", 100)]
    public void JsonWithSingleLineCommentEndingWithNonStandardLineEndingMultiSegment(string jsonString, int segmentSize)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);

        foreach (JsonCommentHandling jsonCommentHandling in typeof(JsonCommentHandling).GetEnumValues())
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = jsonCommentHandling });

            foreach (bool isFinalBlock in new bool[] { false, true })
            {
                var json = new Utf8JsonReader(sequence, isFinalBlock, state);

                try
                {
                    json.Read();
                    Assert.Fail($"Expected JsonException was not thrown. CommentHandling = {jsonCommentHandling}");
                }
                catch (JsonException) { }
            }
        }
    }

    [TestMethod]
    [DataRow("{ \"foo\" : \"bar\" //\u2028\n}", 1)]
    [DataRow("{ \"foo\" : \"bar\" //\u2028\n}", 2)]
    [DataRow("{ \"foo\" : \"bar\" //\u2028\n}", 3)]
    [DataRow("{ \"foo\" : \"bar\" //\u2028\n}", 100)]
    public void JsonWithSingleLineCommentInTheMiddleOfThePayloadEndingWithNonStandardLineEndingMultiSegment(string jsonString, int segmentSize)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);

        foreach (JsonCommentHandling jsonCommentHandling in typeof(JsonCommentHandling).GetEnumValues())
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = jsonCommentHandling });

            foreach (bool isFinalBlock in new bool[] { false, true })
            {
                var json = new Utf8JsonReader(sequence, isFinalBlock, state);

                try
                {
                    Assert.IsTrue(json.Read()); // {
                    Assert.IsTrue(json.Read()); // "foo"
                    Assert.IsTrue(json.Read()); // "bar"
                    json.Read(); // bad comment
                    Assert.Fail($"Expected JsonException was not thrown. CommentHandling = {jsonCommentHandling}");
                }
                catch (JsonException) { }
            }
        }
    }

    [TestMethod]
    [DataRow("//", "", 1, 2)]
    [DataRow("//", "", 2, 2)]
    [DataRow("//", "", 3, 2)]
    [DataRow("//", "", 100, 2)]
    [DataRow("//a", "a", 1, 3)]
    [DataRow("//a", "a", 2, 3)]
    [DataRow("//a", "a", 3, 3)]
    [DataRow("//a", "a", 100, 3)]
    [DataRow("//abc", "abc", 1, 5)]
    [DataRow("//abc", "abc", 2, 5)]
    [DataRow("//abc", "abc", 3, 5)]
    [DataRow("//abc", "abc", 100, 5)]
    public void JsonWithSingleLineCommentWithNoLineEndingsFinalBlockMultiSegment(string jsonString, string expectedComment, int segmentSize, int expectedBytesConsumed)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);
        var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

        Assert.IsTrue(json.Read());
        Assert.AreEqual(JsonTokenType.Comment, json.TokenType);
        Assert.AreEqual(expectedComment, json.GetComment());
        Assert.IsFalse(json.Read());
        Assert.AreEqual(expectedBytesConsumed, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("//", 1)]
    [DataRow("//", 2)]
    [DataRow("//", 3)]
    [DataRow("//", 100)]
    [DataRow("//a", 1)]
    [DataRow("//a", 2)]
    [DataRow("//a", 3)]
    [DataRow("//a", 100)]
    [DataRow("//abc", 1)]
    [DataRow("//abc", 2)]
    [DataRow("//abc", 3)]
    [DataRow("//abc", 100)]
    public void JsonWithSingleLineCommentWithNoLineEndingsNonFinalBlockMultiSegment(string jsonString, int segmentSize)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);
        var json = new Utf8JsonReader(sequence, isFinalBlock: false, state);

        Assert.IsFalse(json.Read());
        Assert.AreEqual(0, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("//", "", 1)]
    [DataRow("//", "", 2)]
    [DataRow("//", "", 3)]
    [DataRow("//", "", 100)]
    [DataRow("//a", "a", 1)]
    [DataRow("//a", "a", 2)]
    [DataRow("//a", "a", 3)]
    [DataRow("//a", "a", 100)]
    [DataRow("//abc", "abc", 1)]
    [DataRow("//abc", "abc", 2)]
    [DataRow("//abc", "abc", 3)]
    [DataRow("//abc", "abc", 100)]
    public void JsonWithSingleLineCommentWithRegularLineEndingMultiSegment(string jsonStringWithoutLineEnding, string expectedComment, int segmentSize)
    {
        foreach (string lineEnding in new string[] { "\r", "\r ", "\r\n", "\r\n ", "\n", "\n ", "" })
        {
            foreach (bool isFinalBlock in new bool[] { false, true })
            {
                if (!isFinalBlock && (lineEnding == "\r" || lineEnding == ""))
                {
                    // In this case parser would return false on the first Read (and check for \n on the next segment)
                    // which is not the purpose of this test and is covered separately
                    continue;
                }

                byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonStringWithoutLineEnding + lineEnding);
                var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
                ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);

                var json = new Utf8JsonReader(sequence, isFinalBlock, state);

                Assert.IsTrue(json.Read(), $"Expected read to return true. IsFinalBlock = {isFinalBlock}; LineEnding = {string.Join("", lineEnding.Select((c) => ((byte)c).ToString("X2")))}");
                Assert.AreEqual(JsonTokenType.Comment, json.TokenType);
                Assert.AreEqual(expectedComment, json.GetComment());
                Assert.IsFalse(json.Read());
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithInvalidTrailingCommasAndComments))]
    public void JsonWithTrailingCommasAndCommentsMultiSegment_Invalid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(sequence, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(sequence, state, allowTrailingCommas, expectThrow: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommasAndComments))]
    public void JsonWithTrailingCommasAndCommentsMultiSegment_Valid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(sequence, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(sequence, state, allowTrailingCommas, expectThrow: false);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithInvalidTrailingCommas))]
    public void JsonWithTrailingCommasMultiSegment_Invalid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(sequence, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(sequence, state, allowTrailingCommas, expectThrow: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommas))]
    public void JsonWithTrailingCommasMultiSegment_Valid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        {
            JsonReaderState state = default;
            TrailingCommasHelper(sequence, state, allow: false, expectThrow: true);
        }

        {
            var state = new JsonReaderState(options: default);
            TrailingCommasHelper(sequence, state, allow: false, expectThrow: true);
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(sequence, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(sequence, state, allowTrailingCommas, expectThrow: false);
        }
    }

    [TestMethod]
    [DataRow("/*", 1)]
    [DataRow("/*", 2)]
    [DataRow("/*", 100)]
    [DataRow("/*  ", 1)]
    [DataRow("/*  ", 2)]
    [DataRow("/*  ", 3)]
    [DataRow("/*  ", 100)]
    [DataRow("/**", 1)]
    [DataRow("/**", 2)]
    [DataRow("/**", 3)]
    [DataRow("/**", 100)]
    [DataRow("/*  *", 1)]
    [DataRow("/*  *", 2)]
    [DataRow("/*  *", 3)]
    [DataRow("/*  *", 100)]
    public void JsonWithUnfinishedMultiLineCommentNonFinalBlockMultiSegment(string jsonString, int segmentSize)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, segmentSize);
        var json = new Utf8JsonReader(sequence, isFinalBlock: false, state);

        Assert.IsFalse(json.Read());
        Assert.AreEqual(0, json.BytesConsumed);
    }

    [TestMethod]
    public void MultiSegmentInvalidLiteral()
    {
        // Tests edge case in Utf8JsonReader.CheckLiteralMultiSegment error handling
        // when segment has invalid literal and more characters than stack-allocated span (5)
        IEnumerable<byte[]> bytes = ["tr"u8.ToArray(), "uabcdef"u8.ToArray()];
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(bytes);

        var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state: default);

        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref reader) =>
        {
            reader.Read();
        });
    }

    [TestMethod]
    public void MultiSegmentInvalidLiteralInObject()
    {
        // Tests edge case in Utf8JsonReader.CheckLiteralMultiSegment error handling
        // when segment has invalid literal after having done some prior reads (i.e. start of object and property name)
        IEnumerable<byte[]> bytes = ["{\"foo\""u8.ToArray(), ":nul234"u8.ToArray()];
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(bytes);

        var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state: default);
        Assert.IsTrue(reader.Read()); // StartObject
        Assert.IsTrue(reader.Read()); // PropertyName

        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref reader) =>
        {
            reader.Read();
        });
    }

    [TestMethod]
    [DataRow("{]")]
    [DataRow("[}")]
    [DataRow("{// comment\n]")]
    [DataRow("[// comment\n}")]
    [DataRow("{,}")]
    [DataRow("[,]")]
    [DataRow("{// comment\n,}")]
    [DataRow("[// comment\n,]")]
    [DataRow("123, ")]
    [DataRow("\"abc\", ")]
    [DataRow("123, // comment\n")]
    [DataRow("\"abc\", // comment\n")]
    [DataRow("123 // comment\n,")]
    [DataRow("\"abc\" // comment\n,")]
    [DataRow("{\"Property1\": [ 42], 5}")]
    [DataRow("{\"Property1\": [ 42], // comment\n5}")]
    [DataRow("{\"Property1\": [ 42], // comment\r5}")]
    [DataRow("{\"Property1\": [ 42], // comment\r\n5}")]
    [DataRow("{\"Property1\": [ 42], /*comment*/5}")]
    [DataRow("{\"Property1\": [ 42] // comment\n,5}")]
    [DataRow("{\"Property1\": [ 42], // comment\n // comment\n5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, 5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\n5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\r5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\r\n5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, /*comment*/5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42} // comment\n,5}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\n // comment\n5}")]
    [DataRow("{// comment\n5}")]
    public void ReadInvalidJsonStringsWithComments(string jsonString)
    {
        byte[] input = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var options = new JsonReaderOptions() { CommentHandling = commentHandling };
            var optionsWithTrailing = new JsonReaderOptions() { CommentHandling = commentHandling, AllowTrailingCommas = true };

            var reader = new Utf8JsonReader(input, options);
            ValidateThrows(ref reader);

            reader = new Utf8JsonReader(input, optionsWithTrailing);
            ValidateThrows(ref reader);

            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(input, 1);
            reader = new Utf8JsonReader(sequence, options);
            ValidateThrows(ref reader);

            reader = new Utf8JsonReader(sequence, optionsWithTrailing);
            ValidateThrows(ref reader);

            for (int splitLocation = 0; splitLocation < input.Length; splitLocation++)
            {
                sequence = JsonTestHelper.CreateSegments(input, splitLocation);
                reader = new Utf8JsonReader(sequence, options);
                ValidateThrows(ref reader);

                reader = new Utf8JsonReader(sequence, optionsWithTrailing);
                ValidateThrows(ref reader);
            }

            for (int firstSplit = 0; firstSplit < input.Length; firstSplit++)
            {
                for (int secondSplit = firstSplit; secondSplit < input.Length; secondSplit++)
                {
                    sequence = JsonTestHelper.CreateSegments(input, firstSplit, secondSplit);
                    reader = new Utf8JsonReader(sequence, options);
                    ValidateThrows(ref reader);

                    reader = new Utf8JsonReader(sequence, optionsWithTrailing);
                    ValidateThrows(ref reader);
                }
            }
        }
    }

    [TestMethod]
    [DataRow("123 ")]
    [DataRow("\"abc\" ")]
    [DataRow("123 // comment\n")]
    [DataRow("\"abc\" // comment\n")]
    [DataRow("{// comment\n}")]
    [DataRow("[// comment\n]")]
    [DataRow("{\"Property1\": [ 42], \"Property2\": 42}")]
    [DataRow("{\"Property1\": [ 42], // comment\n\"Property2\": 42}")]
    [DataRow("{\"Property1\": [ 42], // comment\r\"Property2\": 42}")]
    [DataRow("{\"Property1\": [ 42], // comment\r\n\"Property2\": 42}")]
    [DataRow("{\"Property1\": [ 42], /*comment*/\"Property2\": 42}")]
    [DataRow("{\"Property1\": [ 42] // comment\n,\"Property2\": 42}")]
    [DataRow("{\"Property1\": [ 42], // comment\n // comment\n\"Property2\": 42}")]
    [DataRow("[[ 42], // comment\n 42]")]
    [DataRow("[[ 42] // comment\n, 42]")]
    [DataRow("[[ 42, // comment\n 43], // comment\n 42]")]
    [DataRow("[[ \"42\", // comment\n 43], // comment\n 42]")]
    [DataRow("[[ true, // comment\n 43], // comment\n 42]")]
    [DataRow("[[ false, // comment\n 43], // comment\n 42]")]
    [DataRow("[[ null, // comment\n 43], // comment\n 42]")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, \"Property2\": 42}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\n\"Property2\": 42}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\r\"Property2\": 42}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\r\n\"Property2\": 42}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, /*comment*/\"Property2\": 42}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42} // comment\n,\"Property2\": 42}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\n // comment\n\"Property2\": 42}")]
    [DataRow("[{\"Property1\": 42}, // comment\n 42]")]
    [DataRow("[{\"Property1\": 42} // comment\n, 42]")]
    [DataRow("[{\"Property1\": 42, // comment\n \"prop\": 43 }, // comment\n 42]")]
    [DataRow("[{\"Property1\": \"42\", // comment\n \"prop\": 43 }, // comment\n 42]")]
    [DataRow("[{\"Property1\": true, // comment\n \"prop\": 43 }, // comment\n 42]")]
    [DataRow("[{\"Property1\": false, // comment\n \"prop\": 43 }, // comment\n 42]")]
    [DataRow("[{\"Property1\": null, // comment\n \"prop\": 43 }, // comment\n 42]")]
    public void ReadJsonStringsWithComments(string jsonString)
    {
        byte[] input = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var options = new JsonReaderOptions() { CommentHandling = commentHandling };
            var optionsWithTrailing = new JsonReaderOptions() { CommentHandling = commentHandling, AllowTrailingCommas = true };

            var reader = new Utf8JsonReader(input, options);
            ReadWithCommentsHelper(ref reader, input.Length);

            reader = new Utf8JsonReader(input, optionsWithTrailing);
            ReadWithCommentsHelper(ref reader, input.Length);

            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(input, 1);
            reader = new Utf8JsonReader(sequence, options);
            ReadWithCommentsHelper(ref reader, input.Length);

            reader = new Utf8JsonReader(sequence, optionsWithTrailing);
            ReadWithCommentsHelper(ref reader, input.Length);

            for (int splitLocation = 0; splitLocation < input.Length; splitLocation++)
            {
                sequence = JsonTestHelper.CreateSegments(input, splitLocation);
                reader = new Utf8JsonReader(sequence, options);
                ReadWithCommentsHelper(ref reader, input.Length);

                reader = new Utf8JsonReader(sequence, optionsWithTrailing);
                ReadWithCommentsHelper(ref reader, input.Length);
            }

            for (int firstSplit = 0; firstSplit < input.Length; firstSplit++)
            {
                for (int secondSplit = firstSplit; secondSplit < input.Length; secondSplit++)
                {
                    sequence = JsonTestHelper.CreateSegments(input, firstSplit, secondSplit);
                    reader = new Utf8JsonReader(sequence, options);
                    ReadWithCommentsHelper(ref reader, input.Length);

                    reader = new Utf8JsonReader(sequence, optionsWithTrailing);
                    ReadWithCommentsHelper(ref reader, input.Length);
                }
            }
        }
    }

    [TestMethod]
    [DataRow("{\"Property1\": [ 42], }")]
    [DataRow("{\"Property1\": [ 42], // comment\n}")]
    [DataRow("{\"Property1\": [ 42], // comment\r}")]
    [DataRow("{\"Property1\": [ 42], // comment\r\n}")]
    [DataRow("{\"Property1\": [ 42], /*comment*/}")]
    [DataRow("{\"Property1\": [ 42] // comment\n,}")]
    [DataRow("{\"Property1\": [ 42], // comment\n // comment\n}")]
    [DataRow("[[ 42], // comment\n ]")]
    [DataRow("[[ 42] // comment\n, ]")]
    [DataRow("[[ 42, // comment\n ], // comment\n ]")]
    [DataRow("[[ \"42\", // comment\n ], // comment\n ]")]
    [DataRow("[[ true, // comment\n ], // comment\n ]")]
    [DataRow("[[ false, // comment\n ], // comment\n ]")]
    [DataRow("[[ null, // comment\n ], // comment\n ]")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, }")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\n}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\r}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\r\n}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, /*comment*/}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42} // comment\n,}")]
    [DataRow("{\"Property1\": {\"Property1.1\": 42}, // comment\n // comment\n}")]
    [DataRow("[{\"Property1\": 42}, // comment\n ]")]
    [DataRow("[{\"Property1\": 42} // comment\n, ]")]
    [DataRow("[{\"Property1\": 42, // comment\n }, // comment\n ]")]
    [DataRow("[{\"Property1\": \"42\", // comment\n }, // comment\n ]")]
    [DataRow("[{\"Property1\": true, // comment\n }, // comment\n ]")]
    [DataRow("[{\"Property1\": false, // comment\n }, // comment\n ]")]
    [DataRow("[{\"Property1\": null, // comment\n }, // comment\n ]")]
    public void ReadJsonStringsWithCommentsAndTrailingCommas(string jsonString)
    {
        byte[] input = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var options = new JsonReaderOptions() { CommentHandling = commentHandling };
            var optionsWithTrailing = new JsonReaderOptions() { CommentHandling = commentHandling, AllowTrailingCommas = true };

            var reader = new Utf8JsonReader(input, options);
            ReadWithCommentsHelper(ref reader, -1, validateThrows: true);

            reader = new Utf8JsonReader(input, optionsWithTrailing);
            ReadWithCommentsHelper(ref reader, input.Length);

            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(input, 1);
            reader = new Utf8JsonReader(sequence, options);
            ReadWithCommentsHelper(ref reader, -1, validateThrows: true);

            reader = new Utf8JsonReader(sequence, optionsWithTrailing);
            ReadWithCommentsHelper(ref reader, input.Length);

            for (int splitLocation = 0; splitLocation < input.Length; splitLocation++)
            {
                sequence = JsonTestHelper.CreateSegments(input, splitLocation);
                reader = new Utf8JsonReader(sequence, options);
                ReadWithCommentsHelper(ref reader, input.Length, validateThrows: true);

                reader = new Utf8JsonReader(sequence, optionsWithTrailing);
                ReadWithCommentsHelper(ref reader, input.Length);
            }

            for (int firstSplit = 0; firstSplit < input.Length; firstSplit++)
            {
                for (int secondSplit = firstSplit; secondSplit < input.Length; secondSplit++)
                {
                    sequence = JsonTestHelper.CreateSegments(input, firstSplit, secondSplit);
                    reader = new Utf8JsonReader(sequence, options);
                    ReadWithCommentsHelper(ref reader, input.Length, validateThrows: true);

                    reader = new Utf8JsonReader(sequence, optionsWithTrailing);
                    ReadWithCommentsHelper(ref reader, input.Length);
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonTokenWithExtraValueAndComments))]
    public void ReadJsonTokenWithExtraValueAndCommentsAppendedMultiSegment(string jsonString)
    {
        jsonString = "  /* comment */  /* comment */  " + jsonString;
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            TestReadTokenWithExtra(sequence, commentHandling, isFinalBlock: false, commentsAppended: true);
            TestReadTokenWithExtra(sequence, commentHandling, isFinalBlock: true, commentsAppended: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonTokenWithExtraValueAndComments))]
    public void ReadJsonTokenWithExtraValueAndCommentsMultiSegment(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            TestReadTokenWithExtra(sequence, commentHandling, isFinalBlock: false);
            TestReadTokenWithExtra(sequence, commentHandling, isFinalBlock: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonTokenWithExtraValue))]
    public void ReadJsonTokenWithExtraValueMultiSegment(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            TestReadTokenWithExtra(sequence, commentHandling, isFinalBlock: false);
            TestReadTokenWithExtra(sequence, commentHandling, isFinalBlock: true);
        }
    }

    [TestMethod]
    [DataRow("2e2", 200, 3)]
    [DataRow("2e+2", 200, 4)]
    [DataRow("123e-01", 12.3, 7)]
    [DataRow("0", 0, 1)]
    [DataRow("0.1", 0.1, 3)]
    [DataRow("-0", 0, 2)]
    [DataRow("-0.1", -0.1, 4)]
    [DataRow("   2e2   ", 200, 3)]
    [DataRow("   2e+2   ", 200.0, 4L)]
    [DataRow("   123e-01   ", 12.3, 7L)]
    [DataRow("   0   ", 0.0, 1L)]
    [DataRow("   0.1   ", 0.1, 3L)]
    [DataRow("   -0   ", 0.0, 2L)]
    [DataRow("   -0.1   ", -0.1, 4L)]
    [DataRow("[2e2]", 200.0, 3L)]
    [DataRow("[2e+2]", 200.0, 4L)]
    [DataRow("[123e-01]", 12.3, 7L)]
    [DataRow("[0]", 0.0, 1L)]
    [DataRow("[0.1]", 0.1, 3L)]
    [DataRow("[-0]", 0.0, 2L)]
    [DataRow("[-0.1]", -0.1, 4L)]
    [DataRow("{\"foo\": 2e2}", 200.0, 3L)]
    [DataRow("{\"foo\": 2e+2}", 200.0, 4L)]
    [DataRow("{\"foo\": 123e-01}", 12.3, 7L)]
    [DataRow("{\"foo\": 0}", 0.0, 1L)]
    [DataRow("{\"foo\": 0.1}", 0.1, 3L)]
    [DataRow("{\"foo\": -0}", 0.0, 2L)]
    [DataRow("{\"foo\": -0.1}", -0.1, 4L)]
    public void ReadJsonWithNumberVariousSegmentSizes(string input, double expectedValue, long expectedTokenLength)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);

        var jsonReader = new Utf8JsonReader(utf8);
        ReadDoubleHelper(ref jsonReader, utf8.Length, expectedValue, expectedTokenLength);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        jsonReader = new Utf8JsonReader(sequence);
        ReadDoubleHelper(ref jsonReader, utf8.Length, expectedValue, expectedTokenLength);

        for (int splitLocation = 0; splitLocation < utf8.Length; splitLocation++)
        {
            sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            jsonReader = new Utf8JsonReader(sequence);
            ReadDoubleHelper(ref jsonReader, utf8.Length, expectedValue, expectedTokenLength);
        }

        for (int firstSplit = 0; firstSplit < utf8.Length; firstSplit++)
        {
            for (int secondSplit = firstSplit; secondSplit < utf8.Length; secondSplit++)
            {
                sequence = JsonTestHelper.CreateSegments(utf8, firstSplit, secondSplit);
                jsonReader = new Utf8JsonReader(sequence);
                ReadDoubleHelper(ref jsonReader, utf8.Length, expectedValue, expectedTokenLength);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(SingleValueJson))]
    public void SingleJsonValueMultiSegment(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var sequence = new ReadOnlySequence<byte>(dataUtf8);
        TestReadingSingleValueJson(dataUtf8, sequence, expectedString);

        sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
        TestReadingSingleValueJson(dataUtf8, sequence, expectedString);

        var firstSegment = new BufferSegment<byte>(ReadOnlyMemory<byte>.Empty);
        ReadOnlyMemory<byte> secondMem = dataUtf8;
        BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
        sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);
        TestReadingSingleValueJson(dataUtf8, sequence, expectedString);
    }

    [TestMethod]
    [DynamicData(nameof(CommentTestLineSeparators))]
    public void SkipSingleLineCommentMultiSpanTest(string lineSeparator)
    {
        string expected = "Comment";
        string jsonData = "{//" + expected + lineSeparator + "}";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonData);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

        for (int i = 0; i < jsonData.Length; i++)
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            var json = new Utf8JsonReader(sequence.Slice(0, i), isFinalBlock: false, state);
            VerifyReadLoop(ref json, null);

            json = new Utf8JsonReader(sequence.Slice(json.BytesConsumed), isFinalBlock: true, json.CurrentState);
            VerifyReadLoop(ref json, null);
        }
    }

    [TestMethod]
    public void StateRecoveryMultiSegment()
    {
        byte[] utf8 = "[1]"u8.ToArray();
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        var json = new Utf8JsonReader(sequence, isFinalBlock: false, state: default);

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreNotEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsTrue(json.Read());

        Assert.AreEqual(2, json.BytesConsumed);
        Assert.AreEqual(1, json.TokenStartIndex);
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.Number, json.TokenType);
        Assert.AreNotEqual(default, json.Position);
        Assert.IsTrue(json.HasValueSequence);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.ToArray().AsSpan().SequenceEqual(new byte[] { (byte)'1' }));

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        JsonReaderState state = json.CurrentState;

        json = new Utf8JsonReader(sequence.Slice(json.Position), isFinalBlock: true, state);

        Assert.AreEqual(0, json.BytesConsumed);    // Not retained
        Assert.AreEqual(0, json.TokenStartIndex);  // Not retained
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.Number, json.TokenType);
        Assert.AreNotEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, true, 1)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, true, 2)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, true, 3)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, false, 1)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, false, 2)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, false, 3)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, true, 1)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, true, 2)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, true, 3)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, false, 1)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, false, 2)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, false, 3)]
    public void TestBOMWithSingleJsonValueMultiSegment(byte[] utf8BomAndValue, bool isFinalBlock, int segmentSize)
    {
        Assert.Throws<JsonException>(() =>
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8BomAndValue, segmentSize);
            var json = new Utf8JsonReader(sequence, isFinalBlock: isFinalBlock, state: default);
            json.Read();
        });
    }

    // TestCaseType is only used to give the json strings a descriptive name.
    [TestMethod]
    [TestCategory("outerloop")]
    [DynamicData(nameof(LargeTestCases))]
    public void TestJsonReaderLargestUtf8SegmentSizeOne(bool compactData, TestCaseType type, string jsonString)
    {
        // Skipping really large JSON since slicing them (O(n^2)) is too slow.
        if (type == TestCaseType.Json40KB || type == TestCaseType.Json400KB || type == TestCaseType.ProjectLockJson)
        {
            return;
        }

        ReadPartialSegmentSizeOne(compactData, type, jsonString);
    }

    // TestCaseType is only used to give the json strings a descriptive name.
    [TestMethod]
    [DynamicData(nameof(LargeTestCases))]
    public void TestJsonReaderLargeUtf8SegmentSizeOne(bool compactData, TestCaseType type, string jsonString)
    {
        // Skipping really large JSON on Browser to prevent OOM
        ////if (PlatformDetection.IsBrowser && (type == TestCaseType.Json40KB || type == TestCaseType.Json400KB || type == TestCaseType.ProjectLockJson))
        ////{
        ////    return;
        ////}

        ReadFullySegmentSizeOne(compactData, type, jsonString);
    }

    // TestCaseType is only used to give the json strings a descriptive name.
    [TestMethod]
    // Skipping large JSON since slicing them (O(n^2)) is too slow.
    [DynamicData(nameof(SmallTestCases))]
    public void TestJsonReaderUtf8SegmentSizeOne(bool compactData, TestCaseType type, string jsonString)
    {
        ReadPartialSegmentSizeOne(compactData, type, jsonString);
    }

    [TestMethod]
    public void TestMultiSegmentStringConversionToDateTime()
    {
        string jsonString = "\"1997-07-16\"";
        string expectedString = "1997-07-16";
        int expectedTokenLength = 10;
        var expectedDateTime = DateTime.Parse(expectedString);

        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8);

        for (int j = 0; j < utf8.Length; j++)
        {
            var utf8JsonReader = new Utf8JsonReader(sequence.Slice(0, j), isFinalBlock: false, default);
            ReadDateTimeHelper(ref utf8JsonReader, expectedDateTime, expectedTokenLength);

            Assert.AreEqual(0, utf8JsonReader.TokenStartIndex);

            long consumed = utf8JsonReader.BytesConsumed;
            utf8JsonReader = new Utf8JsonReader(sequence.Slice(consumed), isFinalBlock: true, utf8JsonReader.CurrentState);
            ReadDateTimeHelper(ref utf8JsonReader, expectedDateTime, expectedTokenLength);
        }
    }

    [TestMethod]
    public void TestMultiSegmentStringConversionToDateTimeOffset()
    {
        string jsonString = "\"1997-07-16\"";
        string expectedString = "1997-07-16";
        int expectedTokenLength = 10;
        var expectedDateTimeOffset = DateTimeOffset.Parse(expectedString);

        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8);

        for (int j = 0; j < utf8.Length; j++)
        {
            var utf8JsonReader = new Utf8JsonReader(sequence.Slice(0, j), isFinalBlock: false, default);
            ReadDateTimeOffsetHelper(ref utf8JsonReader, expectedDateTimeOffset, expectedTokenLength);

            Assert.AreEqual(0, utf8JsonReader.TokenStartIndex);

            long consumed = utf8JsonReader.BytesConsumed;
            utf8JsonReader = new Utf8JsonReader(sequence.Slice(consumed), isFinalBlock: true, utf8JsonReader.CurrentState);
            ReadDateTimeOffsetHelper(ref utf8JsonReader, expectedDateTimeOffset, expectedTokenLength);
        }
    }

    [TestMethod]
    [DynamicData(nameof(SmallTestCases))]
    public void TestPartialJsonReaderMultiSegment(bool compactData, TestCaseType type, string jsonString)
    {
        _ = type;

        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlyMemory<byte> dataMemory = dataUtf8;

        List<ReadOnlySequence<byte>> sequences = JsonTestHelper.GetSequences(dataMemory);

        for (int i = 0; i < sequences.Count; i++)
        {
            ReadOnlySequence<byte> sequence = sequences[i];
            var json = new Utf8JsonReader(sequence, isFinalBlock: true, default);
            while (json.Read())
                ;
            Assert.AreEqual(sequence.Length, json.BytesConsumed);

            Assert.IsTrue(sequence.Slice(json.Position).IsEmpty);
        }

        for (int i = 0; i < sequences.Count; i++)
        {
            ReadOnlySequence<byte> sequence = sequences[i];
            var json = new Utf8JsonReader(sequence);
            while (json.Read())
                ;
            Assert.AreEqual(sequence.Length, json.BytesConsumed);

            Assert.IsTrue(sequence.Slice(json.Position).IsEmpty);
        }
    }

    [TestMethod]
    [TestCategory("outerloop")]
    [DynamicData(nameof(SmallTestCases))]
    public void TestPartialJsonReaderSlicesMultiSegment(bool compactData, TestCaseType type, string jsonString)
    {
        _ = type;

        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlyMemory<byte> dataMemory = dataUtf8;

        List<ReadOnlySequence<byte>> sequences = JsonTestHelper.GetSequences(dataMemory);

        for (int i = 0; i < sequences.Count; i++)
        {
            ReadOnlySequence<byte> sequence = sequences[i];
            for (int j = 0; j < dataUtf8.Length; j++)
            {
                var json = new Utf8JsonReader(sequence.Slice(0, j), isFinalBlock: false, default);
                while (json.Read())
                    ;

                long consumed = json.BytesConsumed;
                JsonReaderState jsonState = json.CurrentState;
                byte[] consumedArray = sequence.Slice(0, consumed).ToArray();
                CollectionAssert.AreEqual(consumedArray, sequence.Slice(0, json.Position).ToArray());
                json = new Utf8JsonReader(sequence.Slice(consumed), isFinalBlock: true, jsonState);
                while (json.Read())
                    ;
                Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
            }
        }
    }

    [TestMethod]
    public void TestSingleStringsMultiSegment()
    {
        string jsonString = "\"Hello, \\u0041hson!\"";
        string expectedString = "Hello, \\u0041hson!, ";

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(dataUtf8);

        for (int j = 0; j < dataUtf8.Length; j++)
        {
            var utf8JsonReader = new Utf8JsonReader(sequence.Slice(0, j), isFinalBlock: false, default);
            byte[] resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8JsonReader);
            string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

            Assert.AreEqual(0, utf8JsonReader.TokenStartIndex);

            long consumed = utf8JsonReader.BytesConsumed;
            utf8JsonReader = new Utf8JsonReader(sequence.Slice(consumed), isFinalBlock: true, utf8JsonReader.CurrentState);
            resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out length, ref utf8JsonReader);
            actualStrSequence += Encoding.UTF8.GetString(resultSequence, 0, length);
            string message = $"Expected consumed: {dataUtf8.Length - consumed}, Actual consumed: {utf8JsonReader.BytesConsumed}, Index: {j}";
            Assert.IsTrue(dataUtf8.Length - consumed == utf8JsonReader.BytesConsumed, message);
            Assert.AreEqual(expectedString, actualStrSequence);

            Assert.AreEqual(0, utf8JsonReader.TokenStartIndex);
        }
    }

    [TestMethod]
    public void TestSingleStringsMultiSegmentByOne()
    {
        string jsonString = "\"Hello, \\u0041hson!\"";
        string expectedString = "Hello, \\u0041hson!, ";

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

        for (int j = 0; j < dataUtf8.Length; j++)
        {
            var utf8JsonReader = new Utf8JsonReader(sequence.Slice(0, j), isFinalBlock: false, default);
            byte[] resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8JsonReader);
            string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

            long consumed = utf8JsonReader.BytesConsumed;
            utf8JsonReader = new Utf8JsonReader(sequence.Slice(consumed), isFinalBlock: true, utf8JsonReader.CurrentState);
            resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out length, ref utf8JsonReader);
            actualStrSequence += Encoding.UTF8.GetString(resultSequence, 0, length);
            string message = $"Expected consumed: {dataUtf8.Length - consumed}, Actual consumed: {utf8JsonReader.BytesConsumed}, Index: {j}";
            Assert.IsTrue(dataUtf8.Length - consumed == utf8JsonReader.BytesConsumed, message);
            Assert.AreEqual(expectedString, actualStrSequence);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ComplexArrayJsonTokenStartIndex))]
    public void TestTokenStartIndexMultiSegment_ComplexArrayValue(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ComplexObjectSeveralJsonTokenStartIndex))]
    public void TestTokenStartIndexMultiSegment_ComplexObjectManyValues(string jsonString, int expectedIndexProperty, int expectedIndexValue)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexProperty, reader.TokenStartIndex);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexValue, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexProperty, reader.TokenStartIndex);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexValue, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ComplexObjectJsonTokenStartIndex))]
    public void TestTokenStartIndexMultiSegment_ComplexObjectValue(string jsonString, int expectedIndexProperty, int expectedIndexValue)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexProperty, reader.TokenStartIndex);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexValue, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexProperty, reader.TokenStartIndex);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexValue, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(SingleJsonTokenStartIndex))]
    public void TestTokenStartIndexMultiSegment_SingleValue(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var reader = new Utf8JsonReader(sequence, new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            reader = new Utf8JsonReader(sequence, new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(SingleJsonWithCommentsAllowTokenStartIndex))]
    public void TestTokenStartIndexMultiSegment_SingleValueCommentsAllow(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = false });
        var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

        state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
    }

    [TestMethod]
    [DynamicData(nameof(SingleJsonWithCommentsTokenStartIndex))]
    public void TestTokenStartIndexMultiSegment_SingleValueWithComments(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            if (commentHandling == JsonCommentHandling.Allow)
            {
                Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
                Assert.IsTrue(reader.Read());
            }
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            if (commentHandling == JsonCommentHandling.Allow)
            {
                Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
                Assert.IsTrue(reader.Read());
            }
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommas))]
    public void TestTokenStartIndexMultiSegment_WithTrailingCommas(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            while (reader.Read())
            { }

            Assert.AreEqual(utf8.Length - 1, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommasAndComments))]
    public void TestTokenStartIndexMultiSegment_WithTrailingCommasAndComments(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            var reader = new Utf8JsonReader(sequence, isFinalBlock: true, state);
            while (reader.Read())
            { }

            Assert.AreEqual(utf8.Length - 1, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(TrySkipValues))]
    public void TestTrySkipMultiSegment(string jsonString, JsonTokenType lastToken)
    {
        List<JsonTokenType> expectedTokenTypes = JsonTestHelper.GetTokenTypes(jsonString);
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(dataUtf8);
        TrySkipHelper(sequence, lastToken, expectedTokenTypes, JsonCommentHandling.Disallow);
        TrySkipHelper(sequence, lastToken, expectedTokenTypes, JsonCommentHandling.Skip);
        TrySkipHelper(sequence, lastToken, expectedTokenTypes, JsonCommentHandling.Allow);

        sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
        TrySkipHelper(sequence, lastToken, expectedTokenTypes, JsonCommentHandling.Disallow);
        TrySkipHelper(sequence, lastToken, expectedTokenTypes, JsonCommentHandling.Skip);
        TrySkipHelper(sequence, lastToken, expectedTokenTypes, JsonCommentHandling.Allow);
    }

    [TestMethod]
    [DynamicData(nameof(TrySkipValues))]
    public void TestTrySkipWithCommentsMultiSegment(string jsonString, JsonTokenType lastToken)
    {
        List<JsonTokenType> expectedTokenTypesWithoutComments = JsonTestHelper.GetTokenTypes(jsonString);

        jsonString = JsonTestHelper.InsertCommentsEverywhere(jsonString);

        List<JsonTokenType> expectedTokenTypes = JsonTestHelper.GetTokenTypes(jsonString);

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(dataUtf8);
        TrySkipHelper(sequence, JsonTokenType.Comment, expectedTokenTypes, JsonCommentHandling.Allow);
        TrySkipHelper(sequence, lastToken, expectedTokenTypesWithoutComments, JsonCommentHandling.Skip);

        sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
        TrySkipHelper(sequence, JsonTokenType.Comment, expectedTokenTypes, JsonCommentHandling.Allow);
        TrySkipHelper(sequence, lastToken, expectedTokenTypesWithoutComments, JsonCommentHandling.Skip);
    }

    [TestMethod]
    [DataRow("\"\\\"\"}", 4, 4, "\"", 2)]
    [DataRow("\"\\\"\"5", 4, 4, "\"", 2)]
    [DataRow("\"\\\"\":", 4, 4, "\"", 2)]
    [DataRow("\"\\\"\",", 4, 4, "\"", 2)]
    [DataRow("\"abc\\t\"}", 7, 7, "abc\t", 5)]
    [DataRow("\"abc\\u0061\"}", 11, 11, "abca", 9)]
    [DataRow("\"abc\\u0061abc\"}", 14, 14, "abcaabc", 12)]
    [DataRow("\"abc\\t\\\"\"}", 9, 9, "abc\t\"", 7)]
    [DataRow("\"abc\\n\"}", 7, 7, "abc\n", 5)]
    [DataRow("\"abc\\na\"}", 8, 8, "abc\na", 6)]
    [DataRow("\"abc\\u0061X\"}", 12, 12, "abcaX", 10)]
    [DataRow("\"\\u0061\"}", 8, 8, "a", 6)]
    public void ValidStringWithinInvalidJsonVariousSegmentSizes(string input, int expectedBytePositionInLine, int expectedConsumed, string expectedStr, int expectedTokenLength)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(input);

        var jsonReader = new Utf8JsonReader(utf8);
        ValidReadStringHelper(ref jsonReader, expectedBytePositionInLine, expectedConsumed, expectedStr, expectedTokenLength);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 1);
        jsonReader = new Utf8JsonReader(sequence);
        ValidReadStringHelper(ref jsonReader, expectedBytePositionInLine, expectedConsumed, expectedStr, expectedTokenLength);

        for (int splitLocation = 0; splitLocation < utf8.Length; splitLocation++)
        {
            sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            jsonReader = new Utf8JsonReader(sequence);
            ValidReadStringHelper(ref jsonReader, expectedBytePositionInLine, expectedConsumed, expectedStr, expectedTokenLength);
        }

        for (int firstSplit = 0; firstSplit < utf8.Length; firstSplit++)
        {
            for (int secondSplit = firstSplit; secondSplit < utf8.Length; secondSplit++)
            {
                sequence = JsonTestHelper.CreateSegments(utf8, firstSplit, secondSplit);
                jsonReader = new Utf8JsonReader(sequence);
                ValidReadStringHelper(ref jsonReader, expectedBytePositionInLine, expectedConsumed, expectedStr, expectedTokenLength);
            }
        }
    }

    //*****

    [TestMethod]
    public void ReadComplexNestedJsonMultiSegment()
    {
        string jsonString = """{"array":[1,2,{"nested":"value","number":42.5e10}],"bool":true,"null":null}""";
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            var jsonReader = new Utf8JsonReader(sequence);

            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.AreEqual("array", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(1, jsonReader.GetInt32());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(2, jsonReader.GetInt32());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("nested", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("value", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("number", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(42.5e10, jsonReader.GetDouble());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndArray, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("bool", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.IsTrue(jsonReader.GetBoolean());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("null", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.Null, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndObject, jsonReader.TokenType);
            Assert.IsFalse(jsonReader.Read());
        }
    }

    [TestMethod]
    public void ReadLongStringAcrossSegments()
    {
        string longString = new string('a', 1000);
        string jsonString = $$"""{"key":"{{longString}}"}""";
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        for (int segmentSize = 10; segmentSize < 100; segmentSize += 10)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, segmentSize);
            var jsonReader = new Utf8JsonReader(sequence);

            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.AreEqual("key", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.String, jsonReader.TokenType);
            Assert.AreEqual(longString, jsonReader.GetString());
            Assert.IsTrue(jsonReader.HasValueSequence);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndObject, jsonReader.TokenType);
            Assert.IsFalse(jsonReader.Read());
        }
    }

    [TestMethod]
    public void ReadEscapedStringAcrossSegmentBoundary()
    {
        string jsonString = """{"key":"value\twith\nescape\u0041sequences"}""";
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            var jsonReader = new Utf8JsonReader(sequence);

            Assert.IsTrue(jsonReader.Read());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("key", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("value\twith\nescape\u0041sequences", jsonReader.GetString());
            Assert.IsTrue(jsonReader.ValueIsEscaped);
        }
    }

    [TestMethod]
    public void ReadNumberWithExponentAcrossSegments()
    {
        string jsonString = """[1.23456789e+100,-9.87654321e-50,1e308]""";
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            var jsonReader = new Utf8JsonReader(sequence);

            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(1.23456789e+100, jsonReader.GetDouble());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(-9.87654321e-50, jsonReader.GetDouble());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(1e308, jsonReader.GetDouble());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndArray, jsonReader.TokenType);
            Assert.IsFalse(jsonReader.Read());
        }
    }

    [TestMethod]
    public void ReadLiteralsAcrossSegmentBoundaries()
    {
        string jsonString = """{"t":true,"f":false,"n":null}""";
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            var jsonReader = new Utf8JsonReader(sequence);

            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("t", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.IsTrue(jsonReader.GetBoolean());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("f", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.IsFalse(jsonReader.GetBoolean());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual("n", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.Null, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndObject, jsonReader.TokenType);
            Assert.IsFalse(jsonReader.Read());
        }
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(5)]
    [DataRow(10)]
    [DataRow(20)]
    public void ReadDeeplyNestedArraysMultiSegment(int depth)
    {
        string json = new string('[', depth) + "1" + new string(']', depth);
        byte[] utf8 = Encoding.UTF8.GetBytes(json);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(utf8, 3);
        var jsonReader = new Utf8JsonReader(sequence);

        for (int i = 0; i < depth; i++)
        {
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, jsonReader.TokenType);
            Assert.AreEqual(i, jsonReader.CurrentDepth);
        }

        Assert.IsTrue(jsonReader.Read());
        Assert.AreEqual(1, jsonReader.GetInt32());
        Assert.AreEqual(depth, jsonReader.CurrentDepth);

        for (int i = depth - 1; i >= 0; i--)
        {
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndArray, jsonReader.TokenType);
            Assert.AreEqual(i, jsonReader.CurrentDepth);
        }

        Assert.IsFalse(jsonReader.Read());
    }

    [TestMethod]
    public void ReadCommentsAcrossSegments()
    {
        string jsonString = """
        {
            // single line comment
            "key": /* multi
            line
            comment */ "value"
        }
        """;
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);
        var options = new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip };

        for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            var jsonReader = new Utf8JsonReader(sequence, options);

            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.AreEqual("key", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.String, jsonReader.TokenType);
            Assert.AreEqual("value", jsonReader.GetString());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndObject, jsonReader.TokenType);
            Assert.IsFalse(jsonReader.Read());
        }
    }

    [TestMethod]
    public void ReadPropertyNameAcrossSegmentWithEscaping()
    {
        string jsonString = """{"prop\u0065rty":123}""";
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        for (int splitLocation = 1; splitLocation < utf8.Length; splitLocation++)
        {
            ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(utf8, splitLocation);
            var jsonReader = new Utf8JsonReader(sequence);

            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, jsonReader.TokenType);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.AreEqual("property", jsonReader.GetString());
            Assert.IsTrue(jsonReader.ValueIsEscaped);
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(123, jsonReader.GetInt32());
            Assert.IsTrue(jsonReader.Read());
            Assert.AreEqual(JsonTokenType.EndObject, jsonReader.TokenType);
            Assert.IsFalse(jsonReader.Read());
        }
    }

    //*****

    private static void InvalidReadNumberHelper(ref Utf8JsonReader jsonReader, int expectedBytePositionInLine, bool firstReadSuccessful, int expectedConsumed, char invalidChar)
    {
        if (firstReadSuccessful)
        {
            Assert.IsTrue(jsonReader.Read());

            long tokenLength = jsonReader.HasValueSequence ? jsonReader.ValueSequence.Length : jsonReader.ValueSpan.Length;
            Assert.AreEqual(expectedConsumed, tokenLength);
            Assert.AreEqual(0, jsonReader.TokenStartIndex);
            Assert.AreEqual(expectedConsumed, jsonReader.BytesConsumed - jsonReader.TokenStartIndex);
        }

        try
        {
            jsonReader.Read();
            Assert.Fail("Expected JsonException was not thrown for incomplete/invalid JSON payload reading.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(0, ex.LineNumber);
            Assert.AreEqual(expectedBytePositionInLine, ex.BytePositionInLine);
            Assert.AreEqual(expectedConsumed, jsonReader.BytesConsumed);
            StringAssert.Contains(ex.Message, $"'{invalidChar}'");
        }
    }

    private static void InvalidReadStringHelper(ref Utf8JsonReader jsonReader, int expectedBytePositionInLine, int numberOfSuccessfulReads, int expectedConsumed)
    {
        for (int i = 0; i < numberOfSuccessfulReads; i++)
        {
            Assert.IsTrue(jsonReader.Read());
        }

        try
        {
            jsonReader.Read();
            Assert.Fail("Expected JsonException was not thrown for incomplete/invalid JSON payload reading.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(0, ex.LineNumber);
            Assert.AreEqual(expectedBytePositionInLine, ex.BytePositionInLine);
            Assert.AreEqual(expectedConsumed, jsonReader.BytesConsumed);
        }
    }

    private static void ReadDateTimeHelper(ref Utf8JsonReader jsonReader, DateTime expectedValue, long expectedTokenLength)
    {
        while (jsonReader.Read())
        {
            if (jsonReader.TokenType == JsonTokenType.String)
            {
                long tokenLength = jsonReader.HasValueSequence ? jsonReader.ValueSequence.Length : jsonReader.ValueSpan.Length;
                Assert.AreEqual(expectedTokenLength, tokenLength);
                Assert.AreEqual(expectedValue, jsonReader.GetDateTime());
            }
        }
    }

    private static void ReadDateTimeOffsetHelper(ref Utf8JsonReader jsonReader, DateTimeOffset expectedValue, long expectedTokenLength)
    {
        while (jsonReader.Read())
        {
            if (jsonReader.TokenType == JsonTokenType.String)
            {
                long tokenLength = jsonReader.HasValueSequence ? jsonReader.ValueSequence.Length : jsonReader.ValueSpan.Length;
                Assert.AreEqual(expectedTokenLength, tokenLength);
                Assert.AreEqual(expectedValue, jsonReader.GetDateTimeOffset());
            }
        }
    }

    private static void ReadDoubleHelper(ref Utf8JsonReader jsonReader, int expectedBytesConsumed, double expectedValue, long expectedTokenLength)
    {
        while (jsonReader.Read())
        {
            if (jsonReader.TokenType == JsonTokenType.Number)
            {
                long tokenLength = jsonReader.HasValueSequence ? jsonReader.ValueSequence.Length : jsonReader.ValueSpan.Length;
                Assert.AreEqual(expectedTokenLength, tokenLength);
                Assert.AreEqual(tokenLength, jsonReader.BytesConsumed - jsonReader.TokenStartIndex);
                Assert.AreEqual(expectedValue, jsonReader.GetDouble());
            }
        }
        Assert.AreEqual(expectedBytesConsumed, jsonReader.BytesConsumed);
    }

    private static void ReadFullySegmentSizeOne(bool compactData, TestCaseType type, string jsonString)
    {
        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        Stream stream = new MemoryStream(dataUtf8);
        TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        string expectedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

        var utf8JsonReader = new Utf8JsonReader(sequence, isFinalBlock: true, default);
        byte[] resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8JsonReader);
        string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);
        Assert.AreEqual(expectedStr, actualStrSequence);
    }

    private static void ReadPartialSegmentSizeOne(bool compactData, TestCaseType type, string jsonString)
    {
        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        Stream stream = new MemoryStream(dataUtf8);
        TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        string expectedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

        ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);

        for (int j = 0; j < dataUtf8.Length; j++)
        {
            var utf8JsonReader = new Utf8JsonReader(sequence.Slice(0, j), isFinalBlock: false, default);
            byte[] resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out int length, ref utf8JsonReader);
            string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

            long consumed = utf8JsonReader.BytesConsumed;
            utf8JsonReader = new Utf8JsonReader(sequence.Slice(consumed), isFinalBlock: true, utf8JsonReader.CurrentState);
            resultSequence = JsonTestHelper.ReaderLoop(dataUtf8.Length, out length, ref utf8JsonReader);
            actualStrSequence += Encoding.UTF8.GetString(resultSequence, 0, length);
            string message = $"Expected consumed: {dataUtf8.Length - consumed}, Actual consumed: {utf8JsonReader.BytesConsumed}, Index: {j}";
            Assert.IsTrue(dataUtf8.Length - consumed == utf8JsonReader.BytesConsumed, message);
            Assert.AreEqual(expectedStr, actualStrSequence);
        }
    }

    private static void ReadWithCommentsHelper(ref Utf8JsonReader reader, int expectedConsumed, bool validateThrows = false)
    {
        if (validateThrows)
        {
            try
            {
                while (reader.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown when reading JSON with trailing commas.");
            }
            catch (JsonException ex)
            {
                StringAssert.Contains(ex.Message, "trailing");
            }
        }
        else
        {
            while (reader.Read())
                ;
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }
    }

    private static void SpanSequenceStatesAreEqual(byte[] dataUtf8)
    {
        ReadOnlySequence<byte> sequence = JsonTestHelper.CreateSegments(dataUtf8);

        var jsonSpan = new Utf8JsonReader(dataUtf8, isFinalBlock: true, default);
        var jsonSequence = new Utf8JsonReader(sequence, isFinalBlock: true, default);

        while (true)
        {
            bool spanResult = jsonSpan.Read();
            bool sequenceResult = jsonSequence.Read();

            Assert.AreEqual(spanResult, sequenceResult);
            Assert.AreEqual(jsonSpan.CurrentDepth, jsonSequence.CurrentDepth);
            Assert.AreEqual(jsonSpan.BytesConsumed, jsonSequence.BytesConsumed);
            Assert.AreEqual(jsonSpan.TokenType, jsonSequence.TokenType);

            if (!spanResult)
            {
                break;
            }
        }
    }

    private static void SpanSequenceStatesAreEqualInvalidJson(byte[] dataUtf8, ReadOnlySequence<byte> sequence, int maxDepth, JsonCommentHandling commentHandling)
    {
        var stateSpan = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
        var jsonSpan = new Utf8JsonReader(dataUtf8, isFinalBlock: true, stateSpan);

        var stateSequence = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
        var jsonSequence = new Utf8JsonReader(sequence, isFinalBlock: true, stateSequence);

        try
        {
            while (true)
            {
                bool spanResult = jsonSpan.Read();
                bool sequenceResult = jsonSequence.Read();

                Assert.AreEqual(spanResult, sequenceResult);
                Assert.AreEqual(jsonSpan.CurrentDepth, jsonSequence.CurrentDepth);
                Assert.AreEqual(jsonSpan.BytesConsumed, jsonSequence.BytesConsumed);
                Assert.AreEqual(jsonSpan.TokenType, jsonSequence.TokenType);

                if (!spanResult)
                {
                    break;
                }
            }
            Assert.Fail("Expected JsonException due to invalid JSON.");
        }
        catch (JsonException)
        { }
    }

    private static void TestReadingJsonWithComments(byte[] inputData, ReadOnlySequence<byte> sequence, string expectedWithoutComments, string expectedWithComments)
    {
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

        var builder = new StringBuilder();
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number || json.TokenType == JsonTokenType.Comment || json.TokenType == JsonTokenType.PropertyName)
            {
                builder.Append(Encoding.UTF8.GetString(json.HasValueSequence ? json.ValueSequence.ToArray() : json.ValueSpan.ToArray()));
                if (json.HasValueSequence)
                {
                    Assert.IsTrue(json.ValueSpan == default);
                }
                else
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                }
            }
        }

        Assert.AreEqual(expectedWithComments, builder.ToString());
        CollectionAssert.AreEqual(inputData, sequence.Slice(0, json.Position).ToArray());

        state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        json = new Utf8JsonReader(sequence, isFinalBlock: true, state);

        builder = new StringBuilder();
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number || json.TokenType == JsonTokenType.Comment || json.TokenType == JsonTokenType.PropertyName)
            {
                builder.Append(Encoding.UTF8.GetString(json.HasValueSequence ? json.ValueSequence.ToArray() : json.ValueSpan.ToArray()));
                if (json.HasValueSequence)
                {
                    Assert.IsTrue(json.ValueSpan == default);
                }
                else
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                }
            }
        }

        Assert.AreEqual(expectedWithoutComments, builder.ToString());
        CollectionAssert.AreEqual(inputData, sequence.Slice(0, json.Position).ToArray());
    }

    private static void TestReadingSingleValueJson(byte[] inputData, ReadOnlySequence<byte> sequence, string expectedString)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(sequence, false, state);

            while (json.Read())
            {
                // Check if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
                Assert.IsTrue(json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null);
                Assert.AreEqual(expectedString, Encoding.UTF8.GetString(json.HasValueSequence ? json.ValueSequence.ToArray() : json.ValueSpan.ToArray()));

                if (json.HasValueSequence)
                {
                    Assert.IsTrue(json.ValueSpan == default);
                }
                else
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                }
                Assert.AreEqual(2, json.TokenStartIndex);
            }

            CollectionAssert.AreEqual(inputData, sequence.Slice(0, json.Position).ToArray());
        }
    }

    private static void TestReadTokenWithExtra(ReadOnlySequence<byte> sequence, JsonCommentHandling commentHandling, bool isFinalBlock, bool commentsAppended = false)
    {
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
        var reader = new Utf8JsonReader(sequence, isFinalBlock, state);

        if (commentsAppended && commentHandling == JsonCommentHandling.Allow)
        {
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
        }

        Assert.IsTrue(reader.Read());
        if (reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.StartObject)
        {
            Assert.IsTrue(reader.Read());
            CollectionAssert.Contains(new[] { JsonTokenType.EndArray, JsonTokenType.EndObject }, reader.TokenType);
        }

        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref jsonReader) =>
        {
            jsonReader.Read();
            if (commentHandling == JsonCommentHandling.Allow && jsonReader.TokenType == JsonTokenType.Comment)
            {
                jsonReader.Read();
            }
        });
    }

    private static void TrailingCommasHelper(ReadOnlySequence<byte> utf8, JsonReaderState state, bool allow, bool expectThrow)
    {
        var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);

        Assert.AreEqual(allow, state.Options.AllowTrailingCommas);
        Assert.AreEqual(allow, reader.CurrentState.Options.AllowTrailingCommas);

        if (expectThrow)
        {
            JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref jsonReader) =>
            {
                while (jsonReader.Read())
                    ;
            });
        }
        else
        {
            while (reader.Read())
                ;
        }
    }

    private static void TrySkipHelper(ReadOnlySequence<byte> dataUtf8, JsonTokenType lastToken, List<JsonTokenType> expectedTokenTypes, JsonCommentHandling commentHandling)
    {
        var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        JsonReaderState previous = json.CurrentState;
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(0, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.IsTrue(json.TrySkip());

        JsonReaderState current = json.CurrentState;
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(previous, current);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(0, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        int totalReads = 0;
        while (json.Read())
        {
            totalReads++;
        }

        Assert.AreEqual(expectedTokenTypes.Count, totalReads);

        previous = json.CurrentState;
        Assert.AreEqual(lastToken, json.TokenType);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.IsTrue(json.TrySkip());

        current = json.CurrentState;
        Assert.AreEqual(previous, current);
        Assert.AreEqual(lastToken, json.TokenType);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        for (int i = 0; i < totalReads; i++)
        {
            state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling });
            json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
            for (int j = 0; j < i; j++)
            {
                Assert.IsTrue(json.Read());
            }
            Assert.IsTrue(json.TrySkip());
            Assert.IsTrue(expectedTokenTypes[i] == json.TokenType, $"Expected: {expectedTokenTypes[i]}, Actual: {json.TokenType}, , Index: {i}, BytesConsumed: {json.BytesConsumed}");
        }
    }

    private static void ValidateThrows(ref Utf8JsonReader reader)
    {
        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref jsonReader) =>
        {
            while (jsonReader.Read())
                ;
        });
    }

    private static void ValidReadStringHelper(ref Utf8JsonReader jsonReader, int expectedBytePositionInLine, int expectedConsumed, string expectedStr, int expectedTokenLength)
    {
        Assert.IsTrue(jsonReader.Read());

        Assert.AreEqual(JsonTokenType.String, jsonReader.TokenType);
        long tokenLength = jsonReader.HasValueSequence ? jsonReader.ValueSequence.Length : jsonReader.ValueSpan.Length;
        Assert.AreEqual(expectedTokenLength, tokenLength);
        Assert.AreEqual(expectedTokenLength + 2, jsonReader.BytesConsumed - jsonReader.TokenStartIndex);
        Assert.AreEqual(expectedStr, jsonReader.GetString());

        try
        {
            jsonReader.Read();
            Assert.Fail("Expected JsonException was not thrown for incomplete/invalid JSON payload reading.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(0, ex.LineNumber);
            Assert.AreEqual(expectedBytePositionInLine, ex.BytePositionInLine);
            Assert.AreEqual(expectedConsumed, jsonReader.BytesConsumed);
        }
    }
}