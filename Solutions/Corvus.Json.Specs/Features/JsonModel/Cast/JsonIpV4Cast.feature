Feature: JsonIpV4Cast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an ipV4
	Given the JsonElement backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to JsonAny
	Then the result should equal the JsonAny "192.168.0.1"

Scenario: Cast to JsonAny for dotnet backed value as an ipV4
	Given the dotnet backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to JsonAny
	Then the result should equal the JsonAny "192.168.0.1"

Scenario: Cast to JsonString for json element backed value as an ipV4
	Given the JsonElement backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to JsonString
	Then the result should equal the JsonString "192.168.0.1"

Scenario: Cast to JsonString for dotnet backed value as an ipV4
	Given the dotnet backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to JsonString
	Then the result should equal the JsonString "192.168.0.1"

Scenario: Cast from JsonString for json element backed value as an ipV4
	Given the JsonString for "192.168.0.1"
	When I cast the JsonString to JsonIpV4
	Then the result should equal the JsonIpV4 "192.168.0.1"

Scenario: Cast to IPAddress for json element backed value as an ipV4
	Given the JsonElement backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to IPAddress
	Then the result should equal the IPAddress "192.168.0.1"

Scenario: Cast to IPAddress for dotnet backed value as an ipV4
	Given the dotnet backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to IPAddress
	Then the result should equal the IPAddress "192.168.0.1"

Scenario: Cast from IPAddress for json element backed value as an ipV4
	Given the IPAddress for "192.168.0.1"
	When I cast the IPAddress to JsonIpV4
	Then the result should equal the JsonIpV4 "192.168.0.1"

Scenario: Cast to string for json element backed value as an ipV4
	Given the JsonElement backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to string
	Then the result should equal the string "192.168.0.1"

Scenario: Cast to string for dotnet backed value as an ipV4
	Given the dotnet backed JsonIpV4 "192.168.0.1"
	When I cast the JsonIpV4 to string
	Then the result should equal the string "192.168.0.1"

Scenario: Cast from string for json element backed value as an ipV4
	Given the string for "192.168.0.1"
	When I cast the string to JsonIpV4
	Then the result should equal the JsonIpV4 "192.168.0.1"

