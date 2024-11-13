Feature: JsonNumberComparison
	Validate the less than and greater than operators

# <TargetType>
Scenario Outline: Less than for json element backed value as a number
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | false  | JsonNumber  |
	| 1.1       | 1.1   | false  | JsonNumber  |
	| 1.1       | 1     | false  | JsonNumber  |
	| 2         | 1     | false  | JsonNumber  |
	| 1.1       | 3     | true   | JsonNumber  |
	| null      | null  | false  | JsonNumber  |
	| null      | 1.1   | false  | JsonNumber  |
	| 1         | 1     | false  | JsonSingle  |
	| 1.1       | 1.1   | false  | JsonSingle  |
	| 1.1       | 1     | false  | JsonSingle  |
	| 2         | 1     | false  | JsonSingle  |
	| 1.1       | 3     | true   | JsonSingle  |
	| null      | null  | false  | JsonSingle  |
	| null      | 1.1   | false  | JsonSingle  |
	| 1         | 1     | false  | JsonHalf    |
	| 1.1       | 1.1   | false  | JsonHalf    |
	| 1.1       | 1     | false  | JsonHalf    |
	| 2         | 1     | false  | JsonHalf    |
	| 1.1       | 3     | true   | JsonHalf    |
	| null      | null  | false  | JsonHalf    |
	| null      | 1.1   | false  | JsonHalf    |
	| 1         | 1     | false  | JsonDouble  |
	| 1.1       | 1.1   | false  | JsonDouble  |
	| 1.1       | 1     | false  | JsonDouble  |
	| 2         | 1     | false  | JsonDouble  |
	| 1.1       | 3     | true   | JsonDouble  |
	| null      | null  | false  | JsonDouble  |
	| null      | 1.1   | false  | JsonDouble  |
	| 1         | 1     | false  | JsonDecimal |
	| 1.1       | 1.1   | false  | JsonDecimal |
	| 1.1       | 1     | false  | JsonDecimal |
	| 2         | 1     | false  | JsonDecimal |
	| 1.1       | 3     | true   | JsonDecimal |
	| null      | null  | false  | JsonDecimal |
	| null      | 1.1   | false  | JsonDecimal |

Scenario Outline: Less than for dotnet backed value as a number
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | false  | JsonNumber  |
	| 1.1       | 1.1   | false  | JsonNumber  |
	| 1.1       | 1     | false  | JsonNumber  |
	| 1         | 3     | true   | JsonNumber  |
	| 1         | 1     | false  | JsonSingle  |
	| 1.1       | 1.1   | false  | JsonSingle  |
	| 1.1       | 1     | false  | JsonSingle  |
	| 1         | 3     | true   | JsonSingle  |
	| 1         | 1     | false  | JsonHalf    |
	| 1.1       | 1.1   | false  | JsonHalf    |
	| 1.1       | 1     | false  | JsonHalf    |
	| 1         | 3     | true   | JsonHalf    |
	| 1         | 1     | false  | JsonDouble  |
	| 1.1       | 1.1   | false  | JsonDouble  |
	| 1.1       | 1     | false  | JsonDouble  |
	| 1         | 3     | true   | JsonDouble  |
	| 1         | 1     | false  | JsonDecimal |
	| 1.1       | 1.1   | false  | JsonDecimal |
	| 1.1       | 1     | false  | JsonDecimal |
	| 1         | 3     | true   | JsonDecimal |

Scenario Outline: Less than or equal to for json element backed value as a number
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than or equal to the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonNumber  |
	| 1.1       | 1.1   | true   | JsonNumber  |
	| 1.1       | 1     | false  | JsonNumber  |
	| 2         | 1     | false  | JsonNumber  |
	| 1.1       | 3     | true   | JsonNumber  |
	| null      | null  | false  | JsonNumber  |
	| null      | 1.1   | false  | JsonNumber  |
	| 1         | 1     | true   | JsonHalf    |
	| 1.1       | 1.1   | true   | JsonHalf    |
	| 1.1       | 1     | false  | JsonHalf    |
	| 2         | 1     | false  | JsonHalf    |
	| 1.1       | 3     | true   | JsonHalf    |
	| null      | null  | false  | JsonHalf    |
	| null      | 1.1   | false  | JsonHalf    |
	| 1         | 1     | true   | JsonSingle  |
	| 1.1       | 1.1   | true   | JsonSingle  |
	| 1.1       | 1     | false  | JsonSingle  |
	| 2         | 1     | false  | JsonSingle  |
	| 1.1       | 3     | true   | JsonSingle  |
	| null      | null  | false  | JsonSingle  |
	| null      | 1.1   | false  | JsonSingle  |
	| 1         | 1     | true   | JsonDouble  |
	| 1.1       | 1.1   | true   | JsonDouble  |
	| 1.1       | 1     | false  | JsonDouble  |
	| 2         | 1     | false  | JsonDouble  |
	| 1.1       | 3     | true   | JsonDouble  |
	| null      | null  | false  | JsonDouble  |
	| null      | 1.1   | false  | JsonDouble  |
	| 1         | 1     | true   | JsonDecimal |
	| 1.1       | 1.1   | true   | JsonDecimal |
	| 1.1       | 1     | false  | JsonDecimal |
	| 2         | 1     | false  | JsonDecimal |
	| 1.1       | 3     | true   | JsonDecimal |
	| null      | null  | false  | JsonDecimal |
	| null      | 1.1   | false  | JsonDecimal |

Scenario Outline: Less than or equal to for dotnet backed value as a number
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than or equal to the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonNumber  |
	| 1.1       | 1.1   | true   | JsonNumber  |
	| 1.1       | 1     | false  | JsonNumber  |
	| 1         | 3     | true   | JsonNumber  |
	| 1         | 1     | true   | JsonHalf    |
	| 1.1       | 1.1   | true   | JsonHalf    |
	| 1.1       | 1     | false  | JsonHalf    |
	| 1         | 3     | true   | JsonHalf    |
	| 1         | 1     | true   | JsonSingle  |
	| 1.1       | 1.1   | true   | JsonSingle  |
	| 1.1       | 1     | false  | JsonSingle  |
	| 1         | 3     | true   | JsonSingle  |
	| 1         | 1     | true   | JsonDouble  |
	| 1.1       | 1.1   | true   | JsonDouble  |
	| 1.1       | 1     | false  | JsonDouble  |
	| 1         | 3     | true   | JsonDouble  |
	| 1         | 1     | true   | JsonDecimal |
	| 1.1       | 1.1   | true   | JsonDecimal |
	| 1.1       | 1     | false  | JsonDecimal |
	| 1         | 3     | true   | JsonDecimal |

# <TargetType>
Scenario Outline: Greater than for json element backed value as a number
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | false  | JsonNumber  |
	| 1.1       | 1.1   | false  | JsonNumber  |
	| 1.1       | 1     | true   | JsonNumber  |
	| 2         | 1     | true   | JsonNumber  |
	| 1.1       | 3     | false  | JsonNumber  |
	| null      | null  | false  | JsonNumber  |
	| null      | 1.1   | false  | JsonNumber  |
	| 1         | 1     | false  | JsonSingle  |
	| 1.1       | 1.1   | false  | JsonSingle  |
	| 1.1       | 1     | true   | JsonSingle  |
	| 2         | 1     | true   | JsonSingle  |
	| 1.1       | 3     | false  | JsonSingle  |
	| null      | null  | false  | JsonSingle  |
	| null      | 1.1   | false  | JsonSingle  |
	| 1         | 1     | false  | JsonHalf    |
	| 1.1       | 1.1   | false  | JsonHalf    |
	| 1.1       | 1     | true   | JsonHalf    |
	| 2         | 1     | true   | JsonHalf    |
	| 1.1       | 3     | false  | JsonHalf    |
	| null      | null  | false  | JsonHalf    |
	| null      | 1.1   | false  | JsonHalf    |
	| 1         | 1     | false  | JsonDouble  |
	| 1.1       | 1.1   | false  | JsonDouble  |
	| 1.1       | 1     | true   | JsonDouble  |
	| 2         | 1     | true   | JsonDouble  |
	| 1.1       | 3     | false  | JsonDouble  |
	| null      | null  | false  | JsonDouble  |
	| null      | 1.1   | false  | JsonDouble  |
	| 1         | 1     | false  | JsonDecimal |
	| 1.1       | 1.1   | false  | JsonDecimal |
	| 1.1       | 1     | true   | JsonDecimal |
	| 2         | 1     | true   | JsonDecimal |
	| 1.1       | 3     | false  | JsonDecimal |
	| null      | null  | false  | JsonDecimal |
	| null      | 1.1   | false  | JsonDecimal |

Scenario Outline: Greater than for dotnet backed value as a number
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | false  | JsonNumber  |
	| 1.1       | 1.1   | false  | JsonNumber  |
	| 1.1       | 1     | true   | JsonNumber  |
	| 1         | 3     | false  | JsonNumber  |
	| 1         | 1     | false  | JsonHalf    |
	| 1.1       | 1.1   | false  | JsonHalf    |
	| 1.1       | 1     | true   | JsonHalf    |
	| 1         | 3     | false  | JsonHalf    |
	| 1         | 1     | false  | JsonSingle  |
	| 1.1       | 1.1   | false  | JsonSingle  |
	| 1.1       | 1     | true   | JsonSingle  |
	| 1         | 3     | false  | JsonSingle  |
	| 1         | 1     | false  | JsonDouble  |
	| 1.1       | 1.1   | false  | JsonDouble  |
	| 1.1       | 1     | true   | JsonDouble  |
	| 1         | 3     | false  | JsonDouble  |
	| 1         | 1     | false  | JsonDecimal |
	| 1.1       | 1.1   | false  | JsonDecimal |
	| 1.1       | 1     | true   | JsonDecimal |
	| 1         | 3     | false  | JsonDecimal |

Scenario Outline: Greater than or equal to for json element backed value as a number
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than or equal to the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonNumber  |
	| 1.1       | 1.1   | true   | JsonNumber  |
	| 1.1       | 1     | true   | JsonNumber  |
	| 2         | 1     | true   | JsonNumber  |
	| 1.1       | 3     | false  | JsonNumber  |
	| null      | null  | false  | JsonNumber  |
	| null      | 1.1   | false  | JsonNumber  |
	| 1         | 1     | true   | JsonHalf    |
	| 1.1       | 1.1   | true   | JsonHalf    |
	| 1.1       | 1     | true   | JsonHalf    |
	| 2         | 1     | true   | JsonHalf    |
	| 1.1       | 3     | false  | JsonHalf    |
	| null      | null  | false  | JsonHalf    |
	| null      | 1.1   | false  | JsonHalf    |
	| 1         | 1     | true   | JsonSingle  |
	| 1.1       | 1.1   | true   | JsonSingle  |
	| 1.1       | 1     | true   | JsonSingle  |
	| 2         | 1     | true   | JsonSingle  |
	| 1.1       | 3     | false  | JsonSingle  |
	| null      | null  | false  | JsonSingle  |
	| null      | 1.1   | false  | JsonSingle  |
	| 1         | 1     | true   | JsonDouble  |
	| 1.1       | 1.1   | true   | JsonDouble  |
	| 1.1       | 1     | true   | JsonDouble  |
	| 2         | 1     | true   | JsonDouble  |
	| 1.1       | 3     | false  | JsonDouble  |
	| null      | null  | false  | JsonDouble  |
	| null      | 1.1   | false  | JsonDouble  |
	| 1         | 1     | true   | JsonDecimal |
	| 1.1       | 1.1   | true   | JsonDecimal |
	| 1.1       | 1     | true   | JsonDecimal |
	| 2         | 1     | true   | JsonDecimal |
	| 1.1       | 3     | false  | JsonDecimal |
	| null      | null  | false  | JsonDecimal |
	| null      | 1.1   | false  | JsonDecimal |

Scenario Outline: Greater than or euqal to for dotnet backed value as a number
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than or equal to the number <value>
	Then the result should be <result>

Examples:
	| jsonValue | value | result | TargetType  |
	| 1         | 1     | true   | JsonNumber  |
	| 1.1       | 1.1   | true   | JsonNumber  |
	| 1.1       | 1     | true   | JsonNumber  |
	| 1         | 3     | false  | JsonNumber  |
	| 1         | 1     | true   | JsonHalf    |
	| 1.1       | 1.1   | true   | JsonHalf    |
	| 1.1       | 1     | true   | JsonHalf    |
	| 1         | 3     | false  | JsonHalf    |
	| 1         | 1     | true   | JsonSingle  |
	| 1.1       | 1.1   | true   | JsonSingle  |
	| 1.1       | 1     | true   | JsonSingle  |
	| 1         | 3     | false  | JsonSingle  |
	| 1         | 1     | true   | JsonDouble  |
	| 1.1       | 1.1   | true   | JsonDouble  |
	| 1.1       | 1     | true   | JsonDouble  |
	| 1         | 3     | false  | JsonDouble  |
	| 1         | 1     | true   | JsonDecimal |
	| 1.1       | 1.1   | true   | JsonDecimal |
	| 1.1       | 1     | true   | JsonDecimal |
	| 1         | 3     | false  | JsonDecimal |