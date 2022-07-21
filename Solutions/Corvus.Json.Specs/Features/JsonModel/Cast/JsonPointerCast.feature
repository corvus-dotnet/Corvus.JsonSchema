Feature: JsonRelativePointerCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a relativePointer
	Given the JsonElement backed JsonRelativePointer "0/foo/bar"
	When I cast the JsonRelativePointer to JsonAny
	Then the result should equal the JsonAny '0/foo/bar'

Scenario: Cast to JsonAny for dotnet backed value as a relativePointer
	Given the dotnet backed JsonRelativePointer 0/foo/bar
	When I cast the JsonRelativePointer to JsonAny
	Then the result should equal the JsonAny '0/foo/bar'

Scenario: Cast from JsonAny for json element backed value as a relativePointer
	Given the JsonAny for "0/foo/bar"
	When I cast the JsonAny to JsonRelativePointer
	Then the result should equal the JsonRelativePointer '0/foo/bar'

Scenario: Cast to JsonString for json element backed value as a relativePointer
	Given the JsonElement backed JsonRelativePointer "0/foo/bar"
	When I cast the JsonRelativePointer to JsonString
	Then the result should equal the JsonString '0/foo/bar'

Scenario: Cast to JsonString for dotnet backed value as a relativePointer
	Given the dotnet backed JsonRelativePointer 0/foo/bar
	When I cast the JsonRelativePointer to JsonString
	Then the result should equal the JsonString '0/foo/bar'

Scenario: Cast from JsonString for json element backed value as a relativePointer
	Given the JsonString for "0/foo/bar"
	When I cast the JsonString to JsonRelativePointer
	Then the result should equal the JsonRelativePointer '0/foo/bar'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as a relativePointer
	Given the JsonElement backed JsonRelativePointer "0/foo/bar"
	When I cast the JsonRelativePointer to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> '0/foo/bar'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as a relativePointer
	Given the dotnet backed JsonRelativePointer 0/foo/bar
	When I cast the JsonRelativePointer to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> '0/foo/bar'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as a relativePointer
	Given the ReadOnlyMemory<char> for "0/foo/bar"
	When I cast the ReadOnlySpan<char> to JsonRelativePointer
	Then the result should equal the JsonRelativePointer '0/foo/bar'

Scenario: Cast to string for json element backed value as a relativePointer
	Given the JsonElement backed JsonRelativePointer "0/foo/bar"
	When I cast the JsonRelativePointer to string
	Then the result should equal the string '0/foo/bar'

Scenario: Cast to string for dotnet backed value as a relativePointer
	Given the dotnet backed JsonRelativePointer 0/foo/bar
	When I cast the JsonRelativePointer to string
	Then the result should equal the string '0/foo/bar'

Scenario: Cast from string for json element backed value as a relativePointer
	Given the string for "0/foo/bar"
	When I cast the string to JsonRelativePointer
	Then the result should equal the JsonRelativePointer '0/foo/bar'

