Feature: JsonStringCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a string
	Given the JsonElement backed JsonString "Hello"
	When I cast the JsonString to JsonAny
	Then the result should equal the JsonAny "Hello"

Scenario: Cast to JsonAny for dotnet backed value as a string
	Given the dotnet backed JsonString "Hello"
	When I cast the JsonString to JsonAny
	Then the result should equal the JsonAny "Hello"

Scenario: Cast to string for json element backed value as a string
	Given the JsonElement backed JsonString "Hello"
	When I cast the JsonString to string
	Then the result should equal the string "Hello"

Scenario: Cast from string for json element backed value as a string
	Given the string for "Hello"
	When I cast the string to JsonString
	Then the result should equal the JsonString "Hello"

