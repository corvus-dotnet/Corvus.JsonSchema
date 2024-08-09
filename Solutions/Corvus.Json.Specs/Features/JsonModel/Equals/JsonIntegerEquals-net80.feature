Feature: JsonIntegerEqualsNet80
	Validate the Json Equals operator, equality overrides and hashcode

# JsonInteger
Scenario Outline: Equals for json element backed value as a integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInt128  |
	| 1         | 3     | false  | JsonInt128  |
	| null      | null  | true   | JsonInt128  |
	| null      | 1     | false  | JsonInt128  |
	| 1         | 1     | true   | JsonUInt128 |
	| 1         | 3     | false  | JsonUInt128 |
	| null      | null  | true   | JsonUInt128 |
	| null      | 1     | false  | JsonUInt128 |

Scenario Outline: Equals for dotnet backed value as a integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonInt128  |
	| 1         | 3     | false  | JsonInt128  |
	| 1         | 1     | true   | JsonUInt128 |
	| 1         | 3     | false  | JsonUInt128 |