Feature: JsonStringEqualsString
	Use equals

Scenario Outline: Compare a JSON string using a string success
	Given the JsonElement backed <StringType> "Hello"
	When the string "Hello" is compared to the <StringType>
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
	| JsonUriTemplate            |
	| JsonUuid                   |
	| JsonContent                |
	| JsonContentPre201909       |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |
	| JsonBase64String           |
	| JsonBase64StringPre201909  |

Scenario Outline: Compare a JSON string using a string failure
	Given the JsonElement backed <StringType> "Hello"
	When the string "Goodbye" is compared to the <StringType>
	Then the result should be false

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
	| JsonUriTemplate            |
	| JsonUuid                   |
	| JsonContent                |
	| JsonContentPre201909       |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |
	| JsonBase64String           |
	| JsonBase64StringPre201909  |

Scenario Outline: Compare a dotnet back JSON string using a string success
	Given the dotnet backed <StringType> "Hello"
	When the string "Hello" is compared to the <StringType>
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
	| JsonUriTemplate            |
	| JsonUuid                   |
	| JsonContent                |
	| JsonContentPre201909       |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |
	| JsonBase64String           |
	| JsonBase64StringPre201909  |

Scenario Outline: Compare a dotnet backed JSON string using a string failure
	Given the dotnet backed <StringType> "Hello"
	When the string "Goodbye" is compared to the <StringType>
	Then the result should be false

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
	| JsonUriTemplate            |
	| JsonUuid                   |
	| JsonContent                |
	| JsonContentPre201909       |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |
	| JsonBase64String           |
	| JsonBase64StringPre201909  |
