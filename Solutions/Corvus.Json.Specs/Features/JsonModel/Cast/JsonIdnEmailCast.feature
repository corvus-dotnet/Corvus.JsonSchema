Feature: JsonIdnEmailCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to JsonAny
	Then the result should equal the JsonAny 'hello@endjin.com'

Scenario: Cast to JsonAny for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail hello@endjin.com
	When I cast the JsonIdnEmail to JsonAny
	Then the result should equal the JsonAny 'hello@endjin.com'

Scenario: Cast from JsonAny for json element backed value as an idnEmail
	Given the JsonAny for "hello@endjin.com"
	When I cast the JsonAny to JsonIdnEmail
	Then the result should equal the JsonIdnEmail 'hello@endjin.com'

Scenario: Cast to JsonString for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to JsonString
	Then the result should equal the JsonString 'hello@endjin.com'

Scenario: Cast to JsonString for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail hello@endjin.com
	When I cast the JsonIdnEmail to JsonString
	Then the result should equal the JsonString 'hello@endjin.com'

Scenario: Cast from JsonString for json element backed value as an idnEmail
	Given the JsonString for "hello@endjin.com"
	When I cast the JsonString to JsonIdnEmail
	Then the result should equal the JsonIdnEmail 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<byte> for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<byte> for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail hello@endjin.com
	When I cast the JsonIdnEmail to ReadOnlySpan<byte>
	Then the result should equal the ReadOnlySpan<byte> 'hello@endjin.com'

Scenario: Cast from ReadOnlySpan<byte> for json element backed value as an idnEmail
	Given the ReadOnlyMemory<byte> for "hello@endjin.com"
	When I cast the ReadOnlySpan<byte> to JsonIdnEmail
	Then the result should equal the JsonIdnEmail 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<char> for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'hello@endjin.com'

Scenario: Cast to ReadOnlySpan<char> for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail hello@endjin.com
	When I cast the JsonIdnEmail to ReadOnlySpan<char>
	Then the result should equal the ReadOnlySpan<char> 'hello@endjin.com'

Scenario: Cast from ReadOnlySpan<char> for json element backed value as an idnEmail
	Given the ReadOnlyMemory<char> for "hello@endjin.com"
	When I cast the ReadOnlySpan<char> to JsonIdnEmail
	Then the result should equal the JsonIdnEmail 'hello@endjin.com'

Scenario: Cast to string for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to string
	Then the result should equal the string 'hello@endjin.com'

Scenario: Cast to string for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail hello@endjin.com
	When I cast the JsonIdnEmail to string
	Then the result should equal the string 'hello@endjin.com'

Scenario: Cast from string for json element backed value as an idnEmail
	Given the string for "hello@endjin.com"
	When I cast the string to JsonIdnEmail
	Then the result should equal the JsonIdnEmail 'hello@endjin.com'

