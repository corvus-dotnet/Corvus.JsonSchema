Feature: JsonStringEqualsCharSpan
	Use equals

Scenario Outline: Compare a JSON string using span of char success
	Given the JsonElement backed <StringType> "Hello"
	When the charSpan "Hello" is compared to the <StringType>
	Then the result should be true

Examples:
	| StringType                 |
	| JsonString                 |
	| JsonDate                   |
	| JsonDateTime               |
	| JsonDuration               |
	| JsonEmail                  |
	| JsonHostname               |
	| JsonIdnEmail               |
	| JsonIdnHostname            |
	| JsonIpV4                   |
	| JsonIpV6                   |
	| JsonIri                    |
	| JsonIriReference           |
	| JsonPointer                |
	| JsonRegex                  |
	| JsonRelativePointer        |
	| JsonTime                   |
	| JsonUri                    |
	| JsonUriReference           |
	| JsonUuid                   |
	| JsonContent                |
	| JsonContentPre201909       |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |
	| JsonBase64String           |
	| JsonBase64StringPre201909  |

Scenario Outline: Compare a JSON string using span of char failure
	Given the JsonElement backed <StringType> "Hello"
	When the charSpan "Goodbye" is compared to the <StringType>
	Then the result should be false

Examples:
	| StringType                |
	| JsonString                |
	| JsonDate                  |
	| JsonDateTime              |
	| JsonDuration              |
	| JsonEmail                 |
	| JsonHostname              |
	| JsonIdnEmail              |
	| JsonIdnHostname           |
	| JsonIpV4                  |
	| JsonIpV6                  |
	| JsonIri                   |
	| JsonIriReference          |
	| JsonPointer               |
	| JsonRegex                 |
	| JsonRelativePointer       |
	| JsonTime                  |
	| JsonUri                   |
	| JsonUriReference          |
	| JsonUuid                  |
	| JsonContent               |
	| JsonContentPre201909      |
	| JsonBase64Content         |
	| JsonBase64ContentPre201909 |
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a dotnet back JSON string using span of char success
	Given the dotnet backed <StringType> "Hello"
	When the charSpan "Hello" is compared to the <StringType>
	Then the result should be true

Examples:
	| StringType                |
	| JsonString                |
	| JsonDate                  |
	| JsonDateTime              |
	| JsonDuration              |
	| JsonEmail                 |
	| JsonHostname              |
	| JsonIdnEmail              |
	| JsonIdnHostname           |
	| JsonIpV4                  |
	| JsonIpV6                  |
	| JsonIri                   |
	| JsonIriReference          |
	| JsonPointer               |
	| JsonRegex                 |
	| JsonRelativePointer       |
	| JsonTime                  |
	| JsonUri                   |
	| JsonUriReference          |
	| JsonUuid                  |
	| JsonContent               |
	| JsonContentPre201909      |
	| JsonBase64Content         |
	| JsonBase64ContentPre201909 |
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a dotnet backed JSON string using span of char failure
	Given the dotnet backed <StringType> "Hello"
	When the charSpan "Goodbye" is compared to the <StringType>
	Then the result should be false

Examples:
	| StringType                |
	| JsonString                |
	| JsonDate                  |
	| JsonDateTime              |
	| JsonDuration              |
	| JsonEmail                 |
	| JsonHostname              |
	| JsonIdnEmail              |
	| JsonIdnHostname           |
	| JsonIpV4                  |
	| JsonIpV6                  |
	| JsonIri                   |
	| JsonIriReference          |
	| JsonPointer               |
	| JsonRegex                 |
	| JsonRelativePointer       |
	| JsonTime                  |
	| JsonUri                   |
	| JsonUriReference          |
	| JsonUuid                  |
	| JsonContent               |
	| JsonContentPre201909      |
	| JsonBase64Content         |
	| JsonBase64ContentPre201909 |
	| JsonBase64String          |
	| JsonBase64StringPre201909 |