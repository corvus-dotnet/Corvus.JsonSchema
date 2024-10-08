Feature: JsonContentCast
	Validate the Json cast operators

Scenario Outline: Cast to JsonAny for json element backed value as an content
	Given the JsonElement backed <TargetType> "{\"foo\": \"bar\"}"
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny "{\"foo\": \"bar\"}"

Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast to JsonAny for dotnet backed value as an content
	Given the dotnet backed <TargetType> "{\"foo\": \"bar\"}"
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny "{\"foo\": \"bar\"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast to JsonString for json element backed value as an content
	Given the JsonElement backed <TargetType> "{\"foo\": \"bar\"}"
	When I cast the <TargetType> to JsonString
	Then the result should equal the JsonString "{\"foo\": \"bar\"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast to JsonString for dotnet backed value as an content
	Given the dotnet backed <TargetType> "{\"foo\": \"bar\"}"
	When I cast the <TargetType> to JsonString
	Then the result should equal the JsonString "{\"foo\": \"bar\"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast from JsonString for json element backed value as an content
	Given the JsonString for "{\"foo\": \"bar\"}"
	When I cast the JsonString to <TargetType>
	Then the result should equal the <TargetType> "{\"foo\": \"bar\"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast to string for json element backed value as an content
	Given the JsonElement backed <TargetType> "{\"foo\": \"bar\"}"
	When I cast the <TargetType> to string
	Then the result should equal the string "{"foo": "bar"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast to string for dotnet backed value as an content
	Given the dotnet backed <TargetType> "{\"foo\": \"bar\"}"
	When I cast the <TargetType> to string
	Then the result should equal the string "{"foo": "bar"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |

Scenario Outline: Cast from string for json element backed value as an content
	Given the string for "{\"foo\": \"bar\"}"
	When I cast the string to <TargetType>
	Then the result should equal the <TargetType> "{\"foo\": \"bar\"}"
Examples:
	| TargetType           |
	| JsonContent          |
	| JsonContentPre201909 |
