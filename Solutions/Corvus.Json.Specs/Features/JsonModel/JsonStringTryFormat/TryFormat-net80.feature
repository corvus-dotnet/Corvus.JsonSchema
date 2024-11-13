Feature: Basic SpanFormatable implementation

Scenario Outline: JsonElement backed string
	Given the JsonElement backed <TargetType> <value>
	When I TryFormat() the <TargetType>
	Then the formatted result should equal the string <value>
Examples:
	| TargetType                 | value |
	| JsonString                 | "Foo" |
	| JsonDate                   | "Foo" |
	| JsonDateTime               | "Foo" |
	| JsonDuration               | "Foo" |
	| JsonEmail                  | "Foo" |
	| JsonHostname               | "Foo" |
	| JsonIdnEmail               | "Foo" |
	| JsonIdnHostname            | "Foo" |
	| JsonIpV4                   | "Foo" |
	| JsonIpV6                   | "Foo" |
	| JsonIri                    | "Foo" |
	| JsonIriReference           | "Foo" |
	| JsonPointer                | "Foo" |
	| JsonRegex                  | "Foo" |
	| JsonRelativePointer        | "Foo" |
	| JsonTime                   | "Foo" |
	| JsonUri                    | "Foo" |
	| JsonUriReference           | "Foo" |
	| JsonUriTemplate            | "Foo" |
	| JsonUuid                   | "Foo" |
	| JsonContent                | "Foo" |
	| JsonContentPre201909       | "Foo" |
	| JsonBase64Content          | "Foo" |
	| JsonBase64ContentPre201909 | "Foo" |
	| JsonBase64String           | "Foo" |
	| JsonBase64StringPre201909  | "Foo" |

Scenario Outline: Ddotnet backed string
	Given the dotnet backed <TargetType> <value>
	When I TryFormat() the <TargetType>
	Then the formatted result should equal the string <value>
Examples:
	| TargetType                 | value |
	| JsonString                 | "Foo" |
	| JsonDate                   | "Foo" |
	| JsonDateTime               | "Foo" |
	| JsonDuration               | "Foo" |
	| JsonEmail                  | "Foo" |
	| JsonHostname               | "Foo" |
	| JsonIdnEmail               | "Foo" |
	| JsonIdnHostname            | "Foo" |
	| JsonIpV4                   | "Foo" |
	| JsonIpV6                   | "Foo" |
	| JsonIri                    | "Foo" |
	| JsonIriReference           | "Foo" |
	| JsonPointer                | "Foo" |
	| JsonRegex                  | "Foo" |
	| JsonRelativePointer        | "Foo" |
	| JsonTime                   | "Foo" |
	| JsonUri                    | "Foo" |
	| JsonUriReference           | "Foo" |
	| JsonUriTemplate            | "Foo" |
	| JsonUuid                   | "Foo" |
	| JsonContent                | "Foo" |
	| JsonContentPre201909       | "Foo" |
	| JsonBase64Content          | "Foo" |
	| JsonBase64ContentPre201909 | "Foo" |
	| JsonBase64String           | "Foo" |
	| JsonBase64StringPre201909  | "Foo" |
