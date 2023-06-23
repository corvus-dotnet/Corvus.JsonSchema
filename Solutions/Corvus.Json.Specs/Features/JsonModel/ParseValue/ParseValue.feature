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
	| 123                            | JsonNumber   | 123                            |
	| 123.4                          | JsonNumber   | 123.4                          |
	| 123                            | JsonInteger  | 123                            |
	| "Hello"                        | JsonString   | "Hello"                        |
	| "Hello"                        | JsonAny      | "Hello"                        |
	| [123,"Hello"]                  | JsonArray    | [123, "Hello"]                 |
	| { "foo": 123, "bar": "Hello" } | JsonObject   | { "foo": 123, "bar": "Hello" } |
