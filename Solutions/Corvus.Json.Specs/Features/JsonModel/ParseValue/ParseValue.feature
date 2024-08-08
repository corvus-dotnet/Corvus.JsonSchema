Feature: ParseValue
	Parse a string into a JSON value. 

Scenario Outline: Parse a UTF8 span into a string
	When the utf8 span '<utf8Value>' is parsed into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| utf8Value                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |

Scenario Outline: Parse a char span into a string
	When the char span '<charValue>' is parsed into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| charValue                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| "Hello"                        | JsonString   | "Hello"                        |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |

Scenario Outline: Parse a UTF8 span into a disposable value
	When the utf8 span '<utf8Value>' is parsed with ParsedValue{T} into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| utf8Value                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |

Scenario Outline: Parse a char span into a disposable value
	When the char span '<charValue>' is parsed with ParsedValue{T} into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| charValue                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |

Scenario Outline: Parse a UTF8 ReadOnlyMemory into a disposable value
	When the utf8 ReadOnlyMemory '<utf8Value>' is parsed with ParsedValue{T} into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| utf8Value                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |

Scenario Outline: Parse a char ReadOnlyMemory into a disposable value
	When the char ReadOnlyMemory '<charValue>' is parsed with ParsedValue{T} into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| charValue                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |

Scenario Outline: Parse a UTF8 stream into a disposable value
	When the utf8 Stream '<utf8Value>' is parsed with ParsedValue{T} into a <jsonTypeName>
	Then the result should be equal to the JsonAny <result>

Examples:
	| utf8Value                      | jsonTypeName | result                         |
	| true                           | JsonBoolean  | true                           |
	| false                          | JsonBoolean  | false                          |
	| null                           | JsonNull     | null                           |
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| 123.4                          | JsonSingle   | 123.4                          |
	| 123.4                          | JsonDouble   | 123.4                          |
	| 123.4                          | JsonDecimal  | 123.4                          |
	| 123                            | JsonByte     | 123                            |
	| 123                            | JsonSByte    | 123                            |
	| 123                            | JsonInt16    | 123                            |
	| 123                            | JsonUInt16   | 123                            |
	| 123                            | JsonInt32    | 123                            |
	| 123                            | JsonUInt32   | 123                            |
	| 123                            | JsonInt64    | 123                            |
	| 123                            | JsonUInt64   | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |