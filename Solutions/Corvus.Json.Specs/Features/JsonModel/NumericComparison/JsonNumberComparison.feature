Feature: JsonNumberComparison
	Validate the less than and greater than operators

# JsonNumber
Scenario Outline: Less than for json element backed value as a number
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare it as less than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result |
	| 1         | 1     | false  |
	| 1.1       | 1.1   | false  |
	| 1.1       | 1     | false  |
	| 2         | 1     | false  |
	| 1.1       | 3     | true   |
	| null      | null  | false  |
	| null      | 1.1   | false  |

Scenario Outline: Less than for dotnet backed value as a number
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare it as less than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result |
	| 1         | 1     | false  |
	| 1.1       | 1.1   | false  |
	| 1.1       | 1     | false  |
	| 1         | 3     | true   |

# JsonNumber
Scenario Outline: Greater than for json element backed value as a number
	Given the JsonElement backed JsonNumber <jsonValue>
	When I compare it as greater than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result |
	| 1         | 1     | false  |
	| 1.1       | 1.1   | false  |
	| 1.1       | 1     | true   |
	| 2         | 1     | true   |
	| 1.1       | 3     | false  |
	| null      | null  | false  |
	| null      | 1.1   | false  |

Scenario Outline: Greater than for dotnet backed value as a number
	Given the dotnet backed JsonNumber <jsonValue>
	When I compare it as greater than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result |
	| 1         | 1     | false  |
	| 1.1       | 1.1   | false  |
	| 1.1       | 1     | true   |
	| 1         | 3     | false  |