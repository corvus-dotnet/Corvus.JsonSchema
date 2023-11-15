Feature: JsonIpV6Cast
	Validate the Json cast operators

Scenario: Cast to JsonAny for json element backed value as an ipV6
	Given the JsonElement backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to JsonAny
	Then the result should equal the JsonAny "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast to JsonAny for dotnet backed value as an ipV6
	Given the dotnet backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to JsonAny
	Then the result should equal the JsonAny "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast to JsonString for json element backed value as an ipV6
	Given the JsonElement backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to JsonString
	Then the result should equal the JsonString "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast to JsonString for dotnet backed value as an ipV6
	Given the dotnet backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to JsonString
	Then the result should equal the JsonString "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast from JsonString for json element backed value as an ipV6
	Given the JsonString for "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonString to JsonIpV6
	Then the result should equal the JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast to IPAddress for json element backed value as an ipV6
	Given the JsonElement backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to IPAddress
	Then the result should equal the IPAddress "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast to IPAddress for dotnet backed value as an ipV6
	Given the dotnet backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to IPAddress
	Then the result should equal the IPAddress "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast from IPAddress for json element backed value as an ipV6
	Given the IPAddress for "::ffff:192.168.0.1"
	When I cast the IPAddress to JsonIpV6
	Then the result should equal the JsonIpV6 "::ffff:192.168.0.1"

Scenario: Cast to string for json element backed value as an ipV6
	Given the JsonElement backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to string
	Then the result should equal the string "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast to string for dotnet backed value as an ipV6
	Given the dotnet backed JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the JsonIpV6 to string
	Then the result should equal the string "0:0:0:0:0:ffff:c0a8:0001"

Scenario: Cast from string for json element backed value as an ipV6
	Given the string for "0:0:0:0:0:ffff:c0a8:0001"
	When I cast the string to JsonIpV6
	Then the result should equal the JsonIpV6 "0:0:0:0:0:ffff:c0a8:0001"

