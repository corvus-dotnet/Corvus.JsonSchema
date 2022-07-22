Feature: JsonContentCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an content
	Given the JsonElement backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to JsonAny
	Then the result should equal the JsonAny '"{\"foo\": \"bar\"}"'

Scenario: Cast to JsonAny for dotnet backed value as an content
	Given the dotnet backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to JsonAny
	Then the result should equal the JsonAny '"{\"foo\": \"bar\"}"'

Scenario: Cast from JsonAny for json element backed value as an content
	Given the JsonAny for "{\"foo\": \"bar\"}"
	When I cast the JsonAny to JsonContent
	Then the result should equal the JsonContent '"{\"foo\": \"bar\"}"'

Scenario: Cast to JsonString for json element backed value as an content
	Given the JsonElement backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to JsonString
	Then the result should equal the JsonString '"{\"foo\": \"bar\"}"'

Scenario: Cast to JsonString for dotnet backed value as an content
	Given the dotnet backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to JsonString
	Then the result should equal the JsonString '"{\"foo\": \"bar\"}"'

Scenario: Cast from JsonString for json element backed value as an content
	Given the JsonString for "{\"foo\": \"bar\"}"
	When I cast the JsonString to JsonContent
	Then the result should equal the JsonContent '"{\"foo\": \"bar\"}"'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an content
	Given the JsonElement backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> '{"foo": "bar"}'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an content
	Given the dotnet backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> '{"foo": "bar"}'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an content
	Given the ReadOnlyMemory<char> for "{\"foo\": \"bar\"}"
	When I cast the ReadOnlySpan<char> to JsonContent
	Then the result should equal the JsonContent '"{\"foo\": \"bar\"}"'

Scenario: Cast to string for json element backed value as an content
	Given the JsonElement backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to string
	Then the result should equal the string '{"foo": "bar"}'

Scenario: Cast to string for dotnet backed value as an content
	Given the dotnet backed JsonContent "{\"foo\": \"bar\"}"
	When I cast the JsonContent to string
	Then the result should equal the string '{"foo": "bar"}'

Scenario: Cast from string for json element backed value as an content
	Given the string for "{\"foo\": \"bar\"}"
	When I cast the string to JsonContent
	Then the result should equal the JsonContent '"{\"foo\": \"bar\"}"'

