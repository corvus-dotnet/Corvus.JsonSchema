Feature: JsonNumberEqualsNet80
	Validate the Json Equals operator, equality overrides and hashcode

# JsonNumber
Scenario Outline: Equals for json element backed value as a number
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonHalf  |
	| 1.1       | 1.1   | true   | JsonHalf  |
	| 1.1       | 1     | false  | JsonHalf  |
	| 1.1       | 3     | false  | JsonHalf  |
	| null      | null  | true   | JsonHalf  |
	| null      | 1.1   | false  | JsonHalf  |

Scenario Outline: Equals for dotnet backed value as a number
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the <TargetType> <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonHalf  |
	| 1.1       | 1.1   | true   | JsonHalf  |
	| 1.1       | 1     | false  | JsonHalf  |
	| 1         | 3     | false  | JsonHalf  |