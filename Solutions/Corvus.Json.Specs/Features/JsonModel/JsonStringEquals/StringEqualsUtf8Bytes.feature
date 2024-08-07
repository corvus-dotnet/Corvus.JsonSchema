Feature: JsonStringEqualsUtf8Bytes
	Use equals

Scenario Outline: Compare a JSON string using Utf8 success
	Given the JsonElement backed <StringType> "Hello"
	When the utf8bytes "Hello" are compared to the <StringType>
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
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a long JSON string using Utf8 success
	Given the JsonElement backed <StringType> "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"
	When the utf8bytes "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890" are compared to the <StringType>
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
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a JSON string using Utf8 failure
	Given the JsonElement backed <StringType> "Hello"
	When the utf8bytes "Goodbye" are compared to the <StringType>
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
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a dotnet backed JSON string using Utf8 success
	Given the dotnet backed <StringType> "Hello"
	When the utf8bytes "Hello" are compared to the <StringType>
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
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a long dotnet backed JSON string using Utf8 success
	Given the dotnet backed <StringType> "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"
	When the utf8bytes "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890" are compared to the <StringType>
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
	| JsonBase64String          |
	| JsonBase64StringPre201909 |

Scenario Outline: Compare a dotnet backed JSON string using Utf8 failure
	Given the dotnet backed <StringType> "Hello"
	When the utf8bytes "Goodbye" are compared to the <StringType>
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
	| JsonBase64String          |
	| JsonBase64StringPre201909 |