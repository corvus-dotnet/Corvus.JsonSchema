Feature: JsonNumberCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a number
	Given the JsonElement backed JsonNumber 1.2
	When I cast the JsonNumber to JsonAny
	Then the result should equal the JsonAny 1.2

Scenario: Cast to JsonAny for dotnet backed value as a number
	Given the dotnet backed JsonNumber 1.2
	When I cast the JsonNumber to JsonAny
	Then the result should equal the JsonAny 1.2

Scenario: Cast to long for json element backed value as a number
	Given the JsonElement backed JsonNumber 12
	When I cast the JsonNumber to long
	Then the result should equal the long 12

Scenario: Cast to long for dotnet backed value as a number
	Given the dotnet backed JsonNumber 12
	When I cast the JsonNumber to long
	Then the result should equal the long 12

Scenario: Cast from long for json element backed value as a number
	Given the long for 12
	When I cast the long to JsonNumber
	Then the result should equal the JsonNumber 12

Scenario: Cast to double for json element backed value as a number
	Given the JsonElement backed JsonNumber 1.2
	When I cast the JsonNumber to double
	Then the result should equal the double 1.2

Scenario: Cast to double for dotnet backed value as a number
	Given the dotnet backed JsonNumber 1.2
	When I cast the JsonNumber to double
	Then the result should equal the double 1.2

Scenario: Cast from double for json element backed value as a number
	Given the double for 1.2
	When I cast the double to JsonNumber
	Then the result should equal the JsonNumber 1.2

Scenario: Cast to int for json element backed value as a number
	Given the JsonElement backed JsonNumber 12
	When I cast the JsonNumber to int
	Then the result should equal the int 12

Scenario: Cast to int for dotnet backed value as a number
	Given the dotnet backed JsonNumber 12
	When I cast the JsonNumber to int
	Then the result should equal the int 12

Scenario: Cast from int for json element backed value as a number
	Given the int for 12
	When I cast the int to JsonNumber
	Then the result should equal the JsonNumber 12

Scenario: Cast to float for json element backed value as a number
	Given the JsonElement backed JsonNumber 1.2
	When I cast the JsonNumber to float
	Then the result should equal the float 1.2

Scenario: Cast to float for dotnet backed value as a number
	Given the dotnet backed JsonNumber 1.2
	When I cast the JsonNumber to float
	Then the result should equal the float 1.2

Scenario: Cast from float for json element backed value as a number
	Given the float for 1.2
	When I cast the float to JsonNumber
	Then the result should equal the JsonNumber 1.2