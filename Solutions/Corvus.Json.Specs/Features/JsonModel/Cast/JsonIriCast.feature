Feature: JsonIriCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an iri
	Given the JsonElement backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to JsonAny
	Then the result should equal the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Cast to JsonAny for dotnet backed value as an iri
	Given the dotnet backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to JsonAny
	Then the result should equal the JsonAny "http://foo.bar/?baz=qux#quux"

Scenario: Cast to JsonString for json element backed value as an iri
	Given the JsonElement backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to JsonString
	Then the result should equal the JsonString "http://foo.bar/?baz=qux#quux"

Scenario: Cast to JsonString for dotnet backed value as an iri
	Given the dotnet backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to JsonString
	Then the result should equal the JsonString "http://foo.bar/?baz=qux#quux"

Scenario: Cast from JsonString for json element backed value as an iri
	Given the JsonString for "http://foo.bar/?baz=qux#quux"
	When I cast the JsonString to JsonIri
	Then the result should equal the JsonIri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to Uri for json element backed value as an iri
	Given the JsonElement backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to Uri
	Then the result should equal the Uri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to Uri for dotnet backed value as an iri
	Given the dotnet backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to Uri
	Then the result should equal the Uri "http://foo.bar/?baz=qux#quux"

Scenario: Cast from Uri for json element backed value as an iri
	Given the Uri for "http://foo.bar/?baz=qux#quux"
	When I cast the Uri to JsonIri
	Then the result should equal the JsonIri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an iri
	Given the JsonElement backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "http://foo.bar/?baz=qux#quux"

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an iri
	Given the dotnet backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> "http://foo.bar/?baz=qux#quux"

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an iri
	Given the ReadOnlyMemory<char> for "http://foo.bar/?baz=qux#quux"
	When I cast the ReadOnlySpan<char> to JsonIri
	Then the result should equal the JsonIri "http://foo.bar/?baz=qux#quux"

Scenario: Cast to string for json element backed value as an iri
	Given the JsonElement backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to string
	Then the result should equal the string "http://foo.bar/?baz=qux#quux"

Scenario: Cast to string for dotnet backed value as an iri
	Given the dotnet backed JsonIri "http://foo.bar/?baz=qux#quux"
	When I cast the JsonIri to string
	Then the result should equal the string "http://foo.bar/?baz=qux#quux"

Scenario: Cast from string for json element backed value as an iri
	Given the string for "http://foo.bar/?baz=qux#quux"
	When I cast the string to JsonIri
	Then the result should equal the JsonIri "http://foo.bar/?baz=qux#quux"

