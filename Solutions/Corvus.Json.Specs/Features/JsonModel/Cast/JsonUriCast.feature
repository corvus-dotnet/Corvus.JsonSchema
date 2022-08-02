Feature: JsonUriCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an uri
	Given the JsonElement backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to JsonAny
	Then the result should equal the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Cast to JsonAny for dotnet backed value as an uri
	Given the dotnet backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to JsonAny
	Then the result should equal the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Cast from JsonAny for json element backed value as an uri
	Given the JsonAny for "http://foo.bar/?baz=qux#quux"
	When I cast the JsonAny to JsonUri
	Then the result should equal the JsonUri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to JsonString for json element backed value as an uri
	Given the JsonElement backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to JsonString
	Then the result should equal the JsonString "http://foo.bar/?baz=qux#quux"

Scenario: Cast to JsonString for dotnet backed value as an uri
	Given the dotnet backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to JsonString
	Then the result should equal the JsonString "http://foo.bar/?baz=qux#quux"

Scenario: Cast from JsonString for json element backed value as an uri
	Given the JsonString for "http://foo.bar/?baz=qux#quux"
	When I cast the JsonString to JsonUri
	Then the result should equal the JsonUri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to Uri for json element backed value as an uri
	Given the JsonElement backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to Uri
	Then the result should equal the Uri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to Uri for dotnet backed value as an uri
	Given the dotnet backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to Uri
	Then the result should equal the Uri "http://foo.bar/?baz=qux#quux"

Scenario: Cast from Uri for json element backed value as an uri
	Given the Uri for "http://foo.bar/?baz=qux#quux"
	When I cast the Uri to JsonUri
	Then the result should equal the JsonUri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an uri
	Given the JsonElement backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "http://foo.bar/?baz=qux#quux"

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an uri
	Given the dotnet backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "http://foo.bar/?baz=qux#quux"

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an uri
	Given the ReadOnlyMemory<char> for "http://foo.bar/?baz=qux#quux"
	When I cast the ReadOnlySpan<char> to JsonUri
	Then the result should equal the JsonUri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to string for json element backed value as an uri
	Given the JsonElement backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to string
	Then the result should equal the string "http://foo.bar/?baz=qux#quux"

Scenario: Cast to string for dotnet backed value as an uri
	Given the dotnet backed JsonUri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonUri to string
	Then the result should equal the string "http://foo.bar/?baz=qux#quux"

Scenario: Cast from string for json element backed value as an uri
	Given the string for "http://foo.bar/?baz=qux#quux"
	When I cast the string to JsonUri
	Then the result should equal the JsonUri "http://foo.bar/?baz=qux#quux"

