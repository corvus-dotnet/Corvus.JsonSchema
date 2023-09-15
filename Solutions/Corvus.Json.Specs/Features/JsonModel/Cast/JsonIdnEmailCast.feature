Feature: JsonIdnEmailCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to JsonAny for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to JsonString for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to JsonString
	Then the result should equal the JsonString "hello@endjin.com"

Scenario: Cast to JsonString for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to JsonString
	Then the result should equal the JsonString "hello@endjin.com"

Scenario: Cast from JsonString for json element backed value as an idnEmail
	Given the JsonString for "hello@endjin.com"
	When I cast the JsonString to JsonIdnEmail
	Then the result should equal the JsonIdnEmail "hello@endjin.com"

Scenario: Cast to string for json element backed value as an idnEmail
	Given the JsonElement backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to string
	Then the result should equal the string "hello@endjin.com"

Scenario: Cast to string for dotnet backed value as an idnEmail
	Given the dotnet backed JsonIdnEmail "hello@endjin.com"
	When I cast the JsonIdnEmail to string
	Then the result should equal the string "hello@endjin.com"

Scenario: Cast from string for json element backed value as an idnEmail
	Given the string for "hello@endjin.com"
	When I cast the string to JsonIdnEmail
	Then the result should equal the JsonIdnEmail "hello@endjin.com"

