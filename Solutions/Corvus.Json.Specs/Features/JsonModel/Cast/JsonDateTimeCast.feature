Feature: JsonDateTimeCast
	ValidateTime the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an dateTime
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When I cast the JsonDateTime to JsonAny
	Then the result should equal the JsonAny '2018-11-13T20:20:39+00:00'

Scenario: Cast to JsonAny for dotnet backed value as an dateTime
	Given the dotnet backed JsonDateTime 2018-11-13T20:20:39+00:00
	When I cast the JsonDateTime to JsonAny
	Then the result should equal the JsonAny '2018-11-13T20:20:39+00:00'

Scenario: Cast from JsonAny for json element backed value as an dateTime
	Given the JsonAny for "2018-11-13T20:20:39+00:00"
	When I cast the JsonAny to JsonDateTime
	Then the result should equal the JsonDateTime '2018-11-13T20:20:39+00:00'

Scenario: Cast to JsonString for json element backed value as an dateTime
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When I cast the JsonDateTime to JsonString
	Then the result should equal the JsonString '2018-11-13T20:20:39+00:00'

Scenario: Cast to JsonString for dotnet backed value as an dateTime
	Given the dotnet backed JsonDateTime 2018-11-13T20:20:39+00:00
	When I cast the JsonDateTime to JsonString
	Then the result should equal the JsonString '2018-11-13T20:20:39+00:00'

Scenario: Cast from JsonString for json element backed value as an dateTime
	Given the JsonString for "2018-11-13T20:20:39+00:00"
	When I cast the JsonString to JsonDateTime
	Then the result should equal the JsonDateTime '2018-11-13T20:20:39+00:00'

Scenario: Cast from OffsetDateTime for json element backed value as an dateTime
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When I cast the JsonDateTime to OffsetDateTime
	Then the result should equal the OffsetDateTime '2018-11-13T20:20:39+00:00'

Scenario: Cast to OffsetDateTime for json element backed value as an dateTime
	Given the OffsetDateTime for "2018-11-13T20:20:39+00:00"
	When I cast the OffsetDateTime to JsonDateTime
	Then the result should equal the JsonDateTime '2018-11-13T20:20:39+00:00'

Scenario: Cast to ReadOnlySpan<byte> for json element backed value as an dateTime
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When I cast the JsonDateTime to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> '2018-11-13T20:20:39+00:00'

Scenario: Cast to ReadOnlySpan<byte> for dotnet backed value as an dateTime
	Given the dotnet backed JsonDateTime 2018-11-13T20:20:39+00:00
	When I cast the JsonDateTime to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> '2018-11-13T20:20:39+00:00'

Scenario: Cast from ReadOnlySpan<byte> for json element backed value as an dateTime
	Given the ReadOnlyMemory<byte> for "2018-11-13T20:20:39+00:00"
	When I cast the ReadOnlySpan<byte> to JsonDateTime
	Then the result should equal the JsonDateTime '2018-11-13T20:20:39+00:00'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an dateTime
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When I cast the JsonDateTime to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> '2018-11-13T20:20:39+00:00'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an dateTime
	Given the dotnet backed JsonDateTime 2018-11-13T20:20:39+00:00
	When I cast the JsonDateTime to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> '2018-11-13T20:20:39+00:00'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an dateTime
	Given the ReadOnlyMemory<char> for "2018-11-13T20:20:39+00:00"
	When I cast the ReadOnlySpan<char> to JsonDateTime
	Then the result should equal the JsonDateTime '2018-11-13T20:20:39+00:00'

Scenario: Cast to string for json element backed value as an dateTime
	Given the JsonElement backed JsonDateTime "2018-11-13T20:20:39+00:00"
	When I cast the JsonDateTime to string
	Then the result should equal the string '2018-11-13T20:20:39+00:00'

Scenario: Cast to string for dotnet backed value as an dateTime
	Given the dotnet backed JsonDateTime 2018-11-13T20:20:39+00:00
	When I cast the JsonDateTime to string
	Then the result should equal the string '2018-11-13T20:20:39+00:00'

Scenario: Cast from string for json element backed value as an dateTime
	Given the string for "2018-11-13T20:20:39+00:00"
	When I cast the string to JsonDateTime
	Then the result should equal the JsonDateTime '2018-11-13T20:20:39+00:00'

