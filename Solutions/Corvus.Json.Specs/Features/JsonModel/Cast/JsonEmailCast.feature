Feature: JsonEmailCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an email
	Given the JsonElement backed JsonEmail "hello@endjin.com"
	When I cast the JsonEmail to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to JsonAny for dotnet backed value as an email
	Given the dotnet backed JsonEmail "hello@endjin.com"
	When I cast the JsonEmail to JsonAny
	Then the result should equal the JsonAny "hello@endjin.com"

Scenario: Cast to JsonString for json element backed value as an email
	Given the JsonElement backed JsonEmail "hello@endjin.com"
	When I cast the JsonEmail to JsonString
	Then the result should equal the JsonString "hello@endjin.com"

Scenario: Cast to JsonString for dotnet backed value as an email
	Given the dotnet backed JsonEmail "hello@endjin.com"
	When I cast the JsonEmail to JsonString
	Then the result should equal the JsonString "hello@endjin.com"

Scenario: Cast from JsonString for json element backed value as an email
	Given the JsonString for "hello@endjin.com"
	When I cast the JsonString to JsonEmail
	Then the result should equal the JsonEmail "hello@endjin.com"

Scenario: Cast to string for json element backed value as an email
	Given the JsonElement backed JsonEmail "hello@endjin.com"
	When I cast the JsonEmail to string
	Then the result should equal the string "hello@endjin.com"

Scenario: Cast to string for dotnet backed value as an email
	Given the dotnet backed JsonEmail "hello@endjin.com"
	When I cast the JsonEmail to string
	Then the result should equal the string "hello@endjin.com"

Scenario: Cast from string for json element backed value as an email
	Given the string for "hello@endjin.com"
	When I cast the string to JsonEmail
	Then the result should equal the JsonEmail "hello@endjin.com"

