Feature: JsonStringTryGetValue
	Optimize parsing a value from a JSON string

Scenario Outline: Get a numeric value from a dotnet-backed string using a char parser
	Given the dotnet backed <StringType> "2"
	When you try get an integer from the <StringType> using a char parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

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

Scenario Outline: Get a numeric value from a jsonelement-backed string using a char parser
	Given the JsonElement backed <StringType> "2"
	When you try get an integer from the <StringType> using a char parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

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

Scenario Outline: Get a numeric value from a dotnet-backed string which does not support the format using a char parser
	Given the dotnet backed <StringType> "Hello"
	When you try get an integer from the <StringType> using a char parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

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

Scenario Outline: Get a numeric value from a jsonelement-backed string which does not support the format using a char parser
	Given the JsonElement backed <StringType> "Hello"
	When you try get an integer from the <StringType> using a char parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

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

Scenario Outline: Get a numeric value from a dotnet-backed string using a utf8 parser
	Given the dotnet backed <StringType> "2"
	When you try get an integer from the <StringType> using a utf8 parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

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

Scenario Outline: Get a numeric value from a jsonelement-backed string using a utf8 parser
	Given the JsonElement backed <StringType> "2"
	When you try get an integer from the <StringType> using a utf8 parser with the multiplier 3
	Then the parse result should be true
	And the parsed value should be equal to the number 6

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
Scenario Outline: Get a numeric value from a dotnet-backed string which does not support the format using a utf8 parser
	Given the dotnet backed <StringType> "Hello"
	When you try get an integer from the <StringType> using a utf8 parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

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

Scenario Outline: Get a numeric value from a jsonelement-backed string which does not support the format using a utf8 parser
	Given the JsonElement backed <StringType> "Hello"
	When you try get an integer from the <StringType> using a utf8 parser with the multiplier 3
	Then the parse result should be false
	And the parsed value should be null

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