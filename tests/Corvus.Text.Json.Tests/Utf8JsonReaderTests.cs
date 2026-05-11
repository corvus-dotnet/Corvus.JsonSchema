// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Corvus.Text.Json.Internal;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class Utf8JsonReaderTests
{
    public static IEnumerable<object[]> CommentTestLineSeparators
    {
        get
        {
            return new List<object[]>
            {
                new object [] {"\r" },
                new object [] {"\r\n" },
                new object [] {"\n" },
            };
        }
    }

    public static IEnumerable<object[]> ComplexArrayJsonTokenStartIndex
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"[1,2]", 3},
                new object[] {"[1,  2]", 5},
                new object[] {"[1  ,2]", 5},
                new object[] {"[1  ,  2]", 7},
                new object[] {"[1  ,  2  ]", 7},

                new object[] {"[1,\"string\"]", 3},
                new object[] {"[1,  \"string\"]", 5},
                new object[] {"[1  ,\"string\"]", 5},
                new object[] {"[1  ,  \"string\"]", 7},
                new object[] {"[1  ,  \"string\"  ]", 7},

                new object[] {"[{}]", 2},
                new object[] {"[[]]", 2},
                new object[] {"[123,{}]", 5},
                new object[] {"[123,[]]", 5},
                new object[] {"[  {}]", 4},
                new object[] {"[  []]", 4},
                new object[] {"[123,  {}]", 7},
                new object[] {"[123,  []]", 7},
            };
        }
    }

    public static IEnumerable<object[]> ComplexObjectJsonTokenStartIndex
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"{\"propertyName\":\"value\"}", 1, 16},
                new object[] {"{  \"propertyName\":\"value\"}", 3, 18},
                new object[] {"{\"propertyName\"  :\"value\"}", 1, 18},
                new object[] {"{\"propertyName\":  \"value\"}", 1, 18},
                new object[] {"{\"propertyName\":\"value\"  }", 1, 16},
                new object[] {"  {\"propertyName\":\"value\"}", 3, 18},
                new object[] {"{\"propertyName\":\"value\"}  ", 1, 16},

                new object[] {"{  \"propertyName\"  :\"value\"}", 3, 20},
                new object[] {"{  \"propertyName\":  \"value\"}", 3, 20},
                new object[] {"{  \"propertyName\":\"value\"  }", 3, 18},
                new object[] {"  {  \"propertyName\":\"value\"}", 5, 20},
                new object[] {"{  \"propertyName\":\"value\"}   ", 3, 18},

                new object[] {"{\"propertyName\"  :  \"value\"}", 1, 20},
                new object[] {"{\"propertyName\":  \"value\"  }", 1, 18},
                new object[] {"  {\"propertyName\":  \"value\"}", 3, 20},
                new object[] {"{\"propertyName\":  \"value\"}  ", 1, 18},

                new object[] {"{\"propertyName\"  :\"value\"  }", 1, 18},
                new object[] {"  {\"propertyName\"  :\"value\"}", 3, 20},
                new object[] {"{\"propertyName\"  :\"value\"}  ", 1, 18},

                new object[] {"  {\"propertyName\":\"value\"  }", 3, 18},
                new object[] {"{\"propertyName\":\"value\"  }  ", 1, 16},

                new object[] {"{\"propertyName\":123}", 1, 16},
                new object[] {"{  \"propertyName\":123}", 3, 18},
                new object[] {"{\"propertyName\"  :123}", 1, 18},
                new object[] {"{\"propertyName\":  123}", 1, 18},
                new object[] {"{\"propertyName\":123  }", 1, 16},
                new object[] {"  {\"propertyName\":123}", 3, 18},
                new object[] {"{\"propertyName\":123}   ", 1, 16},

                new object[] {"{  \"propertyName\"  :123}", 3, 20},
                new object[] {"{  \"propertyName\":  123}", 3, 20},
                new object[] {"{  \"propertyName\":123  }", 3, 18},
                new object[] {"  {  \"propertyName\":123}", 5, 20},
                new object[] {"{  \"propertyName\":123}  ", 3, 18},

                new object[] {"{\"propertyName\"  :  123}", 1, 20},
                new object[] {"{\"propertyName\":  123  }", 1, 18},
                new object[] {"  {\"propertyName\":  123}", 3, 20},
                new object[] {"{\"propertyName\":  123}  ", 1, 18},

                new object[] {"{\"propertyName\"  :123  }", 1, 18},
                new object[] {"  {\"propertyName\"  :123}", 3, 20},
                new object[] {"{\"propertyName\"  :123}  ", 1, 18},

                new object[] {"  {\"propertyName\":123  }", 3, 18},
                new object[] {"{\"propertyName\":123  }  ", 1, 16},

                new object[] {"{\"propertyName\":[]}", 1, 16},
                new object[] {"{  \"propertyName\":[]}", 3, 18},
                new object[] {"{\"propertyName\"  :[]}", 1, 18},
                new object[] {"{\"propertyName\":  []}", 1, 18},
                new object[] {"{\"propertyName\":[]  }", 1, 16},
                new object[] {"  {\"propertyName\":[]}", 3, 18},
                new object[] {"{\"propertyName\":[]}  ", 1, 16},

                new object[] {"{\"propertyName\":{}}", 1, 16},
                new object[] {"{  \"propertyName\":{}}", 3, 18},
                new object[] {"{\"propertyName\"  :{}}", 1, 18},
                new object[] {"{\"propertyName\":  {}}", 1, 18},
                new object[] {"{\"propertyName\":{}  }", 1, 16},
                new object[] {"  {\"propertyName\":{}}", 3, 18},
                new object[] {"{\"propertyName\":{}}  ", 1, 16},
            };
        }
    }

    public static IEnumerable<object[]> ComplexObjectSeveralJsonTokenStartIndex
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"{\"\":\"\", \"propertyName\":[]}", 8, 23},
                new object[] {"{\"\":\"\",   \"propertyName\":[]}", 10, 25},
                new object[] {"{\"\":\"\", \"propertyName\"  :[]}", 8, 25},
                new object[] {"{\"\":\"\", \"propertyName\":  []}", 8, 25},
                new object[] {"{\"\":\"\", \"propertyName\":[]  }", 8, 23},
                new object[] {"  {\"\":\"\", \"propertyName\":[]}", 10, 25},
                new object[] {"{\"\":\"\", \"propertyName\":[]}  ", 8, 23},

                new object[] {"{\"\":\"\", \"propertyName\":{}}", 8, 23},
                new object[] {"{\"\":\"\",   \"propertyName\":{}}", 10, 25},
                new object[] {"{\"\":\"\", \"propertyName\"  :{}}", 8, 25},
                new object[] {"{\"\":\"\", \"propertyName\":  {}}", 8, 25},
                new object[] {"{\"\":\"\", \"propertyName\":{}  }", 8, 23},
                new object[] {"{  \"\":\"\", \"propertyName\":{}}", 10, 25},
                new object[] {"{\"\":\"\", \"propertyName\":{}}  ", 8, 23},
            };
        }
    }

    public static IEnumerable<object[]> GetCommentTestData
    {
        get
        {
            var dataList = new List<object[]>();
            foreach (string delim in new[] { "\r", "\r\n", "\n" })
            {
                // NOTE: Leading and trailing spaces in the comments are significant.
                string singleLineComment = " Single Line Comment ";
                dataList.Add(new object[] { $"{{//{singleLineComment}{delim}}}", singleLineComment });
                string multilineComment = $" Multiline {delim} Comment ";
                dataList.Add(new object[] { $"{{/*{multilineComment}*/{delim}}}", multilineComment });
            }
            return dataList;
        }
    }

    public static IEnumerable<object[]> GetCommentUnescapeData
    {
        get
        {
            var dataList = new List<object[]>();

            string[] rawComments = new string[]
            {
                "A string with {0}valid UTF8 \\t tab",
                "A string with {0}invalid UTF8 \\xc3\\x28",
                "A string with {0}valid UTF16 \\u002e \\u0009 \u092E",
                "A string with {0}invalid UTF16 \\uDD1E"
            };

            // single line comments
            foreach (string raw in rawComments)
            {
                string str = string.Format(raw, "");
                string cmt = "//" + str;
                dataList.Add(new object[] { cmt, str });
            }

            // multiline comments
            foreach (string raw in rawComments)
            {
                string str = string.Format(raw, "\n");
                string cmt = "/*" + str + "*/";
                dataList.Add(new object[] { cmt, str });
            }

            return dataList;
        }
    }

    public static IEnumerable<object[]> InvalidJsonStrings
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"\"", 0, 1},
                new object[] {"{]", 0, 1},
                new object[] {"[}", 0, 1},
                new object[] {"nul", 0, 3},
                new object[] {"tru", 0, 3},
                new object[] {"fals", 0, 4},
                new object[] {"falseinvalid", 0, 5},
                new object[] {"\"a\u6F22\u5B57ge\":", 0, 11},
                new object[] {"{\"a\u6F22\u5B57ge\":", 0, 13},
                new object[] {"{\"name\":\"A\u6F22\u5B57hso", 0, 19},
                new object[] {"{\"name\":\"A\u6F22\u5B57h\\nso", 0, 21},
                new object[] {"12345.1.", 0, 7},
                new object[] {"-", 0, 1},
                new object[] {"-f", 0, 1},
                new object[] {"1.f", 0, 2},
                new object[] {"0.", 0, 2},
                new object[] {"0.1f", 0, 3},
                new object[] {"0.1e1f", 0, 5},
                new object[] {"123,", 0, 3},
                new object[] {"false,", 0, 5},
                new object[] {"true,", 0, 4},
                new object[] {"null,", 0, 4},
                new object[] {"trUe,", 0, 2},
                new object[] {"\"h\u6F22\u5B57ello\",", 0, 13},
                new object[] {"\"\\u12z3\"", 0, 5},
                new object[] {"\"\\u12]3\"", 0, 5},
                new object[] {"\"\\u12=3\"", 0, 5},
                new object[] {"\"\\u12$3\"", 0, 5},
                new object[] {"\"\\u12\"", 0, 5},
                new object[] {"\"\\u120\"", 0, 6},
                new object[] {"+0", 0, 0},
                new object[] {"+1", 0, 0},
                new object[] {"0e", 0, 2},
                new object[] {"0.1e", 0, 4},
                new object[] {"01", 0, 1},
                new object[] {"1a", 0, 1},
                new object[] {"-01", 0, 2},
                new object[] {"10.5e", 0, 5},
                new object[] {"10.5e-", 0, 6},
                new object[] {"10.5e+", 0, 6},
                new object[] {"10.5e-0.2", 0, 7},
                new object[] {"{\"age\":30, \"ints\":[1, 2, 3, 4, 5.1e7.3]}", 0, 36},
                new object[] {"{\"age\":30, \r\n \"num\":-0.e, \r\n \"ints\":[1, 2, 3, 4, 5]}", 1, 10},
                new object[] {"{ \"number\": 00", 0, 13},
                new object[] {"{{}}", 0, 1},
                new object[] {"[[]", 0, 3},
                new object[] {"[[{{}}]]", 0, 3},
                new object[] {"[1, 2, 3, ]", 0, 10},
                new object[] {"{\"ints\":[1, 2, 3, 4, 5", 0, 22},
                new object[] {"{\"s\u6F22\u5B57trings\":[\"a\u6F22\u5B57bc\", \"def\"", 0, 36},
                new object[] {"{\"age\":30, \"ints\":[1, 2, 3, 4, 5}}", 0, 32},
                new object[] {"{\"age\":30, \"name\":\"test}", 0, 24},
                new object[] {"{\r\n\"isActive\": false \"\r\n}", 1, 18},
                new object[] {"[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2\":[}]]]]}]]]]", 1, 28},
                new object[] {"[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2:[]}]]]]}]]]]", 1, 38},
                new object[] {"[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2\":[]},[}]]]]}]]]]", 1, 32},
                new object[] {"{\r\n\t\"isActive\": false,\r\n\t\"array\": [\r\n\t\t[{\r\n\t\t\t\"id\": 1\r\n\t\t}]\r\n\t]\r\n}", 3, 3, 3},
                new object[] {"{\"Here is a \u6F22\u5B57string: \\\"\\\"\":\"Here is \u6F22\u5B57a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str:\"\\\"\\\"\"}", 0, 190},
                new object[] {"\"hel\rlo\"", 0, 4},
                new object[] {"\"hel\nlo\"", 0, 4},
                new object[] {"\"hel\\uABCXlo\"", 0, 9},
                new object[] {"\"hel\\\tlo\"", 0, 5},
                new object[] {"\"hel\rlo\\\"\"", 0, 4},
                new object[] {"\"hel\nlo\\\"\"", 0, 4},
                new object[] {"\"hel\\uABCXlo\\\"\"", 0, 9},
                new object[] {"\"hel\\\tlo\\\"\"", 0, 5},
                new object[] {"\"he\\nl\rlo\\\"\"", 0, 6},
                new object[] {"\"he\\nl\nlo\\\"\"", 0, 6},
                new object[] {"\"he\\nl\\uABCXlo\\\"\"", 0, 11},
                new object[] {"\"he\\nl\\\tlo\\\"\"", 0, 7},
                new object[] {"\"he\\nl\rlo", 0, 6},
                new object[] {"\"he\\nl\nlo", 0, 6},
                new object[] {"\"he\\nl\\uABCXlo", 0, 11},
                new object[] {"\"he\\nl\\\tlo", 0, 7},
            };
        }
    }

    public static IEnumerable<object[]> InvalidUTF8Strings
    {
        get
        {
            return new List<object[]>
            {
                new object[] { new byte[] { 34, 97, 0xc3, 0x28, 98, 34 } },
                new object[] { new byte[] { 34, 97, 0xa0, 0xa1, 98, 34 } },
                new object[] { new byte[] { 34, 97, 0xe2, 0x28, 0xa1, 98, 34 } },
                new object[] { new byte[] { 34, 97, 0xe2, 0x82, 0x28, 98, 34 } },
                new object[] { new byte[] { 34, 97, 0xf0, 0x28, 0x8c, 0xbc, 98, 34 } },
                new object[] { new byte[] { 34, 97, 0xf0, 0x90, 0x28, 0xbc, 98, 34 } },
                new object[] { new byte[] { 34, 97, 0xf0, 0x28, 0x8c, 0x28, 98, 34 } },
            };
        }
    }

    public static IEnumerable<object[]> JsonTokenWithExtraValue
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"  true  5 "},
                new object[] {"  false  5 "},
                new object[] {"  null  5 "},
                new object[] {"  5  5 "},
                new object[] {"  5.1234e-4  5 "},
                new object[] {"  \"hello\"  5 "},
                new object[] {"  \"hello\"  \"hello\" "},
                new object[] {"  [  ]  5 "},
                new object[] {"  [  ]  [] "},
                new object[] {"  [  ]  {} "},
                new object[] {"  { }  5 "},
                new object[] {"  { }  [] "},
                new object[] {"  { }  {} "},
                new object[] {"  [  ]5 "},
                new object[] {"  [  ][] "},
                new object[] {"  [  ]{} "},
                new object[] {"  { }5 "},
                new object[] {"  { }[] "},
                new object[] {"  { }{} "},
                new object[] {"  { }  5.1234e-4"},
                new object[] {"  { }  null "},
                new object[] {"  { }  false "},
                new object[] {"  { }  true "},
                new object[] {"  { }  \"hello\" " },
                new object[] {"  { },  5 "},
                new object[] {"  { },  [] "},
                new object[] {"  { },  {} "},
                new object[] {"  { },  5.1234e-4"},
                new object[] {"  { },  null "},
                new object[] {"  { },  false "},
                new object[] {"  { },  true "},
                new object[] {"  { },  \"hello\" " },
            };
        }
    }

    public static IEnumerable<object[]> JsonTokenWithExtraValueAndComments
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"  true  /* comment */ 5 "},
                new object[] {"  false  /* comment */ 5 "},
                new object[] {"  null  /* comment */ 5 "},
                new object[] {"  5  /* comment */ 5 "},
                new object[] {"  5.1234e-4  /* comment */ 5 "},
                new object[] {"  \"hello\"  /* comment */ 5 "},
                new object[] {"  \"hello\"  /* comment */ \"hello\" "},
                new object[] {"  \"hello\"  // comment \n \"hello\" "},
                new object[] {"  [  ]  /* comment */ 5 "},
                new object[] {"  [  ]  /* comment */ [ ]"},
                new object[] {"  [  ]  /* comment */ { }"},
                new object[] {"  [  ]  // comment \n 5 "},
                new object[] {"  { }  /* comment */ 5 "},
                new object[] {"  { }  /* comment */ [] "},
                new object[] {"  { }  /* comment */ {} "},
                new object[] {"  [  ]/* comment */5 "},
                new object[] {"  [  ]/* comment */[ ]"},
                new object[] {"  [  ]/* comment */{ }"},
                new object[] {"  [  ]// comment \n5 "},
                new object[] {"  { }/* comment */5 "},
                new object[] {"  { }/* comment */[] "},
                new object[] {"  { }/* comment */{} "},
                new object[] {"  { }  /* comment */ 5.1234e-4"},
                new object[] {"  { }  /* comment */ null "},
                new object[] {"  { }  /* comment */ false "},
                new object[] {"  { }  /* comment */ true "},
                new object[] {"  { }  /* comment */ \"hello\" "},
                new object[] {"  { }  // comment \n \"hello\" "},
                new object[] {"  { },  /* comment */ 5 "},
                new object[] {"  { },  /* comment */ [] "},
                new object[] {"  { },  /* comment */ {} "},
                new object[] {"  { },  /* comment */ 5.1234e-4"},
                new object[] {"  { },  /* comment */ null "},
                new object[] {"  { },  /* comment */ false "},
                new object[] {"  { },  /* comment */ true "},
                new object[] {"  { },  /* comment */ \"hello\" "},
                new object[] {"  { },  // comment \n \"hello\" "},
            };
        }
    }

    public static IEnumerable<object[]> JsonWithInvalidTrailingCommas
    {
        get
        {
            return new List<object[]>
            {
                new object[] {","},
                new object[] {"   ,   "},
                new object[] {"{},"},
                new object[] {"[],"},
                new object[] {"1,"},
                new object[] {"true,"},
                new object[] {"false,"},
                new object[] {"null,"},
                new object[] {"{,}"},
                new object[] {"{\"name\": 1,,}"},
                new object[] {"{\"name\": 1,,\"last\":2,}"},
                new object[] {"[,]"},
                new object[] {"[1,,]"},
                new object[] {"[1,,2,]"},
            };
        }
    }

    public static IEnumerable<object[]> JsonWithInvalidTrailingCommasAndComments
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"/*comment*/ ,/*comment*/"},
                new object[] {"   /*comment*/ ,  /*comment*/ "},
                new object[] {"{}/*comment*/,/*comment*/"},
                new object[] {"[]/*comment*/,/*comment*/"},
                new object[] {"1/*comment*/,/*comment*/"},
                new object[] {"true/*comment*/,/*comment*/"},
                new object[] {"false/*comment*/,/*comment*/"},
                new object[] {"null/*comment*/,/*comment*/"},
                new object[] {"{/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": 1/*comment*/,/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": 1,/*comment*/,\"last\":2,}"},
                new object[] {"[/*comment*/,/*comment*/]"},
                new object[] {"[1/*comment*/,/*comment*/,/*comment*/]"},
                new object[] {"[1,/*comment*/,2,]"},
            };
        }
    }

    public static IEnumerable<object[]> JsonWithValidTrailingCommas
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"{\"name\": \"value\",}"},
                new object[] {"{\"name\": [],}"},
                new object[] {"{\"name\": 1,}"},
                new object[] {"{\"name\": true,}"},
                new object[] {"{\"name\": false,}"},
                new object[] {"{\"name\": null,}"},
                new object[] {"{\"name\": [{},],}"},
                new object[] {"{\"first\" : \"value\", \"name\": [{},], \"last\":2 ,}"},
                new object[] {"{\"prop\":{\"name\": 1,\"last\":2,},}"},
                new object[] {"{\"prop\":[1,2,],}"},
                new object[] {"[\"value\",]"},
                new object[] {"[1,]"},
                new object[] {"[true,]"},
                new object[] {"[false,]"},
                new object[] {"[null,]"},
                new object[] {"[{},]"},
                new object[] {"[{\"name\": [],},]"},
                new object[] {"[1, {\"name\": [],},2 , ]"},
                new object[] {"[[1,2,],]"},
                new object[] {"[{\"name\": 1,\"last\":2,},]"},
            };
        }
    }

    public static IEnumerable<object[]> JsonWithValidTrailingCommasAndComments
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"{\"name\": \"value\"/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": []/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": 1/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": true/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": false/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": null/*comment*/,/*comment*/}"},
                new object[] {"{\"name\": [{},]/*comment*/,/*comment*/}"},
                new object[] {"{\"first\" : \"value\", \"name\": [{},], \"last\":2 /*comment*/,/*comment*/}"},
                new object[] {"{\"prop\":{\"name\": 1,\"last\":2,}/*comment*/,}"},
                new object[] {"{\"prop\":[1,2,]/*comment*/,}"},
                new object[] {"{\"prop\":1,/*comment*/}"},
                new object[] {"[\"value\"/*comment*/,/*comment*/]"},
                new object[] {"[1/*comment*/,/*comment*/]"},
                new object[] {"[true/*comment*/,/*comment*/]"},
                new object[] {"[false/*comment*/,/*comment*/]"},
                new object[] {"[null/*comment*/,/*comment*/]"},
                new object[] {"[{}/*comment*/,/*comment*/]"},
                new object[] {"[{\"name\": [],}/*comment*/,/*comment*/]"},
                new object[] {"[1, {\"name\": [],},2 /*comment*/,/*comment*/ ]"},
                new object[] {"[[1,2,]/*comment*/,]"},
                new object[] {"[{\"name\": 1,\"last\":2,}/*comment*/,]"},
                new object[] {"[1,/*comment*/]"},
            };
        }
    }

    public static IEnumerable<object[]> LargeTestCases
    {
        get
        {
            return new List<object[]>
            {
                new object[] { true, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Json.NET and Corvus.Text.Json
                new object[] { true, TestCaseType.DeepTree, SR.DeepTree},
                new object[] { true, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                new object[] { true, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                new object[] { true, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
                new object[] { true, TestCaseType.Json400B, SR.Json400B},
                new object[] { true, TestCaseType.Json40KB, SR.Json40KB},
                new object[] { true, TestCaseType.Json400KB, SR.Json400KB},

                new object[] { false, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Json.NET and Corvus.Text.Json
                new object[] { false, TestCaseType.DeepTree, SR.DeepTree},
                new object[] { false, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                new object[] { false, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                new object[] { false, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
                new object[] { false, TestCaseType.Json400B, SR.Json400B},
                new object[] { false, TestCaseType.Json40KB, SR.Json40KB},
                new object[] { false, TestCaseType.Json400KB, SR.Json400KB}
            };
        }
    }

    public static IEnumerable<object[]> LotsOfCommentsTests
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"   12345   ", true, "12345"},
                new object[] {"   12345.67890e-12   ", true, "1.23456789E-08"},
                new object[] {"   true  ", true, "True"},
                new object[] {"   false   ", true, "False"},
                new object[] {"   null   ", true, "null"},
                new object[] {"   \" Test string with \\\"nested quotes \\\" and hex: \\uABCD values! \"   ", true, " Test string with \"nested quotes \" and hex: \uABCD values! "},

                new object[] {"   12345   ", false, "12345"},
                new object[] {"   12345.67890e-12   ", false, "1.23456789E-08"},
                new object[] {"   true  ", false, "True"},
                new object[] {"   false   ", false, "False"},
                new object[] {"   null   ", false, "null"},
                new object[] {"   \" Test string with \\\"nested quotes \\\" and hex: \\uABCD values! \"   ", false, " Test string with \"nested quotes \" and hex: \uABCD values! "},
            };
        }
    }

    public static IEnumerable<object[]> SingleJsonTokenStartIndex
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"[]", 0},
                new object[] {"{}", 0},
                new object[] {"12345", 0},
                new object[] {"1", 0},
                new object[] {"-0", 0},
                new object[] {"0.0e-0", 0},
                new object[] {"0.0e+0", 0},
                new object[] {"true", 0},
                new object[] {"false", 0},
                new object[] {"null", 0},
                new object[] {"\"hello\"", 0},
                new object[] {"\"\"", 0},

                new object[] {"  []", 2},
                new object[] {"  {}", 2},
                new object[] {"  12345", 2},
                new object[] {"  1", 2},
                new object[] {"  -0", 2},
                new object[] {"  0.0e-0", 2},
                new object[] {"  0.0e+0", 2},
                new object[] {"  true", 2},
                new object[] {"  false", 2},
                new object[] {"  null", 2},
                new object[] {"  \"hello\"", 2},
                new object[] {"  \"\"", 2},

                new object[] {"  []  ", 2},
                new object[] {"  {}  ", 2},
                new object[] {"  12345  ", 2},
                new object[] {"  1  ", 2},
                new object[] {"  -0  ", 2},
                new object[] {"  0.0e-0  ", 2},
                new object[] {"  0.0e+0  ", 2},
                new object[] {"  true  ", 2},
                new object[] {"  false  ", 2},
                new object[] {"  null  ", 2},
                new object[] {"  \"hello\"  ", 2},
                new object[] {"  \"\"  ", 2},
            };
        }
    }

    public static IEnumerable<object[]> SingleJsonWithCommentsAllowTokenStartIndex
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"/*comment*/", 0},
                new object[] {"//comment\n", 0},
                new object[] {"/*comment*//*comment*/", 0},
                new object[] {"/*comment*///comment\n", 0},
                new object[] {"//comment\n/*comment*/", 0},
                new object[] {"//comment\n//comment\n", 0},

                new object[] {"  /*comment*/", 2},
                new object[] {"  //comment\n", 2},
                new object[] {"  /*comment*//*comment*/", 2},
                new object[] {"  /*comment*///comment\n", 2},
                new object[] {"  //comment\n/*comment*/", 2},
                new object[] {"  //comment\n//comment\n", 2},

                new object[] {"  /*comment*/  ", 2},
                new object[] {"  //comment\n  ", 2},
                new object[] {"  /*comment*//*comment*/  ", 2},
                new object[] {"  /*comment*///comment\n  ", 2},
                new object[] {"  //comment\n/*comment*/  ", 2},
                new object[] {"  //comment\n//comment\n  ", 2},
            };
        }
    }

    public static IEnumerable<object[]> SingleJsonWithCommentsTokenStartIndex
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"/*comment*/[]", 11},
                new object[] {"/*comment*/{}", 11},
                new object[] {"/*comment*/12345", 11},
                new object[] {"/*comment*/12345  ", 11},
                new object[] {"/*comment*/12345/*comment*/", 11},
                new object[] {"/*comment*/12345  /*comment*/", 11},
                new object[] {"/*comment*/12345  /*comment*/  ", 11},
                new object[] {"/*comment*/1", 11},
                new object[] {"/*comment*/true", 11},
                new object[] {"/*comment*/false", 11},
                new object[] {"/*comment*/null", 11},
                new object[] {"/*comment*/\"hello\"", 11},
                new object[] {"/*comment*/\"\"", 11},

                new object[] {"  /*comment*/  []", 15},
                new object[] {"  /*comment*/  {}", 15},
                new object[] {"  /*comment*/  12345", 15},
                new object[] { "  /*comment*/  12345  ", 15},
                new object[] { "  /*comment*/  12345/*comment*/", 15},
                new object[] { "  /*comment*/  12345  /*comment*/", 15},
                new object[] { "  /*comment*/  12345  /*comment*/  ", 15},
                new object[] {"  /*comment*/  1", 15},
                new object[] {"  /*comment*/  true", 15},
                new object[] {"  /*comment*/  false", 15},
                new object[] {"  /*comment*/  null", 15},
                new object[] {"  /*comment*/  \"hello\"", 15},
                new object[] {"  /*comment*/  \"\"", 15},
            };
        }
    }

    public static IEnumerable<object[]> SingleValueJson
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"  \"h\u6F22\u5B57ello\"  ", "h\u6F22\u5B57ello"},    // "\u6F22\u5B57" is Chinese for "Chinese character" (from the Han script)
                new object[] {"  \"he\\r\\n\\\"l\\\\\\\"lo\\\\\"  ", "he\\r\\n\\\"l\\\\\\\"lo\\\\"},
                new object[] {"  12345  ", "12345"},
                new object[] {"  null  ", "null"},
                new object[] {"  true  ", "true"},
                new object[] {"  false  ", "false"},
            };
        }
    }

    public static IEnumerable<object[]> SmallTestCases
    {
        get
        {
            return new List<object[]>
            {
                new object[] { true, TestCaseType.Basic, SR.BasicJson},
                new object[] { true, TestCaseType.BasicLargeNum, SR.BasicJsonWithLargeNum}, // Json.NET treats numbers starting with 0 as octal (0425 becomes 277)
                new object[] { true, TestCaseType.FullSchema1, SR.FullJsonSchema1},
                new object[] { true, TestCaseType.HelloWorld, SR.HelloWorld},
                new object[] { true, TestCaseType.Json400B, SR.Json400B},

                new object[] { false, TestCaseType.Basic, SR.BasicJson},
                new object[] { false, TestCaseType.BasicLargeNum, SR.BasicJsonWithLargeNum}, // Json.NET treats numbers starting with 0 as octal (0425 becomes 277)
                new object[] { false, TestCaseType.FullSchema1, SR.FullJsonSchema1},
                new object[] { false, TestCaseType.HelloWorld, SR.HelloWorld},
                new object[] { false, TestCaseType.Json400B, SR.Json400B},
            };
        }
    }

    public static IEnumerable<object[]> SpecialNumTestCases
    {
        get
        {
            return new List<object[]>
            {
                new object[] { TestCaseType.FullSchema2, SR.FullJsonSchema2},
                new object[] { TestCaseType.SpecialNumForm, SR.JsonWithSpecialNumFormat},
            };
        }
    }

    public static IEnumerable<object[]> TestCases
    {
        get
        {
            return new List<object[]>
            {
                new object[] { true, TestCaseType.Basic, SR.BasicJson},
                new object[] { true, TestCaseType.BasicLargeNum, SR.BasicJsonWithLargeNum}, // Json.NET treats numbers starting with 0 as octal (0425 becomes 277)
                new object[] { true, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Json.NET and Corvus.Text.Json
                new object[] { true, TestCaseType.DeepTree, SR.DeepTree},
                new object[] { true, TestCaseType.FullSchema1, SR.FullJsonSchema1},
                new object[] { true, TestCaseType.HelloWorld, SR.HelloWorld},
                new object[] { true, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                new object[] { true, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                new object[] { true, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
                new object[] { true, TestCaseType.Json400B, SR.Json400B},
                new object[] { true, TestCaseType.Json4KB, SR.Json4KB},
                new object[] { true, TestCaseType.Json40KB, SR.Json40KB},
                new object[] { true, TestCaseType.Json400KB, SR.Json400KB},

                new object[] { false, TestCaseType.Basic, SR.BasicJson},
                new object[] { false, TestCaseType.BasicLargeNum, SR.BasicJsonWithLargeNum}, // Json.NET treats numbers starting with 0 as octal (0425 becomes 277)
                new object[] { false, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Json.NET and Corvus.Text.Json
                new object[] { false, TestCaseType.DeepTree, SR.DeepTree},
                new object[] { false, TestCaseType.FullSchema1, SR.FullJsonSchema1},
                new object[] { false, TestCaseType.HelloWorld, SR.HelloWorld},
                new object[] { false, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
                new object[] { false, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
                new object[] { false, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
                new object[] { false, TestCaseType.Json400B, SR.Json400B},
                new object[] { false, TestCaseType.Json4KB, SR.Json4KB},
                new object[] { false, TestCaseType.Json40KB, SR.Json40KB},
                new object[] { false, TestCaseType.Json400KB, SR.Json400KB}
            };
        }
    }

    public static IEnumerable<object[]> TrySkipValues
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"[[[]], {\"a\":1, \"b\": 2}, 3, {\"a\":{}, \"b\":{\"c\":[]} }, [{\"a\":1, \"b\": 2}, null]]", JsonTokenType.EndArray},
                new object[] {"[]", JsonTokenType.EndArray},
                new object[] {"[[],[],[],[]]", JsonTokenType.EndArray},
                new object[] {"[[[],[],[]]]", JsonTokenType.EndArray},
                new object[] {"[{},{},{},{}]", JsonTokenType.EndArray},
                new object[] {"[{\"a\":[], \"b\":[], \"c\":[]}]", JsonTokenType.EndArray},
                new object[] {"{\"a\":{\"b\":{}}, \"c\":[1, 2], \"d\": 3, \"e\":[[], [{}] ], \"f\":{\"g\":[1, 2], \"e\":null}}", JsonTokenType.EndObject},
                new object[] {"{}", JsonTokenType.EndObject},
                new object[] {"{\"a\":{}, \"b\":{}, \"c\":{}, \"d\":{}}", JsonTokenType.EndObject},
                new object[] {"{\"a\":{\"b\":{}, \"c\":{}, \"d\":{}}}", JsonTokenType.EndObject},
                new object[] {"{\"a\":[], \"b\":[], \"c\":[], \"d\":[]}", JsonTokenType.EndObject},
                new object[] {"{\"a\":[{}, {}, {}]}", JsonTokenType.EndObject},
            };
        }
    }

    [TestMethod]
    [DataRow("//", "", 2)]
    [DataRow("//\n", "", 3)]
    [DataRow("/**/", "", 4)]
    [DataRow("/*/*/", "/", 5)]
    [DataRow("//T\u6F22\u5B57his is a \u6F22\u5B57comment before json\n\"hello\"", "T\u6F22\u5B57his is a \u6F22\u5B57comment before json", 44)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json", "This is a \u6F22\u5B57comment after json", 49)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json\n", "This is a \u6F22\u5B57comment after json", 50)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment", "This is a \u6F22\u5B57comment after json", 53)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a \u6F22\u5B57comment after json", 52)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment", "This is a \u6F22\u5B57comment after json", 53)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a \u6F22\u5B57comment after json", 53)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json with new line\n", "This is a \u6F22\u5B57comment after json with new line", 64)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a \u6F22\u5B57comment between key-value pairs\n 30}", "This is a \u6F22\u5B57comment between key-value pairs", 66)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a \u6F22\u5B57comment between key-value pairs on the same line\n}", "This is a \u6F22\u5B57comment between key-value pairs on the same line", 84)]
    [DataRow("/*T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json*/\"hello\"", "T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json", 56)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line \u6F22\u5B57comment after json*/", "This is a multi-line \u6F22\u5B57comment after json", 62)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", "This is a multi-line \u6F22\u5B57comment after json", 65)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a multi-line \u6F22\u5B57comment after json", 64)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", "This is a multi-line \u6F22\u5B57comment after json", 65)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a multi-line \u6F22\u5B57comment after json", 65)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a \u6F22\u5B57comment between key-value pairs*/ 30}", "This is a \u6F22\u5B57comment between key-value pairs", 67)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a \u6F22\u5B57comment between key-value pairs on the same line*/}", "This is a \u6F22\u5B57comment between key-value pairs on the same line", 85)]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json*/\"hello\"", "T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json", 63)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \n\u6F22\u5B57comment after json*/", "This is a split multi-line \n\u6F22\u5B57comment after json", 69)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", "This is a split multi-line \n\u6F22\u5B57comment after json", 72)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a split multi-line \n\u6F22\u5B57comment after json", 71)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", "This is a split multi-line \n\u6F22\u5B57comment after json", 72)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a split multi-line \n\u6F22\u5B57comment after json", 72)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs*/ 30}", "This is a split multi-line \n\u6F22\u5B57comment between key-value pairs", 85)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line*/}", "This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line", 103)]
    public void Allow(string jsonString, string expectedComment, int expectedIndex)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        bool foundComment = false;
        long indexAfterFirstComment = 0;
        while (json.Read())
        {
            Assert.IsTrue(json.ValueSequence.IsEmpty);
            JsonTokenType tokenType = json.TokenType;
            switch (tokenType)
            {
                case JsonTokenType.Comment:
                    if (foundComment)
                        break;
                    foundComment = true;
                    indexAfterFirstComment = json.BytesConsumed;
                    string actualComment = json.GetComment();
                    Assert.AreEqual(expectedComment, actualComment);
                    break;
            }
        }
        Assert.IsTrue(foundComment);
        Assert.AreEqual(expectedIndex, indexAfterFirstComment);
    }

    [TestMethod]
    [DataRow("[123, 456]", "123456", "123456")]
    [DataRow("/*a*/[{\"testA\":[{\"testB\":[{\"testC\":123}]}]}]", "testAtestBtestC123", "atestAtestBtestC123")]
    [DataRow("{\"testA\":[1/*hi*//*bye*/, 2, 3], \"testB\": 4}", "testA123testB4", "testA1hibye23testB4")]
    [DataRow("{\"test\":[[[123,456]]]}", "test123456", "test123456")]
    [DataRow("/*a*//*z*/[/*b*//*z*/123/*c*//*z*/,/*d*//*z*/456/*e*//*z*/]/*f*//*z*/", "123456", "azbz123czdz456ezfz")]
    [DataRow("[123,/*hi*/456/*bye*/]", "123456", "123hi456bye")]
    [DataRow("[123,//hi\n456//bye\n]", "123456", "123hi456bye")]
    [DataRow("[123,//hi\r456//bye\r]", "123456", "123hi456bye")]
    [DataRow("[123,//hi\r\n456\r\n]", "123456", "123hi456")]
    [DataRow("/*a*//*z*/{/*b*//*z*/\"test\":/*c*//*z*/[/*d*//*z*/[/*e*//*z*/[/*f*//*z*/123/*g*//*z*/,/*h*//*z*/456/*i*//*z*/]/*j*//*z*/]/*k*//*z*/]/*l*//*z*/}/*m*//*z*/",
"test123456", "azbztestczdzezfz123gzhz456izjzkzlzmz")]
    [DataRow("//a\n//z\n{//b\n//z\n\"test\"://c\n//z\n[//d\n//z\n[//e\n//z\n[//f\n//z\n123//g\n//z\n,//h\n//z\n456//i\n//z\n]//j\n//z\n]//k\n//z\n]//l\n//z\n}//m\n//z\n",
"test123456", "azbztestczdzezfz123gzhz456izjzkzlzmz")]
    public void AllowCommentStackMismatch(string jsonString, string expectedWithoutComments, string expectedWithComments)
    {
        byte[] data = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            for (int i = 0; i < data.Length; i++)
            {
                var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
                var json = new Utf8JsonReader(data.AsSpan(0, i), false, state);

                var builder = new StringBuilder();
                while (json.Read())
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                    if (json.TokenType == JsonTokenType.Number || json.TokenType == JsonTokenType.Comment || json.TokenType == JsonTokenType.PropertyName)
                        builder.Append(Encoding.UTF8.GetString(json.ValueSpan.ToArray()));
                }

                long consumed = json.BytesConsumed;
                json = new Utf8JsonReader(data.AsSpan((int)consumed), true, json.CurrentState);
                while (json.Read())
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                    if (json.TokenType == JsonTokenType.Number || json.TokenType == JsonTokenType.Comment || json.TokenType == JsonTokenType.PropertyName)
                        builder.Append(Encoding.UTF8.GetString(json.ValueSpan.ToArray()));
                }
                Assert.AreEqual(data.Length - consumed, json.BytesConsumed);

                Assert.AreEqual(commentHandling == JsonCommentHandling.Allow ? expectedWithComments : expectedWithoutComments, builder.ToString());
            }
        }
    }

    [TestMethod]
    [DataRow("{ } 1", new[] { JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.Number })]
    [DataRow("{ }1", new[] { JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.Number })]
    [DataRow("{ }\t\r\n           1", new[] { JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.Number })]
    [DataRow("1 3.14 false null", new[] { JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.False, JsonTokenType.Null })]
    [DataRow("42", new[] { JsonTokenType.Number })]
    [DataRow("\"str\"\"str\"null", new[] { JsonTokenType.String, JsonTokenType.String, JsonTokenType.Null })]
    [DataRow("[]{}[]", new[] { JsonTokenType.StartArray, JsonTokenType.EndArray, JsonTokenType.StartObject, JsonTokenType.EndObject, JsonTokenType.StartArray, JsonTokenType.EndArray })]
    public void AllowMultipleValues(string json, JsonTokenType[] expectedSequence)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), options);

        Assert.AreEqual(JsonTokenType.None, reader.TokenType);

        foreach (JsonTokenType expected in expectedSequence)
        {
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expected, reader.TokenType);
        }

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
    public void AllowMultipleValues_Comments_PartialData(string jsonValue, JsonTokenType firstTokenType)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true, CommentHandling = JsonCommentHandling.Skip };
        JsonReaderState state = new(options);
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(jsonValue + "/* comment */"), isFinalBlock: false, state);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        Assert.IsTrue(reader.TrySkip());
        Assert.IsFalse(reader.Read());

        reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonValue), isFinalBlock: true, reader.CurrentState);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        reader.Skip();
        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow(JsonCommentHandling.Allow)]
    [DataRow(JsonCommentHandling.Skip)]
    public void AllowMultipleValues_CommentSeparated(JsonCommentHandling commentHandling)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true, CommentHandling = commentHandling };
        Utf8JsonReader reader = new("{ }    /* I'm a comment */       1"u8, options);
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
    [DataRow("")]
    [DataRow("\t\r\n")]
    [DataRow("    \t\t                        ")]
    public void AllowMultipleValues_NoJsonContent_ReturnsFalse(string json)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(json), options);

        Assert.IsTrue(reader.IsFinalBlock);
        Assert.IsFalse(reader.Read());
        Assert.AreEqual(JsonTokenType.None, reader.TokenType);
    }

    [TestMethod]
    public void AllowMultipleValues_NonJsonTrailingData_ThrowsJsonException()
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        Utf8JsonReader reader = new("{ }      not JSON"u8, options);
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
    public void AllowMultipleValues_PartialData(string jsonValue, JsonTokenType firstTokenType)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        JsonReaderState state = new(options);
        Utf8JsonReader reader = new(Encoding.UTF8.GetBytes(jsonValue + " "), isFinalBlock: false, state);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(firstTokenType, reader.TokenType);
        Assert.IsTrue(reader.TrySkip());
        Assert.IsFalse(reader.Read());

        reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonValue), isFinalBlock: true, reader.CurrentState);

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
    public void AllowMultipleValues_SkipMultipleRepeatingValues(string jsonValue, JsonTokenType firstTokenType)
    {
        JsonReaderOptions options = new() { AllowMultipleValues = true };
        string payload = string.Join("\r\n", Enumerable.Repeat(jsonValue, 10));
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(payload), options);

        for (int i = 0; i < 10; i++)
        {
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(firstTokenType, reader.TokenType);
            reader.Skip();
        }
    }

    [TestMethod]
    [DataRow("//", "", 2)]
    [DataRow("//\n", "", 3)]
    [DataRow("/**/", "", 4)]
    [DataRow("/*/*/", "/", 5)]
    [DataRow("//T\u6F22\u5B57his is a \u6F22\u5B57comment before json\n\"hello\"", "T\u6F22\u5B57his is a \u6F22\u5B57comment before json", 44)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json", "This is a \u6F22\u5B57comment after json", 49)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json\n", "This is a \u6F22\u5B57comment after json", 50)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment", "This is a \u6F22\u5B57comment after json", 53)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a \u6F22\u5B57comment after json", 52)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment", "This is a \u6F22\u5B57comment after json", 53)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a \u6F22\u5B57comment after json", 53)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json with new line\n", "This is a \u6F22\u5B57comment after json with new line", 64)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a \u6F22\u5B57comment between key-value pairs\n 30}", "This is a \u6F22\u5B57comment between key-value pairs", 66)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a \u6F22\u5B57comment between key-value pairs on the same line\n}", "This is a \u6F22\u5B57comment between key-value pairs on the same line", 84)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a carriage return\r//Another single-line comment", "This is a comment with a carriage return", 59)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a line break\n//Another single-line comment", "This is a comment with a line break", 54)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a carriage return and line break\r\n//Another single-line comment", "This is a comment with a carriage return and line break", 75)]
    [DataRow("/*T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json*/\"hello\"", "T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json", 56)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line \u6F22\u5B57comment after json*/", "This is a multi-line \u6F22\u5B57comment after json", 62)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", "This is a multi-line \u6F22\u5B57comment after json", 65)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a multi-line \u6F22\u5B57comment after json", 64)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", "This is a multi-line \u6F22\u5B57comment after json", 65)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a multi-line \u6F22\u5B57comment after json", 65)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a \u6F22\u5B57comment between key-value pairs*/ 30}", "This is a \u6F22\u5B57comment between key-value pairs", 67)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a \u6F22\u5B57comment between key-value pairs on the same line*/}", "This is a \u6F22\u5B57comment between key-value pairs on the same line", 85)]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json*/\"hello\"", "T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json", 63)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \n\u6F22\u5B57comment after json*/", "This is a split multi-line \n\u6F22\u5B57comment after json", 69)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", "This is a split multi-line \n\u6F22\u5B57comment after json", 72)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a split multi-line \n\u6F22\u5B57comment after json", 71)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", "This is a split multi-line \n\u6F22\u5B57comment after json", 72)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", "This is a split multi-line \n\u6F22\u5B57comment after json", 72)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs*/ 30}", "This is a split multi-line \n\u6F22\u5B57comment between key-value pairs", 85)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line*/}", "This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line", 103)]
    [DataRow("{\r\n   \"value\": 11,\r\n   /* yes, it's mis-spelled */\r\n   \"deelay\": 3\r\n}", " yes, it's mis-spelled ", 50)]
    [DataRow("[\r\n   12,\r\n   87,\r\n   /* Isn't it \"nice\" that JSON provides no limits on the length of numbers? */\r\n   123456789012345678901234567890123456789.01234567890123456789e+9876543218976543219876543210\r\n]",
        " Isn't it \"nice\" that JSON provides no limits on the length of numbers? ", 98)]
    public void AllowSingleSegment(string jsonString, string expectedComment, int expectedIndex)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        bool foundComment = false;
        long indexAfterFirstComment = 0;
        while (json.Read())
        {
            Assert.IsTrue(json.ValueSequence.IsEmpty);
            JsonTokenType tokenType = json.TokenType;
            switch (tokenType)
            {
                case JsonTokenType.Comment:
                    if (foundComment)
                        break;
                    foundComment = true;
                    indexAfterFirstComment = json.BytesConsumed;
                    string actualComment = json.GetComment();
                    Assert.AreEqual(expectedComment, actualComment);
                    break;
            }
        }
        Assert.IsTrue(foundComment);
        Assert.AreEqual(expectedIndex, indexAfterFirstComment);

        for (int i = 0; i < dataUtf8.Length; i++)
        {
            var stateInner = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
            var jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, stateInner);

            foundComment = false;
            indexAfterFirstComment = 0;
            while (jsonSlice.Read())
            {
                Assert.IsTrue(json.ValueSequence.IsEmpty);
                JsonTokenType tokenType = jsonSlice.TokenType;
                switch (tokenType)
                {
                    case JsonTokenType.Comment:
                        if (foundComment)
                            break;
                        foundComment = true;
                        indexAfterFirstComment = jsonSlice.BytesConsumed;
                        string actualComment = jsonSlice.GetComment();
                        Assert.AreEqual(expectedComment, actualComment);
                        break;
                }
            }

            int consumed = (int)jsonSlice.BytesConsumed;
            jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan(consumed), isFinalBlock: true, jsonSlice.CurrentState);

            if (!foundComment)
            {
                while (jsonSlice.Read())
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                    JsonTokenType tokenType = jsonSlice.TokenType;
                    switch (tokenType)
                    {
                        case JsonTokenType.Comment:
                            if (foundComment)
                                break;
                            foundComment = true;
                            indexAfterFirstComment = jsonSlice.BytesConsumed;
                            string actualComment = jsonSlice.GetComment();
                            Assert.AreEqual(expectedComment, actualComment);
                            break;
                    }
                }
                indexAfterFirstComment += consumed;
            }

            Assert.IsTrue(foundComment);
            Assert.AreEqual(expectedIndex, indexAfterFirstComment);
        }
    }

    [TestMethod]
    [DataRow("[]", 1, 2)]
    [DataRow("[1, 2, 3, 4, 5]", 1, 15)]
    [DataRow("{\"foo\":1}", 2, 8)]
    [DataRow("{\"foo\":[1, 2, 3]}", 2, 16)]
    public void BasicSkipTest(string jsonString, int readCount, int expectedConsumed)
    {
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: true, state: default);
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            reader.Skip();
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: true, state: default);
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            Assert.IsTrue(reader.TrySkip());
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: false, state: default);
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            Assert.IsTrue(reader.TrySkip());
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }

        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString));
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            reader.Skip();
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString));
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            Assert.IsTrue(reader.TrySkip());
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }
    }

    [TestMethod]
    [DataRow("[", 1, 1)]
    [DataRow("[1, 2, 3, 4, 5", 1, 1)]
    [DataRow("{\"foo\":1", 2, 7)]
    [DataRow("{\"foo\":[1, 2, 3", 2, 7)]
    public void BasicTrySkipIncomplete(string jsonString, int readCount, int expectedConsumed)
    {
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: true, state: default);
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            try
            {
                reader.Skip();
                Assert.Fail("Expected JsonException was not thrown for incomplete JSON payload when skipping.");
            }
            catch (JsonException) { }
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: true, state: default);
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            try
            {
                reader.TrySkip();
                Assert.Fail("Expected JsonException was not thrown for incomplete JSON payload when skipping.");
            }
            catch (JsonException) { }
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: false, state: default);
            for (int i = 0; i < readCount; i++)
            {
                reader.Read();
            }

            Assert.IsFalse(reader.TrySkip());
            Assert.AreEqual(expectedConsumed, reader.BytesConsumed);
        }
    }

    [TestMethod]
    [DataRow("//", 0, 0)]
    [DataRow("//\n", 0, 0)]
    [DataRow("/**/", 0, 0)]
    [DataRow("/*/*/", 0, 0)]
    [DataRow("//T\u6F22\u5B57his is a \u6F22\u5B57comment before json\n\"hello\"", 0, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json", 0, 13)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json\n", 0, 13)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json with new line\n", 0, 13)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a \u6F22\u5B57comment between key-value pairs\n 30}", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a \u6F22\u5B57comment between key-value pairs on the same line\n}", 0, 17)]
    [DataRow("/*T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json*/\"hello\"", 0, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line \u6F22\u5B57comment after json*/", 0, 13)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a \u6F22\u5B57comment between key-value pairs*/ 30}", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a \u6F22\u5B57comment between key-value pairs on the same line*/}", 0, 17)]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json*/\"hello\"", 0, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \n\u6F22\u5B57comment after json*/", 0, 13)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs*/ 30}", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line*/}", 0, 17)]
    public void CommentsAreInvalidByDefault(string jsonString, int expectedlineNumber, int expectedPosition)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, default);

        try
        {
            while (json.Read())
            {
                JsonTokenType tokenType = json.TokenType;
                switch (tokenType)
                {
                    case JsonTokenType.Comment:
                        Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                        break;
                }
            }
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(expectedlineNumber, ex.LineNumber);
            Assert.AreEqual(expectedPosition, ex.BytePositionInLine);
        }
    }

    [TestMethod]
    [DataRow("//", 0, 0)]
    [DataRow("//\n", 0, 0)]
    [DataRow("/**/", 0, 0)]
    [DataRow("/*/*/", 0, 0)]
    [DataRow("//T\u6F22\u5B57his is a \u6F22\u5B57comment before json\n\"hello\"", 0, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json", 0, 13)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json\n", 0, 13)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json with new line\n", 0, 13)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a \u6F22\u5B57comment between key-value pairs\n 30}", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a \u6F22\u5B57comment between key-value pairs on the same line\n}", 0, 17)]
    [DataRow("/*T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json*/\"hello\"", 0, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line \u6F22\u5B57comment after json*/", 0, 13)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a \u6F22\u5B57comment between key-value pairs*/ 30}", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a \u6F22\u5B57comment between key-value pairs on the same line*/}", 0, 17)]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json*/\"hello\"", 0, 0)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \n\u6F22\u5B57comment after json*/", 0, 13)]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment", 1, 0)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs*/ 30}", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line*/}", 0, 17)]
    public void CommentsAreInvalidByDefaultSingleSegment(string jsonString, int expectedlineNumber, int expectedPosition)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, default);

        try
        {
            while (json.Read())
            {
                JsonTokenType tokenType = json.TokenType;
                switch (tokenType)
                {
                    case JsonTokenType.Comment:
                        Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                        break;
                }
            }
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(expectedlineNumber, ex.LineNumber);
            Assert.AreEqual(expectedPosition, ex.BytePositionInLine);
        }

        for (int i = 0; i < dataUtf8.Length; i++)
        {
            var jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, default);
            try
            {
                while (jsonSlice.Read())
                {
                    JsonTokenType tokenType = jsonSlice.TokenType;
                    switch (tokenType)
                    {
                        case JsonTokenType.Comment:
                            Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                            break;
                    }
                }

                jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan((int)jsonSlice.BytesConsumed), isFinalBlock: true, jsonSlice.CurrentState);
                while (jsonSlice.Read())
                {
                    JsonTokenType tokenType = jsonSlice.TokenType;
                    switch (tokenType)
                    {
                        case JsonTokenType.Comment:
                            Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                            break;
                    }
                }

                Assert.Fail("Expected JsonException was not thrown with multi-segment data.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedPosition, ex.BytePositionInLine);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(LotsOfCommentsTests))]
    public void ConsumeLotsOfComments(string valueString, bool insideArray, string expectedString)
    {
        var builder = new StringBuilder(2_000_000);
        if (insideArray)
        {
            builder.Append("[");
        }
        for (int i = 0; i < 100_000; i++)
        {
            builder.Append("// comment ").Append(i).Append("\n");
        }
        builder.Append(valueString);
        if (insideArray)
        {
            builder.Append("]");
        }
        string jsonString = builder.ToString();
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        bool foundPrimitiveValue = false;
        while (json.Read())
        {
            Assert.IsTrue(json.ValueSequence.IsEmpty);
            bool isTokenPrimitive = json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null;

            if (insideArray)
            {
                Assert.IsTrue(isTokenPrimitive || json.TokenType == JsonTokenType.Comment || json.TokenType == JsonTokenType.StartArray || json.TokenType == JsonTokenType.EndArray);
            }
            else
            {
                Assert.IsTrue(isTokenPrimitive || json.TokenType == JsonTokenType.Comment);
            }

            switch (json.TokenType)
            {
                case JsonTokenType.Null:
                    Assert.AreEqual(expectedString, Encoding.UTF8.GetString(json.ValueSpan.ToArray()));
                    Assert.IsNull(json.GetString());
                    foundPrimitiveValue = true;
                    break;

                case JsonTokenType.Number:
                    if (json.ValueSpan.IndexOf((byte)'.') != -1)
                    {
                        Assert.IsTrue(json.TryGetDouble(out double numberValue));
                        Assert.AreEqual(expectedString, numberValue.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Assert.IsTrue(json.TryGetInt32(out int numberValue));
                        Assert.AreEqual(expectedString, numberValue.ToString(CultureInfo.InvariantCulture));
                    }
                    foundPrimitiveValue = true;
                    break;

                case JsonTokenType.String:
                    string stringValue = json.GetString();
                    Assert.AreEqual(expectedString, stringValue);
                    foundPrimitiveValue = true;
                    break;

                case JsonTokenType.False:
                case JsonTokenType.True:
                    bool boolValue = json.GetBoolean();
                    Assert.AreEqual(expectedString, boolValue.ToString(CultureInfo.InvariantCulture));
                    foundPrimitiveValue = true;
                    break;
            }
            if (isTokenPrimitive)
            {
                Assert.AreEqual(insideArray ? 1688894 : 1688894 - 1, json.TokenStartIndex);
            }
        }
        Assert.IsTrue(foundPrimitiveValue);
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);

        if (insideArray)
        {
            Assert.IsTrue(json.TokenType == JsonTokenType.EndArray);
            Assert.AreEqual(dataUtf8.Length - 1, json.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(CommentTestLineSeparators))]
    public void ConsumeSingleLineCommentSingleSpanTest(string lineSeparator)
    {
        string expected = "Comment";
        string jsonData = "{//" + expected + lineSeparator + "}";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonData);

        for (int i = 0; i < jsonData.Length; i++)
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            VerifyReadLoop(ref json, expected);

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)json.BytesConsumed), isFinalBlock: true, json.CurrentState);
            VerifyReadLoop(ref json, expected);
        }
    }

    [TestMethod]
    public void CurrentDepthArrayTest()
    {
        string jsonString =
@"[
    [
        1,
        2,
        3
    ]
]";

        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);

            Assert.AreEqual(0, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.IsFalse(json.Read());
            Assert.AreEqual(0, json.CurrentDepth);
        }

        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
            var json = new Utf8JsonReader(dataUtf8);

            Assert.AreEqual(0, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.IsFalse(json.Read());
            Assert.AreEqual(0, json.CurrentDepth);
        }
    }

    [TestMethod]
    public void CurrentDepthObjectTest()
    {
        string jsonString =
@"{
    ""array"": [
        1,
        2,
        3,
        {}
    ]
}";

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);

        Assert.AreEqual(0, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(2, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(2, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(2, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(2, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(2, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.IsTrue(json.Read());
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.IsFalse(json.Read());
        Assert.AreEqual(0, json.CurrentDepth);
    }

    [TestMethod]
    public void DefaultUtf8JsonReader()
    {
        Utf8JsonReader json = default;

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);
        Assert.IsFalse(json.ValueIsEscaped);

        Assert.AreEqual(0, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.IsFalse(json.CurrentState.Options.AllowMultipleValues);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsFalse(json.Read());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.ValueTextEquals(""));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.ValueTextEquals("".AsSpan()));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.ValueTextEquals(default(ReadOnlySpan<char>)));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.ValueTextEquals(default(ReadOnlySpan<byte>)));

        TestGetMethodsOnDefault();
    }

    [TestMethod]
    public void EmptyJsonIsInvalid()
    {
        ReadOnlySpan<byte> dataUtf8 = ReadOnlySpan<byte>.Empty;
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);

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
    public void InitialState()
    {
        var json = new Utf8JsonReader("1"u8, isFinalBlock: true, state: default);

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.IsFalse(json.CurrentState.Options.AllowMultipleValues);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    public void InitialStateSimpleCtor()
    {
        var json = new Utf8JsonReader("1"u8);

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.IsFalse(json.CurrentState.Options.AllowMultipleValues);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    [DynamicData(nameof(InvalidJsonStrings))]
    public void InvalidJson(string jsonString, int expectedlineNumber, int expectedBytePosition, int maxDepth = 64)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown with single-segment data.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
                Assert.AreEqual(default, json.Position);
            }
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var json = new Utf8JsonReader(dataUtf8, new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown with single-segment data.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
                Assert.AreEqual(default, json.Position);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(InvalidJsonStrings))]
    public void InvalidJsonSingleSegment(string jsonString, int expectedlineNumber, int expectedBytePosition, int maxDepth = 64)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown with single-segment data.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
                Assert.AreEqual(default, json.Position);
            }

            for (int i = 0; i < dataUtf8.Length; i++)
            {
                try
                {
                    var stateInner = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
                    var jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, stateInner);
                    while (jsonSlice.Read())
                        ;

                    long consumed = jsonSlice.BytesConsumed;
                    Assert.AreEqual(default, json.Position);

                    JsonReaderState jsonState = jsonSlice.CurrentState;

                    jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, jsonState);
                    while (jsonSlice.Read())
                        ;

                    Assert.Fail("Expected JsonException was not thrown with multi-segment data.");
                }
                catch (JsonException ex)
                {
                    string errorMessage = $"expectedLineNumber: {expectedlineNumber} | actual: {ex.LineNumber} | index: {i} | option: {commentHandling}";
                    string firstSegmentString = Encoding.UTF8.GetString(dataUtf8, 0, i);
                    string secondSegmentString = Encoding.UTF8.GetString(dataUtf8, i, dataUtf8.Length - i);
                    errorMessage += " | " + firstSegmentString + " | " + secondSegmentString;
                    Assert.IsTrue(expectedlineNumber == ex.LineNumber, errorMessage);
                    errorMessage = $"expectedBytePosition: {expectedBytePosition} | actual: {ex.BytePositionInLine} | index: {i} | option: {commentHandling}";
                    errorMessage += " | " + firstSegmentString + " | " + secondSegmentString;
                    Assert.IsTrue(expectedBytePosition == ex.BytePositionInLine, errorMessage);
                    Assert.AreEqual(default, json.Position);
                }
            }
        }
    }

    [TestMethod]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false \"in\u6F22\u5B57valid\"\r\n}", 30, 30, 1, 28)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false \"in\u6F22\u5B57valid\"\r\n}", 31, 31, 1, 28)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false, \"in\u6F22\u5B57valid\"\r\n}", 30, 30, 2, 0)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false, \"in\u6F22\u5B57valid\"\r\n}", 31, 30, 2, 0)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false, \"in\u6F22\u5B57valid\"\r\n}", 32, 30, 2, 0)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false, 5\r\n}", 30, 30, 1, 29)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false, 5\r\n}", 31, 30, 1, 29)]
    [DataRow("{\r\n\"is\\r\\nAct\u6F22\u5B57ive\": false, 5\r\n}", 32, 30, 1, 29)]
    public void InvalidJsonSplitRemainsInvalid(string jsonString, int splitLocation, int consumed, int expectedlineNumber, int expectedBytePosition)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, splitLocation), false, state);
            while (json.Read())
                ;
            Assert.AreEqual(consumed, json.BytesConsumed);

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)json.BytesConsumed), true, json.CurrentState);
            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
            }
        }
    }

    [TestMethod]
    [DataRow("{]", 0, 1, 64)]
    [DataRow("[}", 0, 1, 64)]
    [DataRow("nulz", 0, 3, 64)]
    [DataRow("truz", 0, 3, 64)]
    [DataRow("falsz", 0, 4, 64)]
    [DataRow("\"a\u6F22\u5B57ge\":", 0, 11, 64)]
    [DataRow("12345.1.", 0, 7, 64)]
    [DataRow("-f", 0, 1, 64)]
    [DataRow("1.f", 0, 2, 64)]
    [DataRow("0.1f", 0, 3, 64)]
    [DataRow("0.1e1f", 0, 5, 64)]
    [DataRow("123,", 0, 3, 64)]
    [DataRow("01", 0, 1, 64)]
    [DataRow("-01", 0, 2, 64)]
    [DataRow("001", 0, 1, 64)]
    [DataRow("00h", 0, 1, 64)]
    [DataRow("[01", 0, 2, 64)]
    [DataRow("10.5e-0.2", 0, 7, 64)]
    [DataRow("{\"a\u6F22\u5B57ge\":30, \"ints\":[1, 2, 3, 4, 5.1e7.3]}", 0, 42, 64)]
    [DataRow("{\"a\u6F22\u5B57ge\":30, \r\n \"num\":-0.e, \r\n \"ints\":[1, 2, 3, 4, 5]}", 1, 10, 64)]
    [DataRow("{{}}", 0, 1, 64)]
    [DataRow("[[{{}}]]", 0, 3, 64)]
    [DataRow("[1, 2, 3, ]", 0, 10, 64)]
    [DataRow("{\"a\u6F22\u5B57ge\":30, \"ints\":[1, 2, 3, 4, 5}}", 0, 38, 64)]
    [DataRow("{\r\n\"isActive\": false \"\r\n}", 1, 18, 64)]
    [DataRow("[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2\":[}]]]]}]]]]", 1, 28, 64)]
    [DataRow("[[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"temp2\":[]},[}]]]]}]]]]", 1, 32, 64)]
    [DataRow("{\r\n\t\"isActive\": false,\r\n\t\"array\": [\r\n\t\t[{\r\n\t\t\t\"id\": 1\r\n\t\t}]\r\n\t]\r\n}", 3, 3, 3)]
    [DataRow("{\"Here is a \u6F22\u5B57string: \\\"\\\"\":\"Here is \u6F22\u5B57a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str:\"\\\"\\\"\"}", 0, 190, 64)]
    public void InvalidJsonWhenPartial(string jsonString, int expectedlineNumber, int expectedBytePosition, int maxDepth)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var json = new Utf8JsonReader(dataUtf8, false, state);

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
            }
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var json = new Utf8JsonReader(dataUtf8, new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
            }
        }
    }

    [TestMethod]
    [DataRow("//\n}", 1, 0)]
    [DataRow("//comment\n}", 1, 0)]
    [DataRow("/**/}", 0, 4)]
    [DataRow("/*\n*/}", 1, 2)]
    [DataRow("/*comment\n*/}", 1, 2)]
    [DataRow("/*/*/}", 0, 5)]
    [DataRow("//This is a comment before json\n\"hello\"{", 1, 7)]
    [DataRow("\"hello\"//This is a comment after json\n{", 1, 0)]
    [DataRow("\"gamma\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*/{//Another single-line comment", 3, 28)]
    [DataRow("\"delta\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/{", 4, 18)]
    [DataRow("\"hello\"//This is a comment after json with new line\n{", 1, 0)]
    [DataRow("{\"age\" : \n//This is a comment between key-value pairs\n 30}{", 2, 4)]
    [DataRow("{\"age\" : 30//This is a comment between key-value pairs on the same line\n}{", 1, 1)]
    [DataRow("/*This is a multi-line comment before json*/\"hello\"{", 0, 51)]
    [DataRow("\"hello\"/*This is a multi-line comment after json*/{", 0, 50)]
    [DataRow("\"gamma\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*/{//Another single-line comment", 2, 28)]
    [DataRow("\"delta\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/{", 3, 18)]
    [DataRow("{\"age\" : \n/*This is a comment between key-value pairs*/ 30}{", 1, 49)]
    [DataRow("{\"age\" : 30/*This is a comment between key-value pairs on the same line*/}{", 0, 74)]
    [DataRow("/*This is a split multi-line \ncomment before json*/\"hello\"{", 1, 28)]
    [DataRow("\"hello\"/*This is a split multi-line \ncomment after json*/{", 1, 20)]
    [DataRow("\"gamma\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*/{//Another single-line comment", 3, 28)]
    [DataRow("\"delta\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/{", 4, 18)]
    [DataRow("{\"age\" : \n/*This is a split multi-line \ncomment between key-value pairs*/ 30}{", 2, 37)]
    [DataRow("{\"age\" : 30/*This is a split multi-line \ncomment between key-value pairs on the same line*/}{", 1, 51)]
    [DataRow("//\n\u6F22\u5B57}", 1, 0)]
    [DataRow("//c\u6F22\u5B57omment\n\u6F22\u5B57}", 1, 0)]
    [DataRow("/**/\u6F22\u5B57}", 0, 4)]
    [DataRow("/*\n*/\u6F22\u5B57}", 1, 2)]
    [DataRow("/*c\u6F22\u5B57omment\n*/\u6F22\u5B57}", 1, 2)]
    [DataRow("/*/*/\u6F22\u5B57}", 0, 5)]
    [DataRow("//T\u6F22\u5B57his is a comment before json\n\"hello\"\u6F22\u5B57{", 1, 7)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a comment after json\n\u6F22\u5B57{", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*/\u6F22\u5B57{//Another single-line comment", 3, 28)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/\u6F22\u5B57{", 4, 18)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a comment after json with new line\n\u6F22\u5B57{", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a comment between key-value pairs\n 30}\u6F22\u5B57{", 2, 4)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a comment between key-value pairs on the same line\n}\u6F22\u5B57{", 1, 1)]
    [DataRow("/*T\u6F22\u5B57his is a multi-line comment before json*/\"hello\"\u6F22\u5B57{", 0, 57)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line comment after json*/\u6F22\u5B57{", 0, 56)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*/\u6F22\u5B57{//Another single-line comment", 2, 28)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/\u6F22\u5B57{", 3, 18)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a comment between key-value pairs*/ 30}\u6F22\u5B57{", 1, 49)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a comment between key-value pairs on the same line*/}\u6F22\u5B57{", 0, 80)]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \ncomment before json*/\"hello\"\u6F22\u5B57{", 1, 28)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \ncomment after json*/\u6F22\u5B57{", 1, 20)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*/\u6F22\u5B57{//Another single-line comment", 3, 28)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/\u6F22\u5B57{", 4, 18)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \ncomment between key-value pairs*/ 30}\u6F22\u5B57{", 2, 37)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \ncomment between key-value pairs on the same line*/}\u6F22\u5B57{", 1, 51)]
    [DataRow("{   // comment \n   ]", 1, 3)]
    [DataRow("[   // comment \n   }", 1, 3)]
    [DataRow("{   /* comment */   ]", 0, 20)]
    [DataRow("[   /* comment */   }", 0, 20)]
    public void InvalidJsonWithComments(string jsonString, int expectedlineNumber, int expectedPosition)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        try
        {
            while (json.Read())
                ;
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(expectedlineNumber, ex.LineNumber);
            Assert.AreEqual(expectedPosition, ex.BytePositionInLine);
        }

        state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        try
        {
            while (json.Read())
                ;
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(expectedlineNumber, ex.LineNumber);
            Assert.AreEqual(expectedPosition, ex.BytePositionInLine);
        }
    }

    [TestMethod]
    [DataRow("//\n}", 1, 0)]
    [DataRow("//comment\n}", 1, 0)]
    [DataRow("/**/}", 0, 4)]
    [DataRow("/*\n*/}", 1, 2)]
    [DataRow("/*comment\n*/}", 1, 2)]
    [DataRow("/*/*/}", 0, 5)]
    [DataRow("//This is a comment before json\n\"hello\"{", 1, 7)]
    [DataRow("\"hello\"//This is a comment after json\n{", 1, 0)]
    [DataRow("\"gamma\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*/{//Another single-line comment", 3, 28)]
    [DataRow("\"delta\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/{", 4, 18)]
    [DataRow("\"hello\"//This is a comment after json with new line\n{", 1, 0)]
    [DataRow("{\"age\" : \n//This is a comment between key-value pairs\n 30}{", 2, 4)]
    [DataRow("{\"age\" : 30//This is a comment between key-value pairs on the same line\n}{", 1, 1)]
    [DataRow("/*This is a multi-line comment before json*/\"hello\"{", 0, 51)]
    [DataRow("\"hello\"/*This is a multi-line comment after json*/{", 0, 50)]
    [DataRow("\"gamma\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*/{//Another single-line comment", 2, 28)]
    [DataRow("\"delta\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/{", 3, 18)]
    [DataRow("{\"age\" : \n/*This is a comment between key-value pairs*/ 30}{", 1, 49)]
    [DataRow("{\"age\" : 30/*This is a comment between key-value pairs on the same line*/}{", 0, 74)]
    [DataRow("/*This is a split multi-line \ncomment before json*/\"hello\"{", 1, 28)]
    [DataRow("\"hello\"/*This is a split multi-line \ncomment after json*/{", 1, 20)]
    [DataRow("\"gamma\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*/{//Another single-line comment", 3, 28)]
    [DataRow("\"delta\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/{", 4, 18)]
    [DataRow("{\"age\" : \n/*This is a split multi-line \ncomment between key-value pairs*/ 30}{", 2, 37)]
    [DataRow("{\"age\" : 30/*This is a split multi-line \ncomment between key-value pairs on the same line*/}{", 1, 51)]
    [DataRow("//\n\u6F22\u5B57}", 1, 0)]
    [DataRow("//c\u6F22\u5B57omment\n\u6F22\u5B57}", 1, 0)]
    [DataRow("/**/\u6F22\u5B57}", 0, 4)]
    [DataRow("/*\n*/\u6F22\u5B57}", 1, 2)]
    [DataRow("/*c\u6F22\u5B57omment\n*/\u6F22\u5B57}", 1, 2)]
    [DataRow("/*/*/\u6F22\u5B57}", 0, 5)]
    [DataRow("//T\u6F22\u5B57his is a comment before json\n\"hello\"\u6F22\u5B57{", 1, 7)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a comment after json\n\u6F22\u5B57{", 1, 0)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*/\u6F22\u5B57{//Another single-line comment", 3, 28)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/\u6F22\u5B57{", 4, 18)]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a comment after json with new line\n\u6F22\u5B57{", 1, 0)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a comment between key-value pairs\n 30}\u6F22\u5B57{", 2, 4)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a comment between key-value pairs on the same line\n}\u6F22\u5B57{", 1, 1)]
    [DataRow("/*T\u6F22\u5B57his is a multi-line comment before json*/\"hello\"\u6F22\u5B57{", 0, 57)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line comment after json*/\u6F22\u5B57{", 0, 56)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*/\u6F22\u5B57{//Another single-line comment", 2, 28)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/\u6F22\u5B57{", 3, 18)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a comment between key-value pairs*/ 30}\u6F22\u5B57{", 1, 49)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a comment between key-value pairs on the same line*/}\u6F22\u5B57{", 0, 80)]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \ncomment before json*/\"hello\"\u6F22\u5B57{", 1, 28)]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \ncomment after json*/\u6F22\u5B57{", 1, 20)]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*/\u6F22\u5B57{//Another single-line comment", 3, 28)]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \ncomment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/\u6F22\u5B57{", 4, 18)]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \ncomment between key-value pairs*/ 30}\u6F22\u5B57{", 2, 37)]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \ncomment between key-value pairs on the same line*/}\u6F22\u5B57{", 1, 51)]
    public void InvalidJsonWithCommentsSingleSegment(string jsonString, int expectedlineNumber, int expectedPosition)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        try
        {
            while (json.Read())
                ;
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(expectedlineNumber, ex.LineNumber);
            Assert.AreEqual(expectedPosition, ex.BytePositionInLine);
        }
    }

    [TestMethod]
    [DataRow("//", 2, 0)]
    [DataRow("//\n", 0, 1)]
    [DataRow("/**/", 4, 0)]
    [DataRow("/*/*/", 5, 0)]
    [DataRow("// just a comment", 17, 0)]
    [DataRow(" /* comment and whitespace */ ", 30, 0)]
    public void JsonContainingOnlyCommentsIsInvalid(string jsonString, int expectedConsumed, int expectedLineNumber)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        try
        {
            while (json.Read())
                ;
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(expectedLineNumber, ex.LineNumber);
            Assert.AreEqual(expectedConsumed, ex.BytePositionInLine);
        }
    }

    [TestMethod]
    public void JsonContainingOnlyWhitespaceIsInvalid()
    {
        byte[] dataUtf8 = " "u8.ToArray();
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);

        try
        {
            while (json.Read())
                ;
            Assert.Fail("Expected JsonException was not thrown with single-segment data.");
        }
        catch (JsonException ex)
        {
            Assert.AreEqual(0, ex.LineNumber);
            Assert.AreEqual(1, ex.BytePositionInLine);
        }
    }

    [TestMethod]
    [DataRow("//asd\r", "asd", 6)]
    [DataRow("//asd\r\n", "asd", 7)]
    [DataRow("//asd\n", "asd", 6)]
    public void JsonWithASingleCommentEndingWithLineEndingSingleSegment(string jsonString, string expectedComment, int expectedBytesConsumed)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        Assert.IsTrue(json.Read());
        Assert.AreEqual(expectedComment, json.GetComment());
        Assert.IsFalse(json.Read());
        Assert.AreEqual(expectedBytesConsumed, json.BytesConsumed);
    }

    [TestMethod]
    // Non-standard line-endings are: \u2028 \u2029 (E2, 80, A8/A9)
    // last byte does not match (E2, 80, AA)
    [DataRow("//\u2030\n", "\u2030")]
    [DataRow("//\u2030 \r\n", "\u2030 ")]
    [DataRow("// \u2030\r", " \u2030")]
    [DataRow("//\u2030\u2031\n", "\u2030\u2031")]
    [DataRow("//\u2030 \u2031\r\n", "\u2030 \u2031")]
    [DataRow("// \u2030 \u2031\n", " \u2030 \u2031")]
    [DataRow("// \u2030 \u2031 \r", " \u2030 \u2031 ")]
    // second byte does not match (E2, 81, A8/A9)
    [DataRow("//\u2069\r\n", "\u2069")]
    [DataRow("// \u2069\r", " \u2069")]
    [DataRow("//\u2069 \n", "\u2069 ")]
    [DataRow("//\u2069\u2068\n", "\u2069\u2068")]
    [DataRow("//\u2069 \u2068\n", "\u2069 \u2068")]
    [DataRow("//\u2069 \u2068 \u2068\n", "\u2069 \u2068 \u2068")]
    [DataRow("// \u2069 \u2068 \u2068 \n", " \u2069 \u2068 \u2068 ")]
    public void JsonWithSingleCorrectLineCommentWithPartialNonStandardLineEnding(string jsonString, string expectedComment)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        Assert.IsTrue(json.Read());
        Assert.AreEqual(expectedComment, json.GetComment());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    [DataRow("//\u2028")]
    [DataRow("//\u2029")]
    [DataRow("// \u2028")]
    [DataRow("//   \u2028")]
    [DataRow("//  \u2029 ")]
    [DataRow("//  \u2029  ")]
    [DataRow("//\u2028\n")]
    [DataRow("//\u2028 \n")]
    [DataRow("// \u2028\n")]
    [DataRow("// \u2028 \n")]
    [DataRow("//\u2028\r\n")]
    [DataRow("//\u2028 \r\n")]
    [DataRow("// \u2028\r\n")]
    [DataRow("// \u2028 \r\n")]
    [DataRow("//\u2028\r")]
    [DataRow("//\u2028 \r")]
    [DataRow("// \u2028\r")]
    [DataRow("// \u2028 \r")]
    public void JsonWithSingleLineCommentEndingWithNonStandardLineEnding(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            try
            {
                json.Read();
                Assert.Fail($"Expected JsonException was not thrown. CommentHandling = {commentHandling}");
            }
            catch (JsonException) { }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithInvalidTrailingCommas))]
    public void JsonWithTrailingCommas_Invalid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(utf8, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(utf8, state, allowTrailingCommas, expectThrow: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommas))]
    public void JsonWithTrailingCommas_Valid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        {
            JsonReaderState state = default;
            TrailingCommasHelper(utf8, state, allow: false, expectThrow: true);
        }

        {
            var state = new JsonReaderState(options: default);
            TrailingCommasHelper(utf8, state, allow: false, expectThrow: true);
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(utf8, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(utf8, state, allowTrailingCommas, expectThrow: false);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithInvalidTrailingCommasAndComments))]
    public void JsonWithTrailingCommasAndComments_Invalid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(utf8, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(utf8, state, allowTrailingCommas, expectThrow: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommasAndComments))]
    public void JsonWithTrailingCommasAndComments_Valid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            TrailingCommasHelper(utf8, state, allow: false, expectThrow: true);

            bool allowTrailingCommas = true;
            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = allowTrailingCommas });
            TrailingCommasHelper(utf8, state, allowTrailingCommas, expectThrow: false);
        }
    }

    [TestMethod]
    [DataRow("\"h\u6F22\u5B57ello\"", 1, 0)] // "\""
    [DataRow("12345", 3, 0)]   // "123"
    [DataRow("null", 3, 0)]   // "nul"
    [DataRow("true", 3, 0)]   // "tru"
    [DataRow("false", 4, 0)]  // "fals"
    [DataRow("   {\"a\u6F22\u5B57ge\":30}   ", 16, 16)] // "   {\"a\u6F22\u5B57ge\":"
    [DataRow("{\"n\u6F22\u5B57ame\":\"A\u6F22\u5B57hson\"}", 15, 14)]  // "{\"n\u6F22\u5B57ame\":\"A\u6F22\u5B57hso"
    [DataRow("-123456789", 1, 0)] // "-"
    [DataRow("0.5", 2, 0)]    // "0."
    [DataRow("10.5e+3", 5, 0)] // "10.5e"
    [DataRow("10.5e-1", 6, 0)]    // "10.5e-"
    [DataRow("{\"i\u6F22\u5B57nts\":[1, 2, 3, 4, 5]}", 27, 25)]    // "{\"i\u6F22\u5B57nts\":[1, 2, 3, 4, "
    [DataRow("{\"s\u6F22\u5B57trings\":[\"a\u6F22\u5B57bc\", \"def\"], \"ints\":[1, 2, 3, 4, 5]}", 36, 36)]  // "{\"s\u6F22\u5B57trings\":[\"a\u6F22\u5B57bc\", \"def\""
    [DataRow("{\"a\u6F22\u5B57ge\":30, \"name\":\"test}:[]\", \"another \u6F22\u5B57string\" : \"tests\"}", 25, 24)]   // "{\"a\u6F22\u5B57ge\":30, \"name\":\"test}"
    [DataRow("   [[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"t\u6F22\u5B57emp2:[]}]]]]}]]]]\":[]}]]]]}]]]]   ", 54, 29)] // "   [[[[{\r\n\"t\u6F22\u5B57emp1\":[[[[{\"t\u6F22\u5B57emp2:[]}]]]]}]]]]"
    [DataRow("{\r\n\"is\u6F22\u5B57Active\": false, \"in\u6F22\u5B57valid\"\r\n : \"now its \u6F22\u5B57valid\"}", 26, 26)]  // "{\r\n\"is\u6F22\u5B57Active\": false, \"in\u6F22\u5B57valid\"\r\n}"
    [DataRow("{\"property\\u1234Name\": \"String value with hex: \\uABCD in the middle.\"}", 51, 23)]  // "{\"property\\u1234Name\": \"String value with hex: \\uAB"
    [DataRow("{ \"number\": 0}", 13, 12)]    // "{ \"number\": 0"
    public void PartialJson(string jsonString, int splitLocation, int consumed)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, splitLocation), false, state);
            while (json.Read())
                ;
            Assert.AreEqual(consumed, json.BytesConsumed);
            Assert.AreEqual(default, json.Position);

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)json.BytesConsumed), true, json.CurrentState);
            while (json.Read())
                ;
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
            Assert.AreEqual(default, json.Position);
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (jsonString == "12345")
            {
                continue;
            }

            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, splitLocation), options: new JsonReaderOptions { CommentHandling = commentHandling });

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown.");
            }
            catch (JsonException) { }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithInvalidTrailingCommas))]
    public void PartialJsonWithTrailingCommas_Invalid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommas))]
    public void PartialJsonWithTrailingCommas_Valid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        {
            JsonReaderState state = default;
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);
        }

        {
            var state = new JsonReaderState(options: default);
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            TrailingCommasHelperPartial(utf8, state, expectThrow: false);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithInvalidTrailingCommasAndComments))]
    public void PartialJsonWithTrailingCommasAndComments_Invalid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommasAndComments))]
    public void PartialJsonWithTrailingCommasAndComments_Valid(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            TrailingCommasHelperPartial(utf8, state, expectThrow: true);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            TrailingCommasHelperPartial(utf8, state, expectThrow: false);
        }
    }

    [TestMethod]
    [DataRow("{\"text\": \"\u0E4F\u0020\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35\\uABCZ \u0E42\u0E25\u0E01\"}", 0, 37)]   // * Hello\\uABCZ World in thai
    [DataRow("{\"text\": \"\u0E4F\u0020\u0E2A\u0E39\u0E07\\n\u0E15\u0E48\u0E33\\uABCZ \u0E42\u0E25\u0E01\"}", 0, 39)]    // * High\\nlow\\uABCZ World in thai
    public void PositionInCodeUnits(string jsonString, int expectedlineNumber, int expectedBytePosition)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8, false, state);

            try
            {
                while (json.Read())
                    ;
                Assert.Fail("Expected JsonException was not thrown.");
            }
            catch (JsonException ex)
            {
                Assert.AreEqual(expectedlineNumber, ex.LineNumber);
                Assert.AreEqual(expectedBytePosition, ex.BytePositionInLine);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonTokenWithExtraValue))]
    public void ReadJsonTokenWithExtraValue(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            TestReadTokenWithExtra(utf8, commentHandling, isFinalBlock: false);
            TestReadTokenWithExtra(utf8, commentHandling, isFinalBlock: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonTokenWithExtraValueAndComments))]
    public void ReadJsonTokenWithExtraValueAndComments(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            TestReadTokenWithExtra(utf8, commentHandling, isFinalBlock: false);
            TestReadTokenWithExtra(utf8, commentHandling, isFinalBlock: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonTokenWithExtraValueAndComments))]
    public void ReadJsonTokenWithExtraValueAndCommentsAppended(string jsonString)
    {
        jsonString = "  /* comment */  /* comment */  " + jsonString;
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            TestReadTokenWithExtra(utf8, commentHandling, isFinalBlock: false, commentsAppended: true);
            TestReadTokenWithExtra(utf8, commentHandling, isFinalBlock: true, commentsAppended: true);
        }
    }

    [TestMethod]
    [DynamicData(nameof(SingleValueJson))]
    public void SingleJsonValue(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
                var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), false, state);
                while (json.Read())
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                    // Check if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
                    Assert.IsTrue(json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null);
                    Assert.AreEqual(expectedString, Encoding.UTF8.GetString(json.ValueSpan.ToArray()));
                    Assert.AreEqual(2, json.TokenStartIndex);
                }

                long consumed = json.BytesConsumed;
                json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), true, json.CurrentState);
                while (json.Read())
                {
                    Assert.IsTrue(json.ValueSequence.IsEmpty);
                    // Check if the TokenType is a primitive "value", i.e. String, Number, True, False, and Null
                    Assert.IsTrue(json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null);
                    Assert.AreEqual(expectedString, Encoding.UTF8.GetString(json.ValueSpan.ToArray()));
                    if (consumed <= 2)
                    {
                        Assert.AreEqual(2 - consumed, json.TokenStartIndex);
                    }
                }
                Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
            }
        }
    }

    [TestMethod]
    [DataRow("//T\u6F22\u5B57his is a \u6F22\u5B57comment before json\n\"hello\"")]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json")]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json\n")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json with new line\n")]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a \u6F22\u5B57comment between key-value pairs\n 30}")]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a \u6F22\u5B57comment between key-value pairs on the same line\n}")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a carriage return\r//Another single-line comment")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a line break\n//Another single-line comment")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a carriage return and line break\r\n//Another single-line comment")]
    [DataRow("/*T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json*/\"hello\"")]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line \u6F22\u5B57comment after json*/")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a \u6F22\u5B57comment between key-value pairs*/ 30}")]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a \u6F22\u5B57comment between key-value pairs on the same line*/}")]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json*/\"hello\"")]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \n\u6F22\u5B57comment after json*/")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs*/ 30}")]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line*/}")]
    public void Skip(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        JsonTokenType prevTokenType = JsonTokenType.None;
        while (json.Read())
        {
            JsonTokenType tokenType = json.TokenType;
            switch (tokenType)
            {
                case JsonTokenType.Comment:
                    Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                    break;
            }
            Assert.AreNotEqual(tokenType, prevTokenType);
            prevTokenType = tokenType;
        }
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    public void SkipAtCommentDoesNothing()
    {
        string jsonString = "[//some comment\n 1, 2, 3]";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        Assert.IsTrue(json.Read());
        Assert.IsTrue(json.Read());
        Assert.AreEqual(JsonTokenType.Comment, json.TokenType);

        json.Skip();

        Assert.AreEqual(JsonTokenType.Comment, json.TokenType);
        Assert.AreEqual("some comment", json.GetComment());

        Assert.IsTrue(json.Read());
        Assert.AreEqual(JsonTokenType.Number, json.TokenType);
        Assert.AreEqual(1, json.GetInt32());

        json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        Assert.IsTrue(json.Read());
        Assert.IsTrue(json.Read());
        Assert.AreEqual(JsonTokenType.Comment, json.TokenType);

        Assert.IsTrue(json.TrySkip());

        Assert.AreEqual(JsonTokenType.Comment, json.TokenType);
        Assert.AreEqual("some comment", json.GetComment());

        Assert.IsTrue(json.Read());
        Assert.AreEqual(JsonTokenType.Number, json.TokenType);
        Assert.AreEqual(1, json.GetInt32());
    }

    [TestMethod]
    [DataRow("[[", 1, JsonTokenType.StartArray)]
    [DataRow("[[", 2, JsonTokenType.StartArray)]
    [DataRow("[[//comm", 2, JsonTokenType.StartArray)]
    [DataRow("[[[]], {\"a\":1, \"b\": 2}, 3, {\"a\":{}, \"b\":{\"c\":[]} }, [{\"a\":1, \"b\": 2}, null]", 1, JsonTokenType.StartArray)]
    [DataRow("[[[]], {\"a\":", 7, JsonTokenType.PropertyName)]
    public void SkipIncomplete(string jsonString, int numReads, JsonTokenType skipTokenType)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        {
            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
            for (int i = 0; i < numReads; i++)
            {
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(skipTokenType, json.TokenType);
            try
            {
                Assert.IsTrue(json.IsFinalBlock);
                json.Skip();
                Assert.Fail("Expected JsonException was not thrown for incomplete/invalid JSON payload when skipping.");
            }
            catch (JsonException) { }
        }

        {
            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
            for (int i = 0; i < numReads; i++)
            {
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(skipTokenType, json.TokenType);
            try
            {
                Assert.IsTrue(json.IsFinalBlock);
                json.TrySkip();
                Assert.Fail("Expected JsonException was not thrown for incomplete/invalid JSON payload when skipping.");
            }
            catch (JsonException) { }
        }

        {
            var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: false, state);
            for (int i = 0; i < numReads; i++)
            {
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(skipTokenType, json.TokenType);
            long before = json.BytesConsumed;
            Assert.IsFalse(json.IsFinalBlock);
            Assert.IsFalse(json.TrySkip());
            Assert.AreEqual(skipTokenType, json.TokenType);
            Assert.AreEqual(before, json.BytesConsumed);
        }
    }

    [TestMethod]
    public void SkipInvalid()
    {
        string jsonString = "[[[]], {\"a\":1, \"b\": 2}, 3, {\"a\":{}, {\"c\":[]} }, [{\"a\":1, \"b\": 2}, null]";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, default);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.StartArray, json.TokenType);
            try
            {
                Assert.IsTrue(json.IsFinalBlock);
                json.Skip();
                Assert.Fail("Expected JsonException was not thrown for invalid JSON payload when skipping.");
            }
            catch (JsonException) { }
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, default);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.StartArray, json.TokenType);
            try
            {
                Assert.IsTrue(json.IsFinalBlock);
                json.TrySkip();
                Assert.Fail("Expected JsonException was not thrown for invalid JSON payload when skipping.");
            }
            catch (JsonException) { }
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: false, default);
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.StartArray, json.TokenType);
            try
            {
                Assert.IsFalse(json.IsFinalBlock);
                json.TrySkip();
                Assert.Fail("Expected JsonException was not thrown for invalid JSON payload when skipping.");
            }
            catch (JsonException) { }
        }
    }

    [TestMethod]
    [DynamicData(nameof(LotsOfCommentsTests))]
    public void SkipLotsOfComments(string valueString, bool insideArray, string expectedString)
    {
        var builder = new StringBuilder(2_000_000);
        if (insideArray)
        {
            builder.Append("[");
        }
        for (int i = 0; i < 100_000; i++)
        {
            builder.Append("// comment ").Append(i).Append("\n");
        }
        builder.Append(valueString);
        if (insideArray)
        {
            builder.Append("]");
        }
        string jsonString = builder.ToString();
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        if (insideArray)
        {
            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.TokenType == JsonTokenType.StartArray);
            Assert.AreEqual(0, json.TokenStartIndex);
        }

        if (json.Read())
        {
            Assert.IsTrue(json.ValueSequence.IsEmpty);
            bool isTokenPrimitive = json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null;
            Assert.IsTrue(isTokenPrimitive);
            switch (json.TokenType)
            {
                case JsonTokenType.Null:
                    Assert.AreEqual(expectedString, Encoding.UTF8.GetString(json.ValueSpan.ToArray()));
                    break;

                case JsonTokenType.Number:
                    if (json.ValueSpan.IndexOf((byte)'.') != -1)
                    {
                        Assert.IsTrue(json.TryGetDouble(out double numberValue));
                        // Use InvariantCulture to format the numbers to make sure they retain the decimal point '.'
                        Assert.AreEqual(expectedString, numberValue.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        Assert.IsTrue(json.TryGetInt32(out int numberValue));
                        Assert.AreEqual(expectedString, numberValue.ToString(CultureInfo.InvariantCulture));
                    }
                    break;

                case JsonTokenType.String:
                    string stringValue = json.GetString();
                    Assert.AreEqual(expectedString, stringValue);
                    break;

                case JsonTokenType.False:
                case JsonTokenType.True:
                    bool boolValue = json.GetBoolean();
                    Assert.AreEqual(expectedString, boolValue.ToString(CultureInfo.InvariantCulture));
                    break;
            }
            Assert.AreEqual(insideArray ? 1688894 : 1688894 - 1, json.TokenStartIndex);
        }

        if (insideArray)
        {
            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.TokenType == JsonTokenType.EndArray);
            Assert.AreEqual(dataUtf8.Length - 1, json.TokenStartIndex);
        }

        Assert.IsFalse(json.Read());
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(CommentTestLineSeparators))]
    public void SkipSingleLineCommentSingleSpanTest(string lineSeparator)
    {
        string expected = "Comment";
        string jsonData = "{//" + expected + lineSeparator + "}";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonData);

        for (int i = 0; i < jsonData.Length; i++)
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            VerifyReadLoop(ref json, null);

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)json.BytesConsumed), isFinalBlock: true, json.CurrentState);
            VerifyReadLoop(ref json, null);
        }
    }

    [TestMethod]
    [DataRow("//T\u6F22\u5B57his is a \u6F22\u5B57comment before json\n\"hello\"")]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json")]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json\n")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n//This is a \u6F22\u5B57comment after json\n//Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"h\u6F22\u5B57ello\"//This is a \u6F22\u5B57comment after json with new line\n")]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n//This is a \u6F22\u5B57comment between key-value pairs\n 30}")]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30//This is a \u6F22\u5B57comment between key-value pairs on the same line\n}")]
    [DataRow("/*T\u6F22\u5B57his is a multi-line \u6F22\u5B57comment before json*/\"hello\"")]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a multi-line \u6F22\u5B57comment after json*/")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a multi-line \u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a \u6F22\u5B57comment between key-value pairs*/ 30}")]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a \u6F22\u5B57comment between key-value pairs on the same line*/}")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a carriage return\r//Another single-line comment")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a line break\n//Another single-line comment")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n//This is a comment with a carriage return and line break\r\n//Another single-line comment")]
    [DataRow("/*T\u6F22\u5B57his is a split multi-line \n\u6F22\u5B57comment before json*/\"hello\"")]
    [DataRow("\"h\u6F22\u5B57ello\"/*This is a split multi-line \n\u6F22\u5B57comment after json*/")]
    [DataRow("\"a\u6F22\u5B57lpha\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"b\u6F22\u5B57eta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("\"g\u6F22\u5B57amma\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment")]
    [DataRow("\"d\u6F22\u5B57elta\" \r\n/*This is a split multi-line \n\u6F22\u5B57comment after json*///Here is another comment\n/*and a multi-line comment*///Another single-line comment\n\t  /*blah * blah*/")]
    [DataRow("{\"a\u6F22\u5B57ge\" : \n/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs*/ 30}")]
    [DataRow("{\"a\u6F22\u5B57ge\" : 30/*This is a split multi-line \n\u6F22\u5B57comment between key-value pairs on the same line*/}")]
    [DataRow("{\r\n   \"value\": 11,\r\n   /* yes, it's mis-spelled */\r\n   \"deelay\": 3\r\n}")]
    [DataRow("[\r\n   12,\r\n   87,\r\n   /* Isn't it \"nice\" that JSON provides no limits on the length of numbers? */\r\n   123456789012345678901234567890123456789.01234567890123456789e+9876543218976543219876543210\r\n]")]
    public void SkipSingleSegment(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        while (json.Read())
        {
            JsonTokenType tokenType = json.TokenType;
            switch (tokenType)
            {
                case JsonTokenType.Comment:
                    Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                    break;
            }
        }
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);

        for (int i = 0; i < dataUtf8.Length; i++)
        {
            var stateInner = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });
            var jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, stateInner);

            while (jsonSlice.Read())
            {
                JsonTokenType tokenType = jsonSlice.TokenType;
                switch (tokenType)
                {
                    case JsonTokenType.Comment:
                        Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                        break;
                }
            }

            int prevConsumed = (int)jsonSlice.BytesConsumed;
            jsonSlice = new Utf8JsonReader(dataUtf8.AsSpan(prevConsumed), isFinalBlock: true, jsonSlice.CurrentState);

            while (jsonSlice.Read())
            {
                JsonTokenType tokenType = jsonSlice.TokenType;
                switch (tokenType)
                {
                    case JsonTokenType.Comment:
                        Assert.Fail("TokenType should never be 'Comment' when we are skipping them.");
                        break;
                }
            }

            Assert.AreEqual(dataUtf8.Length - prevConsumed, jsonSlice.BytesConsumed);
        }
    }

    [TestMethod]
    public void SkipTest()
    {
        string jsonString = @"{""propertyName"": {""foo"": ""bar""},""nestedArray"": {""numbers"": [1,2,3]}}";

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            json.Skip();
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.None, json.TokenType);
            Assert.AreEqual(0, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            // start object
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.StartObject, json.TokenType);
            Assert.AreEqual(0, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(0, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndObject, json.TokenType);
            Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            // start object, property
            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, json.TokenType);
            Assert.AreEqual(1, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndObject, json.TokenType);
            Assert.AreEqual(31, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            // start object, property, start object
            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.Read());
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.StartObject, json.TokenType);
            Assert.AreEqual(1, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndObject, json.TokenType);
            Assert.AreEqual(31, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 4; i++)
            {
                // start object, property, start object, property
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.PropertyName, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            Assert.AreEqual(30, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 5; i++)
            {
                // start object, property, start object, property, string value
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            Assert.AreEqual(30, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 6; i++)
            {
                // start object, property, start object, property, string value, end object
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.EndObject, json.TokenType);
            Assert.AreEqual(1, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(1, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndObject, json.TokenType);
            Assert.AreEqual(31, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 9; i++)
            {
                // start object, property, start object, property, string value, property, start object, property
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.PropertyName, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndArray, json.TokenType);
            Assert.AreEqual(66, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 10; i++)
            {
                // start object, property, start object, property, string value, property, start object, property, start array
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.StartArray, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndArray, json.TokenType);
            Assert.AreEqual(66, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 11; i++)
            {
                // start object, property, start object, property, string value, property, start object, property, start array, number value
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.Number, json.TokenType);
            Assert.AreEqual(3, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(3, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.Number, json.TokenType);
            Assert.AreEqual(61, json.BytesConsumed);
        }
    }

    [TestMethod]
    public void SkipTestEmpty()
    {
        string jsonString = @"{""nestedArray"": {""empty"": [],""empty"": [{}]}}";

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 4; i++)
            {
                // start object, property, start object, property
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.PropertyName, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndArray, json.TokenType);
            Assert.AreEqual(28, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 5; i++)
            {
                // start object, property, start object, property, start array
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.StartArray, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndArray, json.TokenType);
            Assert.AreEqual(28, json.BytesConsumed);
        }

        {
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
            for (int i = 0; i < 7; i++)
            {
                // start object, property, start object, property, start array, end array, property name
                Assert.IsTrue(json.Read());
            }
            Assert.AreEqual(JsonTokenType.PropertyName, json.TokenType);
            Assert.AreEqual(2, json.CurrentDepth);
            json.Skip();
            Assert.AreEqual(2, json.CurrentDepth);
            Assert.AreEqual(JsonTokenType.EndArray, json.TokenType);
            Assert.AreEqual(42, json.BytesConsumed);
        }
    }

    [TestMethod]
    public void StateRecovery()
    {
        ReadOnlySpan<byte> utf8 = "[1]"u8;
        var json = new Utf8JsonReader(utf8, isFinalBlock: false, state: default);

        Assert.AreEqual(0, json.BytesConsumed);
        Assert.AreEqual(0, json.TokenStartIndex);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsFalse(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.IsFalse(json.CurrentState.Options.AllowMultipleValues);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsTrue(json.Read());

        Assert.AreEqual(2, json.BytesConsumed);
        Assert.AreEqual(1, json.TokenStartIndex);
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.Number, json.TokenType);
        Assert.AreEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSpan.SequenceEqual("1"u8));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.IsFalse(json.CurrentState.Options.AllowMultipleValues);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        JsonReaderState state = json.CurrentState;

        json = new Utf8JsonReader(utf8.Slice((int)json.BytesConsumed), isFinalBlock: true, state);

        Assert.AreEqual(0, json.BytesConsumed);    // Not retained
        Assert.AreEqual(0, json.TokenStartIndex);  // Not retained
        Assert.AreEqual(1, json.CurrentDepth);
        Assert.AreEqual(JsonTokenType.Number, json.TokenType);
        Assert.AreEqual(default, json.Position);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.AreEqual(64, json.CurrentState.Options.MaxDepth);
        Assert.IsFalse(json.CurrentState.Options.AllowTrailingCommas);
        Assert.IsFalse(json.CurrentState.Options.AllowMultipleValues);
        Assert.AreEqual(JsonCommentHandling.Disallow, json.CurrentState.Options.CommentHandling);

        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.Read());
    }

    [TestMethod]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, true)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'1' }, false)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, true)]
    [DataRow(new byte[] { 0xEF, 0xBB, 0xBF }, false)]
    public void TestBOMWithSingleJsonValue(byte[] utf8BomAndValue, bool isFinalBlock)
    {
        Assert.Throws<JsonException>(() =>
        {
            var json = new Utf8JsonReader(utf8BomAndValue, isFinalBlock: isFinalBlock, state: default);
            json.Read();
        });
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(32)]
    [DataRow(62)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(66)]
    [DataRow(128)]
    [DataRow(256)]
    [DataRow(512)]
    public void TestDepth(int depth)
    {
        ////if (PlatformDetection.IsMonoInterpreter && depth >= 256)
        ////{
        ////    Assert.Inconclusive("Takes very long to run on interpreter."); return;
        ////}

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            for (int i = 0; i < depth; i++)
            {
                string jsonStr = JsonTestHelper.WriteDepthObject(i, commentHandling == JsonCommentHandling.Allow);
                Span<byte> data = Encoding.UTF8.GetBytes(jsonStr);

                var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = depth });
                var json = new Utf8JsonReader(data, isFinalBlock: true, state);

                int actualDepth = 0;
                while (json.Read())
                {
                    if (json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null)
                    {
                        actualDepth = json.CurrentDepth;
                    }
                }

                int expectedDepth = 0;
                var newtonJson = new JsonTextReader(new StringReader(jsonStr))
                {
                    MaxDepth = depth
                };
                while (newtonJson.Read())
                {
                    if (newtonJson.TokenType == JsonToken.String)
                    {
                        expectedDepth = newtonJson.Depth;
                    }
                }

                Assert.AreEqual(expectedDepth, actualDepth);
                Assert.AreEqual(i + 1, actualDepth);
            }
        }

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            for (int i = 0; i < depth; i++)
            {
                string jsonStr = JsonTestHelper.WriteDepthObject(i, commentHandling == JsonCommentHandling.Allow);
                Span<byte> data = Encoding.UTF8.GetBytes(jsonStr);

                var json = new Utf8JsonReader(data, new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = depth });

                int actualDepth = 0;
                while (json.Read())
                {
                    if (json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null)
                    {
                        actualDepth = json.CurrentDepth;
                    }
                }

                int expectedDepth = 0;
                var newtonJson = new JsonTextReader(new StringReader(jsonStr))
                {
                    MaxDepth = depth
                };
                while (newtonJson.Read())
                {
                    if (newtonJson.TokenType == JsonToken.String)
                    {
                        expectedDepth = newtonJson.Depth;
                    }
                }

                Assert.AreEqual(expectedDepth, actualDepth);
                Assert.AreEqual(i + 1, actualDepth);
            }
        }
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(32)]
    [DataRow(62)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(66)]
    [DataRow(128)]
    [DataRow(256)]
    [DataRow(512)]
    public void TestDepthBeyondLimit(int depth)
    {
        string jsonStr = JsonTestHelper.WriteDepthObject(depth - 1);
        Span<byte> data = Encoding.UTF8.GetBytes(jsonStr);

        var state = new JsonReaderState(new JsonReaderOptions { MaxDepth = depth - 1 });
        var json = new Utf8JsonReader(data, isFinalBlock: true, state);

        try
        {
            int maxDepth = 0;
            while (json.Read())
            {
                if (maxDepth < json.CurrentDepth)
                    maxDepth = json.CurrentDepth;
            }
            Assert.Fail($"Expected JsonException was not thrown. Max depth allowed = {json.CurrentState.Options.MaxDepth} | Max depth reached = {maxDepth}");
        }
        catch (JsonException)
        { }

        jsonStr = JsonTestHelper.WriteDepthArray(depth - 1);
        data = Encoding.UTF8.GetBytes(jsonStr);

        state = new JsonReaderState(new JsonReaderOptions { MaxDepth = depth - 1 });
        json = new Utf8JsonReader(data, isFinalBlock: true, state);

        try
        {
            int maxDepth = 0;
            while (json.Read())
            {
                if (maxDepth < json.CurrentDepth)
                    maxDepth = json.CurrentDepth;
            }
            Assert.Fail($"Expected JsonException was not thrown. Max depth allowed = {json.CurrentState.Options.MaxDepth} | Max depth reached = {maxDepth}");
        }
        catch (JsonException)
        { }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(32)]
    [DataRow(62)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(66)]
    [DataRow(128)]
    [DataRow(256)]
    [DataRow(512)]
    public void TestDepthWithObjectArrayMismatch(int depth)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            for (int i = 0; i < depth; i++)
            {
                string jsonStr = JsonTestHelper.WriteDepthObjectWithArray(i, commentHandling == JsonCommentHandling.Allow);
                Span<byte> data = Encoding.UTF8.GetBytes(jsonStr);

                var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling, MaxDepth = depth + 1 });
                var json = new Utf8JsonReader(data, isFinalBlock: true, state);

                int actualDepth = 0;
                while (json.Read())
                {
                    if (json.TokenType >= JsonTokenType.String && json.TokenType <= JsonTokenType.Null)
                        actualDepth = json.CurrentDepth;
                }

                int expectedDepth = 0;
                var newtonJson = new JsonTextReader(new StringReader(jsonStr))
                {
                    MaxDepth = depth + 1
                };
                while (newtonJson.Read())
                {
                    if (newtonJson.TokenType == JsonToken.String)
                    {
                        expectedDepth = newtonJson.Depth;
                    }
                }

                Assert.AreEqual(expectedDepth, actualDepth);
                Assert.AreEqual(i + 2, actualDepth);
            }
        }
    }

    // TestCaseType is only used to give the json strings a descriptive name.
    [TestMethod]
    [DynamicData(nameof(TestCases))]
    public void TestJsonReaderUtf8(bool compactData, TestCaseType type, string jsonString)
    {
        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        SpanSequenceStatesAreEqual(dataUtf8);

        byte[] result = JsonTestHelper.ReturnBytesHelper(dataUtf8, out int length);
        string actualStr = Encoding.UTF8.GetString(result, 0, length);

        byte[] resultSequence = JsonTestHelper.SequenceReturnBytesHelper(dataUtf8, out length);
        string actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

        Stream stream = new MemoryStream(dataUtf8);
        TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        string expectedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

        Assert.AreEqual(expectedStr, actualStr);
        Assert.AreEqual(expectedStr, actualStrSequence);

        // Json payload contains numbers that are too large for .NET (need BigInteger+)
        if (type != TestCaseType.FullSchema1 && type != TestCaseType.BasicLargeNum)
        {
            object jsonValues = JsonTestHelper.ReturnObjectHelper(dataUtf8);
            string str = JsonTestHelper.ObjectToString(jsonValues);
            ReadOnlySpan<char> expectedSpan = expectedStr.AsSpan(0, expectedStr.Length - 2);
            ReadOnlySpan<char> actualSpan = str.AsSpan(0, str.Length - 2);
            Assert.IsTrue(expectedSpan.SequenceEqual(actualSpan));
        }

        result = JsonTestHelper.ReturnBytesHelper(dataUtf8, out length, JsonCommentHandling.Skip);
        actualStr = Encoding.UTF8.GetString(result, 0, length);
        resultSequence = JsonTestHelper.SequenceReturnBytesHelper(dataUtf8, out length, JsonCommentHandling.Skip);
        actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

        Assert.AreEqual(expectedStr, actualStr);
        Assert.AreEqual(expectedStr, actualStrSequence);

        result = JsonTestHelper.ReturnBytesHelper(dataUtf8, out length, JsonCommentHandling.Allow);
        actualStr = Encoding.UTF8.GetString(result, 0, length);
        resultSequence = JsonTestHelper.SequenceReturnBytesHelper(dataUtf8, out length, JsonCommentHandling.Allow);
        actualStrSequence = Encoding.UTF8.GetString(resultSequence, 0, length);

        Assert.AreEqual(expectedStr, actualStr);
        Assert.AreEqual(expectedStr, actualStrSequence);
    }

    [TestMethod]
    [DataRow("{\"nam\\\"e\":\"ah\\\"son\"}", "nam\\\"e, ah\\\"son, ", "nam\"e, ah\"son, ")]
    [DataRow("{\"Here is a string: \\\"\\\"\":\"Here is a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str\":\"\\\"\\\"\"}",
        "Here is a string: \\\"\\\", Here is a, Here is a back slash\\\\, Multiline\\r\\n String\\r\\n, \\tMul\\r\\ntiline String, \\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\, str, \\\"\\\", ",
        "Here is a string: \"\", Here is a, Here is a back slash\\, Multiline\r\n String\r\n, \tMul\r\ntiline String, \"somequote\"\tMu\"\"l\r\ntiline\"another\" String\\, str, \"\", ")]
    public void TestJsonReaderUtf8SpecialString(string jsonString, string expectedStr, string expectedEscapedStr)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
            byte[] result = JsonTestHelper.ReturnBytesHelper(dataUtf8, out int length, commentHandling);
            string actualStr = Encoding.UTF8.GetString(result, 0, length);

            Assert.AreEqual(expectedStr, actualStr);

            result = JsonTestHelper.SequenceReturnBytesHelper(dataUtf8, out length, commentHandling);
            actualStr = Encoding.UTF8.GetString(result, 0, length);

            Assert.AreEqual(expectedStr, actualStr);

            object jsonValues = JsonTestHelper.ReturnObjectHelper(dataUtf8, commentHandling);
            string str = JsonTestHelper.ObjectToString(jsonValues);
            Assert.AreEqual(expectedEscapedStr, str);

            Stream stream = new MemoryStream(dataUtf8);
            TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
            expectedEscapedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);
            Assert.AreEqual(expectedEscapedStr, str);
        }
    }

    [TestMethod]
    // Skipping large JSON since slicing them (O(n^2)) is too slow.
    [DynamicData(nameof(SmallTestCases))]
    public void TestPartialJsonReader(bool compactData, TestCaseType type, string jsonString)
    {
        _ = type;

        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        _ = JsonTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength);
        byte[] outputArray = new byte[outputLength];
        Span<byte> outputSpan = outputArray;

        Stream stream = new MemoryStream(dataUtf8);
        TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        string expectedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            byte[] output = JsonTestHelper.ReaderLoop(outputSpan.Length, out int firstLength, ref json);
            output.AsSpan(0, firstLength).CopyTo(outputSpan);
            long consumed = json.BytesConsumed;
            Assert.AreEqual(default, json.Position);

            for (long j = consumed; j < dataUtf8.Length - consumed; j++)
            {
                // Need to re-initialize the state and reader to avoid using the previous state stack.
                state = default;
                json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                while (json.Read())
                    ;

                JsonReaderState jsonState = json.CurrentState;

                int written = firstLength;
                json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed, (int)j), isFinalBlock: false, jsonState);
                output = JsonTestHelper.ReaderLoop(outputSpan.Length - written, out int length, ref json);
                output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                written += length;

                long consumedInner = json.BytesConsumed;
                json = new Utf8JsonReader(dataUtf8.AsSpan((int)(consumed + consumedInner)), isFinalBlock: true, json.CurrentState);
                output = JsonTestHelper.ReaderLoop(outputSpan.Length - written, out length, ref json);
                output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                written += length;
                Assert.AreEqual(dataUtf8.Length - consumedInner - consumed, json.BytesConsumed);
                Assert.AreEqual(default, json.Position);

                Assert.AreEqual(outputSpan.Length, written);
                string actualStr = Encoding.UTF8.GetString(outputArray);
                Assert.AreEqual(expectedStr, actualStr);
            }
        }
    }

    [TestMethod]
    [TestCategory("outerloop")]
    [DynamicData(nameof(SpecialNumTestCases))]
    public void TestPartialJsonReaderSlicesSpecialNumbers(TestCaseType type, string jsonString)
    {
        _ = type;
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
                var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                while (json.Read())
                    ;

                long consumed = json.BytesConsumed;

                for (long j = consumed; j < dataUtf8.Length - consumed; j++)
                {
                    // Need to re-initialize the state and reader to avoid using the previous state stack.
                    state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
                    json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                    while (json.Read())
                        ;

                    JsonReaderState jsonState = json.CurrentState;

                    json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed, (int)j), isFinalBlock: false, jsonState);
                    while (json.Read())
                        ;

                    long consumedInner = json.BytesConsumed;
                    json = new Utf8JsonReader(dataUtf8.AsSpan((int)(consumed + consumedInner)), isFinalBlock: true, json.CurrentState);
                    while (json.Read())
                        ;
                    Assert.AreEqual(dataUtf8.Length - consumedInner - consumed, json.BytesConsumed);
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(SpecialNumTestCases))]
    public void TestPartialJsonReaderSpecialNumbers(TestCaseType type, string jsonString)
    {
        _ = type;
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            for (int i = 0; i < dataUtf8.Length; i++)
            {
                var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
                var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                while (json.Read())
                    ;
            }
        }
    }

    [TestMethod]
    // Pad depth by nested objects, but minimize the text
    [DataRow(1, true, true)]
    [DataRow(2, true, true)]
    [DataRow(3, true, true)]
    [DataRow(16, true, true)]
    [DataRow(32, true, true)]
    [DataRow(60, true, true)]
    [DataRow(61, true, true)]
    [DataRow(62, true, true)]
    [DataRow(63, true, true)]
    [DataRow(64, true, true)]
    [DataRow(65, true, true)]
    [DataRow(66, true, true)]
    [DataRow(67, true, true)]
    [DataRow(68, true, true)]
    [DataRow(123, true, true)]
    [DataRow(124, true, true)]
    [DataRow(125, true, true)]

    // Pad depth by nested arrays, but minimize the text
    [DataRow(1, false, true)]
    [DataRow(2, false, true)]
    [DataRow(3, false, true)]
    [DataRow(16, false, true)]
    [DataRow(32, false, true)]
    [DataRow(60, false, true)]
    [DataRow(61, false, true)]
    [DataRow(62, false, true)]
    [DataRow(63, false, true)]
    [DataRow(64, false, true)]
    [DataRow(65, false, true)]
    [DataRow(66, false, true)]
    [DataRow(67, false, true)]
    [DataRow(68, false, true)]
    [DataRow(123, false, true)]
    [DataRow(124, false, true)]
    [DataRow(125, false, true)]

    // Pad depth by nested arrays, but keep the text formatted
    [DataRow(1, false, false)]
    [DataRow(2, false, false)]
    [DataRow(3, false, false)]
    [DataRow(16, false, false)]
    [DataRow(32, false, false)]
    [DataRow(60, false, false)]
    [DataRow(61, false, false)]
    [DataRow(62, false, false)]
    [DataRow(63, false, false)]
    [DataRow(64, false, false)]
    [DataRow(65, false, false)]
    [DataRow(66, false, false)]
    [DataRow(67, false, false)]
    [DataRow(68, false, false)]
    [DataRow(123, false, false)]
    [DataRow(124, false, false)]
    [DataRow(125, false, false)]

    // Pad depth by nested objects, but keep the text formatted
    [DataRow(1, true, false)]
    [DataRow(2, true, false)]
    [DataRow(3, true, false)]
    [DataRow(16, true, false)]
    [DataRow(32, true, false)]
    [DataRow(60, true, false)]
    [DataRow(61, true, false)]
    [DataRow(62, true, false)]
    [DataRow(63, true, false)]
    [DataRow(64, true, false)]
    [DataRow(65, true, false)]
    [DataRow(66, true, false)]
    [DataRow(67, true, false)]
    [DataRow(68, true, false)]
    [DataRow(123, true, false)]
    [DataRow(124, true, false)]
    [DataRow(125, true, false)]
    public void TestPartialJsonReaderWithDepthPadding(int depthPadding, bool padByObject, bool compactData)
    {
        var builderPrefix = new StringBuilder();
        var builderSuffix = new StringBuilder();

        if (padByObject)
        {
            for (int i = 0; i < depthPadding; i++)
            {
                builderPrefix.Append($"{{\n \"property{i}\": ");
                builderSuffix.Append("\n}");
            }
        }
        else
        {
            for (int i = 0; i < depthPadding; i++)
            {
                builderPrefix.Append("[\n");
                builderSuffix.Append("\n]");
            }
        }

        string jsonString = builderPrefix.ToString() + SR.FullJsonSchema1 + builderSuffix.ToString();

        // Remove all formatting/indendation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        // Set the max depth sufficiently large to account for the depth padding.
        byte[] result = JsonTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength, maxDepth: 256);
        _ = new byte[outputLength];
        string actualStr = Encoding.UTF8.GetString(result, 0, outputLength);

        Stream stream = new MemoryStream(dataUtf8);
        TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        string expectedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

        Assert.AreEqual(expectedStr, actualStr);
    }

    [TestMethod]
    [DynamicData(nameof(LargeTestCases))]
    public void TestPartialLargeJsonReader(bool compactData, TestCaseType type, string jsonString)
    {
        // Skipping really large JSON since slicing them (O(n^2)) is too slow.
        if (type == TestCaseType.Json40KB || type == TestCaseType.Json400KB || type == TestCaseType.ProjectLockJson)
        {
            return;
        }

        // Remove all formatting/indentation
        if (compactData)
        {
            jsonString = JsonTestHelper.GetCompactString(jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        _ = JsonTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength);
        byte[] outputArray = new byte[outputLength];
        Span<byte> outputSpan = outputArray;

        Stream stream = new MemoryStream(dataUtf8);
        TextReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
        string expectedStr = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            byte[] output = JsonTestHelper.ReaderLoop(outputSpan.Length, out int firstLength, ref json);
            output.AsSpan(0, firstLength).CopyTo(outputSpan);
            int written = firstLength;

            long consumed = json.BytesConsumed;
            Assert.AreEqual(default, json.Position);

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, json.CurrentState);
            output = JsonTestHelper.ReaderLoop(outputSpan.Length - written, out int length, ref json);
            output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
            written += length;
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
            Assert.AreEqual(default, json.Position);

            Assert.AreEqual(outputSpan.Length, written);
            string actualStr = Encoding.UTF8.GetString(outputArray);
            Assert.AreEqual(expectedStr, actualStr);
        }
    }

    [TestMethod]
    public void TestSingleStrings()
    {
        string jsonString = "\"Hello, \\u0041hson!\"";
        string expectedString = "Hello, \\u0041hson!, ";

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        _ = JsonTestHelper.ReturnBytesHelper(dataUtf8, out int outputLength);
        byte[] outputArray = new byte[outputLength];
        Span<byte> outputSpan = outputArray;

        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            byte[] output = JsonTestHelper.ReaderLoop(outputSpan.Length, out int firstLength, ref json);
            output.AsSpan(0, firstLength).CopyTo(outputSpan);
            long consumed = json.BytesConsumed;
            Assert.AreEqual(default, json.Position);
            Assert.AreEqual(0, json.TokenStartIndex);

            for (long j = consumed; j < dataUtf8.Length - consumed; j++)
            {
                // Need to re-initialize the state and reader to avoid using the previous state stack.
                state = default;
                json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
                while (json.Read())
                    ;

                JsonReaderState jsonState = json.CurrentState;

                int written = firstLength;
                json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed, (int)j), isFinalBlock: false, jsonState);
                output = JsonTestHelper.ReaderLoop(outputSpan.Length - written, out int length, ref json);
                output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                written += length;

                long consumedInner = json.BytesConsumed;
                json = new Utf8JsonReader(dataUtf8.AsSpan((int)(consumed + consumedInner)), isFinalBlock: true, json.CurrentState);
                output = JsonTestHelper.ReaderLoop(outputSpan.Length - written, out length, ref json);
                output.AsSpan(0, length).CopyTo(outputSpan.Slice(written));
                written += length;
                Assert.AreEqual(dataUtf8.Length - consumedInner - consumed, json.BytesConsumed);
                Assert.AreEqual(default, json.Position);
                Assert.AreEqual(0, json.TokenStartIndex);

                Assert.AreEqual(outputSpan.Length, written);
                string actualStr = Encoding.UTF8.GetString(outputArray);
                Assert.AreEqual(expectedString, actualStr);
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(TrySkipValues))]
    public void TestSkipPartial(string jsonString, JsonTokenType lastToken)
    {
        _ = lastToken;
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: false, default);
        try
        {
            Assert.IsFalse(json.IsFinalBlock);
            json.Skip();
            Assert.Fail("Expected InvalidOperationException was not thrown when calling Skip with isFinalBlock = false, even if whole payload is available.");
        }
        catch (InvalidOperationException) { }

        // Skip, then skip some more
        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);

            try
            {
                Assert.IsFalse(json.IsFinalBlock);
                json.Skip();
                Assert.Fail("Expected InvalidOperationException was not thrown when calling Skip with isFinalBlock = false");
            }
            catch (InvalidOperationException) { }

            long bytesConsumed = json.BytesConsumed;
            while (json.TrySkip())
            {
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }

            long consumed = json.BytesConsumed;

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, json.CurrentState);
            while (true)
            {
                Assert.IsTrue(json.IsFinalBlock);
                json.Skip();
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
        }

        // Read, then skip
        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            while (json.Read())
                ;

            long consumed = json.BytesConsumed;

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, json.CurrentState);
            long bytesConsumed = json.BytesConsumed;
            while (true)
            {
                Assert.IsTrue(json.IsFinalBlock);
                json.Skip();
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ComplexArrayJsonTokenStartIndex))]
    public void TestTokenStartIndex_ComplexArrayValue(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ComplexObjectSeveralJsonTokenStartIndex))]
    public void TestTokenStartIndex_ComplexObjectManyValues(string jsonString, int expectedIndexProperty, int expectedIndexValue)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexProperty, reader.TokenStartIndex);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexValue, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
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
    public void TestTokenStartIndex_ComplexObjectValue(string jsonString, int expectedIndexProperty, int expectedIndexValue)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexProperty, reader.TokenStartIndex);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndexValue, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
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
    public void TestTokenStartIndex_SingleValue(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(SingleJsonWithCommentsAllowTokenStartIndex))]
    public void TestTokenStartIndex_SingleValueCommentsAllow(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = false });
        var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

        state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(expectedIndex, reader.TokenStartIndex);
    }

    [TestMethod]
    [DynamicData(nameof(SingleJsonWithCommentsTokenStartIndex))]
    public void TestTokenStartIndex_SingleValueWithComments(string jsonString, int expectedIndex)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = false });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            Assert.IsTrue(reader.Read());
            if (commentHandling == JsonCommentHandling.Allow)
            {
                Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
                Assert.IsTrue(reader.Read());
            }
            Assert.AreEqual(expectedIndex, reader.TokenStartIndex);

            state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
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
    public void TestTokenStartIndex_WithTrailingCommas(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            while (reader.Read())
            { }

            Assert.AreEqual(utf8.Length - 1, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonWithValidTrailingCommasAndComments))]
    public void TestTokenStartIndex_WithTrailingCommasAndComments(string jsonString)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            if (commentHandling == JsonCommentHandling.Disallow)
            {
                continue;
            }

            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling, AllowTrailingCommas = true });
            var reader = new Utf8JsonReader(utf8, isFinalBlock: true, state);
            while (reader.Read())
            { }

            Assert.AreEqual(utf8.Length - 1, reader.TokenStartIndex);
        }
    }

    [TestMethod]
    [DynamicData(nameof(TrySkipValues))]
    public void TestTrySkip(string jsonString, JsonTokenType lastToken)
    {
        List<JsonTokenType> expectedTokenTypes = JsonTestHelper.GetTokenTypes(jsonString);
        TrySkipHelper(jsonString, lastToken, expectedTokenTypes, JsonCommentHandling.Disallow);
        TrySkipHelper(jsonString, lastToken, expectedTokenTypes, JsonCommentHandling.Skip);
        TrySkipHelper(jsonString, lastToken, expectedTokenTypes, JsonCommentHandling.Allow);
    }

    [TestMethod]
    [DynamicData(nameof(TrySkipValues))]
    public void TestTrySkipPartial(string jsonString, JsonTokenType lastToken)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        // Skip, then skip some more
        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);

            long bytesConsumed = json.BytesConsumed;
            while (json.TrySkip())
            {
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }

            ValidateNextTrySkip(ref json);

            long consumed = json.BytesConsumed;

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, json.CurrentState);
            while (json.TrySkip())
            {
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
        }

        // Read, then skip
        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            while (json.Read())
                ;

            ValidateNextTrySkip(ref json);

            long consumed = json.BytesConsumed;

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, json.CurrentState);
            long bytesConsumed = json.BytesConsumed;
            while (json.TrySkip())
            {
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
        }

        // Skip, then read
        for (int i = 0; i < dataUtf8.Length; i++)
        {
            JsonReaderState state = default;
            var json = new Utf8JsonReader(dataUtf8.AsSpan(0, i), isFinalBlock: false, state);
            long bytesConsumed = json.BytesConsumed;
            while (json.TrySkip())
            {
                Assert.IsTrue(json.TokenType != JsonTokenType.PropertyName && json.TokenType != JsonTokenType.StartObject && json.TokenType != JsonTokenType.StartArray);
                if (bytesConsumed == json.BytesConsumed)
                {
                    if (!json.Read())
                    {
                        break;
                    }
                }
                else
                {
                    bytesConsumed = json.BytesConsumed;
                }
            }

            ValidateNextTrySkip(ref json);

            long consumed = json.BytesConsumed;

            json = new Utf8JsonReader(dataUtf8.AsSpan((int)consumed), isFinalBlock: true, json.CurrentState);
            while (json.Read())
                ;
            Assert.AreEqual(dataUtf8.Length - consumed, json.BytesConsumed);
            Assert.AreEqual(lastToken, json.TokenType);
        }
    }

    [TestMethod]
    [DynamicData(nameof(TrySkipValues))]
    public void TestTrySkipWithComments(string jsonString, JsonTokenType lastToken)
    {
        List<JsonTokenType> expectedTokenTypesWithoutComments = JsonTestHelper.GetTokenTypes(jsonString);

        jsonString = JsonTestHelper.InsertCommentsEverywhere(jsonString);

        List<JsonTokenType> expectedTokenTypes = JsonTestHelper.GetTokenTypes(jsonString);
        TrySkipHelper(jsonString, JsonTokenType.Comment, expectedTokenTypes, JsonCommentHandling.Allow);
        TrySkipHelper(jsonString, lastToken, expectedTokenTypesWithoutComments, JsonCommentHandling.Skip);
    }

    [TestMethod]
    public void TrySkipOnValuePartialWithWhiteSpace()
    {
        string jsonString = "[ 1, ";

        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: true, state: default);
            reader.Read();
            reader.Read();
            Assert.AreEqual(3, reader.BytesConsumed);
            Assert.IsTrue(reader.TrySkip());
            Assert.AreEqual(3, reader.BytesConsumed);

            try
            {
                reader.Read();
                Assert.Fail("Expected JsonException was not thrown for incomplete JSON payload when reading.");
            }
            catch (JsonException) { }

            Assert.AreEqual(5, reader.BytesConsumed);  // After exception, state is not restored.
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: true, state: default);
            reader.Read();
            Assert.AreEqual(1, reader.BytesConsumed);

            try
            {
                reader.TrySkip();
                Assert.Fail("Expected JsonException was not thrown for incomplete JSON payload when skipping.");
            }
            catch (JsonException) { }

            Assert.AreEqual(5, reader.BytesConsumed);  // After exception, state is not restored.
        }
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(jsonString), isFinalBlock: false, state: default);
            reader.Read();
            Assert.AreEqual(1, reader.BytesConsumed);
            Assert.IsFalse(reader.TrySkip());
            Assert.AreEqual(1, reader.BytesConsumed);
        }
    }

    [TestMethod]
    public void TrySkipPartialAndContinueWithWhiteSpace()
    {
        string jsonString = "[ 1, 2]";
        byte[] utf8Data = Encoding.UTF8.GetBytes(jsonString);

        // "[ 1, "
        var reader = new Utf8JsonReader(utf8Data.AsSpan(0, 5), isFinalBlock: false, state: default);
        reader.Read();
        Assert.AreEqual(1, reader.BytesConsumed);
        Assert.IsFalse(reader.TrySkip());
        Assert.AreEqual(1, reader.BytesConsumed);

        // " 1, 2]"
        int previousConsumed = (int)reader.BytesConsumed;
        reader = new Utf8JsonReader(utf8Data.AsSpan(previousConsumed), isFinalBlock: true, reader.CurrentState);
        Assert.IsTrue(reader.TrySkip());
        Assert.AreEqual(utf8Data.Length - previousConsumed, reader.BytesConsumed);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("false")]
    [DataRow("true")]
    [DataRow("42")]
    [DataRow("\"stringWithoutEscaping\"")]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("/* comment */ null", JsonCommentHandling.Allow)]
    public void ValueIsEscaped_AfterEscapedPropertyNames_IsFalseOnNonEscapedTokens(string jsonValue, JsonCommentHandling commentHandling = default)
    {
        string json = $@"{{ ""hello\n"" : {jsonValue} }}";
        var options = new JsonReaderOptions { CommentHandling = commentHandling };
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test, options);

        static void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
            Assert.IsTrue(reader.ValueIsEscaped);

            while (reader.Read())
            {
                Assert.IsFalse(reader.ValueIsEscaped);
            }
        }
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("false")]
    [DataRow("true")]
    [DataRow("42")]
    [DataRow("\"stringWithoutEscaping\"")]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("/* comment */ null", JsonCommentHandling.Allow)]
    public void ValueIsEscaped_AfterEscapedStrings_IsFalseOnNonEscapedTokens(string jsonValue, JsonCommentHandling commentHandling = default)
    {
        string json = $@"[""hello\n"", {jsonValue}]";
        var options = new JsonReaderOptions { CommentHandling = commentHandling };
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test, options);

        static void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.String, reader.TokenType);
            Assert.IsTrue(reader.ValueIsEscaped);

            while (reader.Read())
            {
                Assert.IsFalse(reader.ValueIsEscaped);
            }
        }
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("false")]
    [DataRow("true")]
    [DataRow("42")]
    [DataRow("\"stringWithoutEscaping\"")]
    [DataRow("{}")]
    [DataRow("[]")]
    [DataRow("/* comment */ null", JsonCommentHandling.Allow)]
    public void ValueIsEscaped_IsFalseOnTokensWithoutEscaping(string json, JsonCommentHandling commentHandling = default)
    {
        var options = new JsonReaderOptions { CommentHandling = commentHandling };
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test, options);

        static void Test(ref Utf8JsonReader reader)
        {
            while (reader.Read())
            {
                Assert.IsFalse(reader.ValueIsEscaped);
            }
        }
    }

    [TestMethod]
    [DataRow(@"\""")]
    [DataRow(@"\n")]
    [DataRow(@"\r")]
    [DataRow(@"\\")]
    [DataRow(@"\/")]
    [DataRow(@"\t")]
    [DataRow(@"\b")]
    [DataRow(@"\f")]
    [DataRow(@"\u6F22\u5B57")]
    public void ValueIsEscaped_IsTrueOnEscapedPropertyNames(string jsonString)
    {
        string json = $@"{{ ""{jsonString}"" : false }}";
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test);

        static void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
            Assert.IsTrue(reader.ValueIsEscaped);
            Assert.IsTrue(System.Linq.Enumerable.Contains(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray(), (byte)'\\'));
        }
    }

    [TestMethod]
    [DataRow(@"\""")]
    [DataRow(@"\n")]
    [DataRow(@"\r")]
    [DataRow(@"\\")]
    [DataRow(@"\/")]
    [DataRow(@"\t")]
    [DataRow(@"\b")]
    [DataRow(@"\f")]
    [DataRow(@"\u6F22\u5B57")]
    public void ValueIsEscaped_IsTrueOnEscapedStrings(string jsonString)
    {
        string json = $@"""{jsonString}""";
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test);

        static void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonTokenType.String, reader.TokenType);
            Assert.IsTrue(reader.ValueIsEscaped);
            Assert.IsTrue(System.Linq.Enumerable.Contains(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray(), (byte)'\\'));
        }
    }

    private static void PartialReaderLoop(byte[] utf8, JsonReaderState state)
    {
        for (int i = 0; i < utf8.Length; i++)
        {
            JsonReaderState stateCopy = state;
            PartialReaderLoop(utf8, stateCopy, i);
        }
    }

    private static void PartialReaderLoop(byte[] utf8, JsonReaderState state, int splitLocation)
    {
        var reader = new Utf8JsonReader(utf8.AsSpan(0, splitLocation), isFinalBlock: false, state);
        while (reader.Read())
            ;

        long consumed = reader.BytesConsumed;
        reader = new Utf8JsonReader(utf8.AsSpan((int)consumed), isFinalBlock: true, reader.CurrentState);
        while (reader.Read())
            ;
    }

    private static void TestGetMethodsOnDefault()
    {
        Utf8JsonReader json = default;

        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetByte(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetByte());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetComment());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetDateTime(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetDateTime());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetDateTimeOffset(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetDateTimeOffset());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetDecimal(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetDecimal());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetDouble(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetDouble());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetInt16(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetInt16());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetInt32(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetInt32());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetInt64(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetInt64());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetSByte(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetSByte());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetSingle(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetSingle());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetUInt16(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetUInt16());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetUInt32(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetUInt32());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetUInt64(out _));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetUInt64());

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetString());
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.CopyString(new byte[16]));
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.CopyString(new char[16]));

        json = default;
        JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetBoolean());
    }

    private static void TestReadTokenWithExtra(byte[] utf8, JsonCommentHandling commentHandling, bool isFinalBlock, bool commentsAppended = false)
    {
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
        var reader = new Utf8JsonReader(utf8, isFinalBlock, state);

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

    private static void TrailingCommasHelper(byte[] utf8, JsonReaderState state, bool allow, bool expectThrow)
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

    private static void TrailingCommasHelperPartial(byte[] utf8, JsonReaderState state, bool expectThrow)
    {
        if (expectThrow)
        {
            Assert.Throws<JsonException>(() => PartialReaderLoop(utf8, state));
        }
        else
        {
            PartialReaderLoop(utf8, state);
        }
    }

    private static void TrySkipHelper(string jsonString, JsonTokenType lastToken, List<JsonTokenType> expectedTokenTypes, JsonCommentHandling commentHandling)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);
        var state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        JsonReaderState previous = json.CurrentState;
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(0, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        Assert.IsTrue(json.TrySkip());

        JsonReaderState current = json.CurrentState;
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(previous, current);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(0, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        json.Skip();

        current = json.CurrentState;
        Assert.AreEqual(JsonTokenType.None, json.TokenType);
        Assert.AreEqual(previous, current);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(0, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsTrue(json.IsFinalBlock);
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
        Assert.IsTrue(json.IsFinalBlock);
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
        Assert.IsTrue(json.IsFinalBlock);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(default));
        Assert.IsTrue(json.ValueSequence.IsEmpty);

        json.Skip();

        current = json.CurrentState;
        Assert.AreEqual(previous, current);
        Assert.AreEqual(lastToken, json.TokenType);
        Assert.AreEqual(0, json.CurrentDepth);
        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.IsFinalBlock);
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
            Assert.IsTrue(expectedTokenTypes[i] == json.TokenType, $"Expected: {expectedTokenTypes[i]}, Actual: {json.TokenType}, Index: {i}, BytesConsumed: {json.BytesConsumed}");
        }

        for (int i = 0; i < totalReads; i++)
        {
            state = new JsonReaderState(new JsonReaderOptions { CommentHandling = commentHandling });
            json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
            for (int j = 0; j < i; j++)
            {
                Assert.IsTrue(json.Read());
            }
            json.Skip();
            Assert.IsTrue(expectedTokenTypes[i] == json.TokenType, $"Expected: {expectedTokenTypes[i]}, Actual: {json.TokenType}, Index: {i}, BytesConsumed: {json.BytesConsumed}");
        }
    }

    private static void ValidateNextTrySkip(ref Utf8JsonReader json)
    {
        JsonReaderState previous = json.CurrentState;
        JsonTokenType prevTokenType = json.TokenType;
        int prevDepth = json.CurrentDepth;
        long prevConsumed = json.BytesConsumed;
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSequence.IsEmpty);
        ReadOnlySpan<byte> prevValue = json.ValueSpan;

        if (json.TokenType == JsonTokenType.PropertyName || json.TokenType == JsonTokenType.StartObject || json.TokenType == JsonTokenType.StartArray)
        {
            Assert.IsFalse(json.TrySkip());
        }
        else
        {
            Assert.IsTrue(json.TrySkip());
        }

        JsonReaderState current = json.CurrentState;
        Assert.AreEqual(previous, current);
        Assert.AreEqual(prevTokenType, json.TokenType);
        Assert.AreEqual(prevDepth, json.CurrentDepth);
        Assert.AreEqual(prevConsumed, json.BytesConsumed);
        Assert.IsFalse(json.HasValueSequence);
        Assert.IsFalse(json.ValueIsEscaped);
        Assert.IsTrue(json.ValueSequence.IsEmpty);
        Assert.IsTrue(json.ValueSpan.SequenceEqual(prevValue));
    }

    private static void VerifyReadLoop(ref Utf8JsonReader json, string expected)
    {
        while (json.Read())
        {
            switch (json.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.EndObject:
                    break;

                case JsonTokenType.Comment:
                    if (expected != null)
                    {
                        string actualComment = json.GetComment();
                        Assert.AreEqual(expected, actualComment);
                    }
                    else
                    {
                        Assert.Fail();
                    }
                    break;

                default:
                    Assert.Fail();
                    break;
            }
        }
    }
    [TestMethod]
    public void SkipComment_SingleLineComment()
    {
        byte[] data = "// This is a comment\n{}"u8.ToArray();
        var reader = new Utf8JsonReader(data, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
    }

    [TestMethod]
    public void SkipComment_MultiLineComment()
    {
        byte[] data = "/* This is a\nmultiline comment */\n{}"u8.ToArray();
        var reader = new Utf8JsonReader(data, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
    }

    [TestMethod]
    public void AllowComment_ReadsCommentToken()
    {
        byte[] data = "// comment\n{}"u8.ToArray();
        var reader = new Utf8JsonReader(data, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
        Assert.AreEqual(" comment", reader.GetComment());

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
    }
}