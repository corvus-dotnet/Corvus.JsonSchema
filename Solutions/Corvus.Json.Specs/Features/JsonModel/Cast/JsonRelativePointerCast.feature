Feature: JsonPointerCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a pointer
	Given the JsonElement backed JsonPointer "/a~1b"
	When I cast the JsonPointer to JsonAny
	Then the result should equal the JsonAny "/a~1b"

Scenario: Cast to JsonAny for dotnet backed value as a pointer
	Given the dotnet backed JsonPointer "/a~1b"
	When I cast the JsonPointer to JsonAny
	Then the result should equal the JsonAny "/a~1b"

Scenario: Cast to JsonString for json element backed value as a pointer
	Given the JsonElement backed JsonPointer "/a~1b"
	When I cast the JsonPointer to JsonString
	Then the result should equal the JsonString "/a~1b"

Scenario: Cast to JsonString for dotnet backed value as a pointer
	Given the dotnet backed JsonPointer "/a~1b"
	When I cast the JsonPointer to JsonString
	Then the result should equal the JsonString "/a~1b"

Scenario: Cast from JsonString for json element backed value as a pointer
	Given the JsonString for "/a~1b"
	When I cast the JsonString to JsonPointer
	Then the result should equal the JsonPointer "/a~1b"

Scenario: Cast to string for json element backed value as a pointer
	Given the JsonElement backed JsonPointer "/a~1b"
	When I cast the JsonPointer to string
	Then the result should equal the string "/a~1b"

Scenario: Cast to string for dotnet backed value as a pointer
	Given the dotnet backed JsonPointer "/a~1b"
	When I cast the JsonPointer to string
	Then the result should equal the string "/a~1b"

Scenario: Cast from string for json element backed value as a pointer
	Given the string for "/a~1b"
	When I cast the string to JsonPointer
	Then the result should equal the JsonPointer "/a~1b"

