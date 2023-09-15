Feature: JsonIntegerCast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as a integer
	Given the JsonElement backed JsonInteger 12
	When I cast the JsonInteger to JsonAny
	Then the result should equal the JsonAny 12

Scenario: Cast to JsonAny for dotnet backed value as a integer
	Given the dotnet backed JsonInteger 12
	When I cast the JsonInteger to JsonAny
	Then the result should equal the JsonAny 12

Scenario: Cast to JsonNumber for json element backed value as a integer
	Given the JsonElement backed JsonInteger 12
	When I cast the JsonInteger to JsonNumber
	Then the result should equal the JsonNumber 12

Scenario: Cast to JsonNumber for dotnet backed value as a integer
	Given the dotnet backed JsonInteger 12
	When I cast the JsonInteger to JsonNumber
	Then the result should equal the JsonNumber 12

Scenario: Cast from JsonNumber for dotnet backed value as a integer
	Given the dotnet backed JsonNumber 12
	When I cast the JsonNumber to JsonInteger
	Then the result should equal the JsonInteger 12

Scenario: Cast from JsonNumber for json element backed value as a integer
	Given the JsonElement backed JsonNumber 12
	When I cast the JsonNumber to JsonInteger
	Then the result should equal the JsonInteger 12

Scenario: Cast to long for json element backed value as a integer
	Given the JsonElement backed JsonInteger 12
	When I cast the JsonInteger to long
	Then the result should equal the long 12

Scenario: Cast to long for dotnet backed value as a integer
	Given the dotnet backed JsonInteger 12
	When I cast the JsonInteger to long
	Then the result should equal the long 12

Scenario: Cast from long for json element backed value as a integer
	Given the long for 12
	When I cast the long to JsonInteger
	Then the result should equal the JsonInteger 12

Scenario: Cast to double for json element backed value as a integer
	Given the JsonElement backed JsonInteger 12
	When I cast the JsonInteger to double
	Then the result should equal the double 12

Scenario: Cast to double for dotnet backed value as a integer
	Given the dotnet backed JsonInteger 12
	When I cast the JsonInteger to double
	Then the result should equal the double 12

Scenario: Cast from double for json element backed value as a integer
	Given the double for 12
	When I cast the double to JsonInteger
	Then the result should equal the JsonInteger 12

Scenario: Cast to int for json element backed value as a integer
	Given the JsonElement backed JsonInteger 12
	When I cast the JsonInteger to int
	Then the result should equal the int 12

Scenario: Cast to int for dotnet backed value as a integer
	Given the dotnet backed JsonInteger 12
	When I cast the JsonInteger to int
	Then the result should equal the int 12

Scenario: Cast from int for json element backed value as a integer
	Given the int for 12
	When I cast the int to JsonInteger
	Then the result should equal the JsonInteger 12

Scenario: Cast to float for json element backed value as a integer
	Given the JsonElement backed JsonInteger 12
	When I cast the JsonInteger to float
	Then the result should equal the float 12

Scenario: Cast to float for dotnet backed value as a integer
	Given the dotnet backed JsonInteger 12
	When I cast the JsonInteger to float
	Then the result should equal the float 12

Scenario: Cast from float for json element backed value as a integer
	Given the float for 12
	When I cast the float to JsonInteger
	Then the result should equal the JsonInteger 12