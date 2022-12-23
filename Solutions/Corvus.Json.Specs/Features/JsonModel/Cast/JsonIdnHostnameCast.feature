Feature: JsonIdnHostnameCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a idnIdnHostname
	Given the JsonElement backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to JsonAny
	Then the result should equal the JsonAny "www.example.com"

Scenario: Cast to JsonAny for dotnet backed value as a idnIdnHostname
	Given the dotnet backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to JsonAny
	Then the result should equal the JsonAny "www.example.com"

Scenario: Cast from JsonAny for json element backed value as a idnIdnHostname
	Given the JsonAny for "www.example.com"
	When I cast the JsonAny to JsonIdnHostname
	Then the result should equal the JsonIdnHostname "www.example.com"

Scenario: Cast to JsonString for json element backed value as a idnIdnHostname
	Given the JsonElement backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to JsonString
	Then the result should equal the JsonString "www.example.com"

Scenario: Cast to JsonString for dotnet backed value as a idnIdnHostname
	Given the dotnet backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to JsonString
	Then the result should equal the JsonString "www.example.com"

Scenario: Cast from JsonString for json element backed value as a idnIdnHostname
	Given the JsonString for "www.example.com"
	When I cast the JsonString to JsonIdnHostname
	Then the result should equal the JsonIdnHostname "www.example.com"

Scenario: Cast to ReadOnlySpan<char> for json element backed value as a idnIdnHostname
	Given the JsonElement backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "www.example.com"

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as a idnIdnHostname
	Given the dotnet backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "www.example.com"

Scenario: Cast from ReadOnlySpan<char> for json element backed value as a idnIdnHostname
	Given the ReadOnlyMemory<char> for "www.example.com"
	When I cast the ReadOnlySpan<char> to JsonIdnHostname
	Then the result should equal the JsonIdnHostname "www.example.com"

Scenario: Cast to string for json element backed value as a idnIdnHostname
	Given the JsonElement backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to string
	Then the result should equal the string "www.example.com"

Scenario: Cast to string for dotnet backed value as a idnIdnHostname
	Given the dotnet backed JsonIdnHostname "www.example.com"
	When I cast the JsonIdnHostname to string
	Then the result should equal the string "www.example.com"

Scenario: Cast from string for json element backed value as a idnIdnHostname
	Given the string for "www.example.com"
	When I cast the string to JsonIdnHostname
	Then the result should equal the JsonIdnHostname "www.example.com"

