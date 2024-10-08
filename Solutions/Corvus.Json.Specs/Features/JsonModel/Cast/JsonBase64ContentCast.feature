Feature: <TargetType>Cast
	Validate the Json cast operators

Scenario Outline: Cast to JsonAny for json element backed value as a base64Content
	Given the JsonElement backed <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast to JsonAny for dotnet backed value as a base64Content
	Given the dotnet backed <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast to JsonString for json element backed value as a base64Content
	Given the JsonElement backed <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the <TargetType> to JsonString
	Then the result should equal the JsonString "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast to JsonString for dotnet backed value as a base64Content
	Given the dotnet backed <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the <TargetType> to JsonString
	Then the result should equal the JsonString "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast from JsonString for json element backed value as a base64Content
	Given the JsonString for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the JsonString to <TargetType>
	Then the result should equal the <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast to string for json element backed value as a base64Content
	Given the JsonElement backed <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the <TargetType> to string
	Then the result should equal the string "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast to string for dotnet backed value as a base64Content
	Given the dotnet backed <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the <TargetType> to string
	Then the result should equal the string "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

Scenario Outline: Cast from string for json element backed value as a base64Content
	Given the string for "eyAiaGVsbG8iOiAid29ybGQiIH0="
	When I cast the string to <TargetType>
	Then the result should equal the <TargetType> "eyAiaGVsbG8iOiAid29ybGQiIH0="
Examples:
	| TargetType                 |
	| JsonBase64Content          |
	| JsonBase64ContentPre201909 |

