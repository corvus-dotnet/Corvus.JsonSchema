Feature: JsonHostnameCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a hostname
	Given the JsonElement backed JsonHostname "www.example.com"
	When I cast the JsonHostname to JsonAny
	Then the result should equal the JsonAny 'www.example.com'

Scenario: Cast to JsonAny for dotnet backed value as a hostname
	Given the dotnet backed JsonHostname www.example.com
	When I cast the JsonHostname to JsonAny
	Then the result should equal the JsonAny 'www.example.com'

Scenario: Cast from JsonAny for json element backed value as a hostname
	Given the JsonAny for "www.example.com"
	When I cast the JsonAny to JsonHostname
	Then the result should equal the JsonHostname 'www.example.com'

Scenario: Cast to JsonString for json element backed value as a hostname
	Given the JsonElement backed JsonHostname "www.example.com"
	When I cast the JsonHostname to JsonString
	Then the result should equal the JsonString 'www.example.com'

Scenario: Cast to JsonString for dotnet backed value as a hostname
	Given the dotnet backed JsonHostname www.example.com
	When I cast the JsonHostname to JsonString
	Then the result should equal the JsonString 'www.example.com'

Scenario: Cast from JsonString for json element backed value as a hostname
	Given the JsonString for "www.example.com"
	When I cast the JsonString to JsonHostname
	Then the result should equal the JsonHostname 'www.example.com'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as a hostname
	Given the JsonElement backed JsonHostname "www.example.com"
	When I cast the JsonHostname to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'www.example.com'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as a hostname
	Given the dotnet backed JsonHostname www.example.com
	When I cast the JsonHostname to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'www.example.com'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as a hostname
	Given the ReadOnlyMemory<char> for "www.example.com"
	When I cast the ReadOnlySpan<char> to JsonHostname
	Then the result should equal the JsonHostname 'www.example.com'

Scenario: Cast to string for json element backed value as a hostname
	Given the JsonElement backed JsonHostname "www.example.com"
	When I cast the JsonHostname to string
	Then the result should equal the string 'www.example.com'

Scenario: Cast to string for dotnet backed value as a hostname
	Given the dotnet backed JsonHostname www.example.com
	When I cast the JsonHostname to string
	Then the result should equal the string 'www.example.com'

Scenario: Cast from string for json element backed value as a hostname
	Given the string for "www.example.com"
	When I cast the string to JsonHostname
	Then the result should equal the JsonHostname 'www.example.com'

