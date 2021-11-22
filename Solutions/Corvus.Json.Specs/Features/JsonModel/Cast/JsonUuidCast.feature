Feature: JsonUuidCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an uuid
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonUuid to JsonAny
	Then the result should equal the JsonAny 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to JsonAny for dotnet backed value as an uuid
	Given the dotnet backed JsonUuid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the JsonUuid to JsonAny
	Then the result should equal the JsonAny 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast from JsonAny for json element backed value as an uuid
	Given the JsonAny for "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonAny to JsonUuid
	Then the result should equal the JsonUuid 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to JsonString for json element backed value as an uuid
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonUuid to JsonString
	Then the result should equal the JsonString 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to JsonString for dotnet backed value as an uuid
	Given the dotnet backed JsonUuid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the JsonUuid to JsonString
	Then the result should equal the JsonString 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast from JsonString for json element backed value as an uuid
	Given the JsonString for "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonString to JsonUuid
	Then the result should equal the JsonUuid 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to Guid for json element backed value as an uuid
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonUuid to Guid
	Then the result should equal the Guid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1

Scenario: Cast to Guid for dotnet backed value as an uuid
	Given the dotnet backed JsonUuid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the JsonUuid to Guid
	Then the result should equal the Guid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1

Scenario: Cast from Guid for json element backed value as an uuid
	Given the Guid for c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the Guid to JsonUuid
	Then the result should equal the JsonUuid 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to ReadOnlySpan<byte> for json element backed value as an uuid
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonUuid to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to ReadOnlySpan<byte> for dotnet backed value as an uuid
	Given the dotnet backed JsonUuid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the JsonUuid to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast from ReadOnlySpan<byte> for json element backed value as an uuid
	Given the ReadOnlyMemory<byte> for "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the ReadOnlySpan<byte> to JsonUuid
	Then the result should equal the JsonUuid 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an uuid
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonUuid to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an uuid
	Given the dotnet backed JsonUuid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the JsonUuid to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an uuid
	Given the ReadOnlyMemory<char> for "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the ReadOnlySpan<char> to JsonUuid
	Then the result should equal the JsonUuid 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to string for json element backed value as an uuid
	Given the JsonElement backed JsonUuid "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the JsonUuid to string
	Then the result should equal the string 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast to string for dotnet backed value as an uuid
	Given the dotnet backed JsonUuid c3f2a2a3-72c1-4abc-a741-b0e7095f20d1
	When I cast the JsonUuid to string
	Then the result should equal the string 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

Scenario: Cast from string for json element backed value as an uuid
	Given the string for "c3f2a2a3-72c1-4abc-a741-b0e7095f20d1"
	When I cast the string to JsonUuid
	Then the result should equal the JsonUuid 'c3f2a2a3-72c1-4abc-a741-b0e7095f20d1'

