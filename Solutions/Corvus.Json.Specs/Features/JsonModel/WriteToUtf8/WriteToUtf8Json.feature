Feature: Write JSON to a UTF8 JSON writer

Scenario Outline: Dotnet backed element
	Given the dotnet backed <TTarget> <value>
	When I write the value to a UTF8 JSON writer and store the resulting JSON
	Then the JSON should equal a valid instance of the <TTarget>
Examples:
	| TTarget                    | value                                  |
	| JsonString                 | "foo"                                  |
	| JsonNumber                 | 3.1                                    |
	| JsonInteger                | 3                                      |
	| JsonBoolean                | true                                   |
	| JsonBoolean                | false                                  |
	| JsonArray                  | []                                     |
	| JsonArray                  | [1,2,3,"4"]                            |
	| JsonObject                 | {}                                     |
	| JsonObject                 | {"foo":"bar"}                          |
	| JsonDate                   | "1963-06-19"                           |
	| JsonDateTime               | "1990-12-31T15:59:50.123-08:00"        |
	| JsonDuration               | "P4DT12H30M5S"                         |
	| JsonEmail                  | "joe.bloggs@example.com"               |
	| JsonHostname               | "www.example.com"                      |
	| JsonIdnEmail               | "실례@실례.테스트"                            |
	| JsonIdnHostname            | "실례.테스트"                               |
	| JsonIpV4                   | "192.168.0.1"                          |
	| JsonIpV6                   | "::1"                                  |
	| JsonIri                    | "http://ƒøø.ßår/?∂éœ=πîx#πîüx"         |
	| JsonIriReference           | "http://ƒøø.ßår/?∂éœ=πîx#πîüx"         |
	| JsonPointer                | "/foo/bar~0/baz~1/%a"                  |
	| JsonRegex                  | "([abc])+\\\\s+$"                      |
	| JsonRelativePointer        | "0/foo/bar"                            |
	| JsonTime                   | "08:30:06Z"                            |
	| JsonUri                    | "http://foo.bar/?baz=qux#quux"         |
	| JsonUriReference           | "http://foo.bar/?baz=qux#quux"         |
	| JsonUuid                   | "2EB8AA08-AA98-11EA-B4AA-73B441D16380" |
	| JsonContent                | "{\\"foo\\": \\"bar\\"}"               |
	| JsonContentPre201909       | "{\\"foo\\": \\"bar\\"}"               |
	| JsonBase64Content          | "eyJmb28iOiJiYXIifQ=="                 |
	| JsonBase64ContentPre201909 | "eyJmb28iOiJiYXIifQ=="                 |
	| JsonBase64String           | "SGVsbG8gd29ybGQ="                     |
	| JsonBase64StringPre201909  | "SGVsbG8gd29ybGQ="                     |
	| JsonInt64                  | 4                                      |
	| JsonInt32                  | 4                                      |
	| JsonInt16                  | 4                                      |
	| JsonSByte                  | 4                                      |
	| JsonUInt64                 | 4                                      |
	| JsonUInt32                 | 4                                      |
	| JsonUInt16                 | 4                                      |
	| JsonByte                   | 4                                      |
	| JsonSingle                 | 4.1                                    |
	| JsonDouble                 | 4.1                                    |
	| JsonDecimal                | 4.1                                    |


Scenario Outline: JSON backed element
	Given the JsonElement backed <TTarget> <value>
	When I write the value to a UTF8 JSON writer and store the resulting JSON
	Then the JSON should equal a valid instance of the <TTarget>
Examples:
	| TTarget                    | value                                  |
	| JsonString                 | "foo"                                  |
	| JsonNumber                 | 3.1                                    |
	| JsonInteger                | 3                                      |
	| JsonBoolean                | true                                   |
	| JsonBoolean                | false                                  |
	| JsonArray                  | []                                     |
	| JsonArray                  | [1,2,3,"4"]                            |
	| JsonObject                 | {}                                     |
	| JsonObject                 | {"foo":"bar"}                          |
	| JsonDate                   | "1963-06-19"                           |
	| JsonDateTime               | "1990-12-31T15:59:50.123-08:00"        |
	| JsonDuration               | "P4DT12H30M5S"                         |
	| JsonEmail                  | "joe.bloggs@example.com"               |
	| JsonHostname               | "www.example.com"                      |
	| JsonIdnEmail               | "실례@실례.테스트"                            |
	| JsonIdnHostname            | "실례.테스트"                               |
	| JsonIpV4                   | "192.168.0.1"                          |
	| JsonIpV6                   | "::1"                                  |
	| JsonIri                    | "http://ƒøø.ßår/?∂éœ=πîx#πîüx"         |
	| JsonIriReference           | "http://ƒøø.ßår/?∂éœ=πîx#πîüx"         |
	| JsonPointer                | "/foo/bar~0/baz~1/%a"                  |
	| JsonRegex                  | "([abc])+\\\\s+$"                      |
	| JsonRelativePointer        | "0/foo/bar"                            |
	| JsonTime                   | "08:30:06Z"                            |
	| JsonUri                    | "http://foo.bar/?baz=qux#quux"         |
	| JsonUriReference           | "http://foo.bar/?baz=qux#quux"         |
	| JsonUuid                   | "2EB8AA08-AA98-11EA-B4AA-73B441D16380" |
	| JsonContent                | "{\\"foo\\": \\"bar\\"}"               |
	| JsonContentPre201909       | "{\\"foo\\": \\"bar\\"}"               |
	| JsonBase64Content          | "eyJmb28iOiJiYXIifQ=="                 |
	| JsonBase64ContentPre201909 | "eyJmb28iOiJiYXIifQ=="                 |
	| JsonBase64String           | "SGVsbG8gd29ybGQ="                     |
	| JsonBase64StringPre201909  | "SGVsbG8gd29ybGQ="                     |
	| JsonInt64                  | 4                                      |
	| JsonInt32                  | 4                                      |
	| JsonInt16                  | 4                                      |
	| JsonSByte                  | 4                                      |
	| JsonUInt64                 | 4                                      |
	| JsonUInt32                 | 4                                      |
	| JsonUInt16                 | 4                                      |
	| JsonByte                   | 4                                      |
	| JsonSingle                 | 4.1                                    |
	| JsonDouble                 | 4.1                                    |
	| JsonDecimal                | 4.1                                    |
