Feature: JsonIntegerComparison
	Validate the less than and greater than operators

Scenario Outline: Less than for json element backed value as an integer
	Given the JsonElement backed JsonInteger <jsonValue>
	When I compare it as less than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value         | result |
	| 1         | 1             | false  |
	| 2         | 1             | false  |
	| 1         | 3             | true   |
	| 1         | long.MaxValue | true   |
	| 1         | long.MinValue | false  |
	| null      | null          | false  |
	| null      | 1             | false  |

Scenario Outline: Less than for dotnet backed value as an integer
	Given the dotnet backed JsonInteger <jsonValue>
	When I compare it as less than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value         | result |
	| 1         | 1             | false  |
	| 2         | 1             | false  |
	| 1         | 3             | true   |
	| 1         | long.MaxValue | true   |
	| 1         | long.MinValue | false  |

Scenario Outline: Greater than for json element backed value as a integer
	Given the JsonElement backed JsonInteger <jsonValue>
	When I compare it as greater than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value         | result |
	| 1         | 1             | false  |
	| 2         | 1             | true   |
	| 1         | 3             | false  |
	| 1         | long.MaxValue | false  |
	| 1         | long.MinValue | true   |
	| null      | null          | false  |
	| null      | 1             | false  |

Scenario Outline: Greater than for dotnet backed value as a integer
	Given the dotnet backed JsonInteger <jsonValue>
	When I compare it as greater than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value         | result |
	| 1         | 1             | false  |
	| 2         | 1             | true   |
	| 1         | 3             | false  |
	| 1         | long.MaxValue | false  |
	| 1         | long.MinValue | true   |
