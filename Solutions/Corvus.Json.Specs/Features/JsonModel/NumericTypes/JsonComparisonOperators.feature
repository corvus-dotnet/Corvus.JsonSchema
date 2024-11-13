Feature: JsonComparisonOperators
  In order to ensure the correctness of the numeric comparison operators
  As a developer
  I want to test the static comparison operators with various scenarios

Scenario Outline: Compare two dotnet backed values using the less than operator
	Given the dotnet backed <TargetType> <left>
	When I compare the <TargetType> as less than the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | true   | JsonInteger |
	| 20   | 10    | false  | JsonInteger |
	| 15   | 15    | false  | JsonInteger |
	| 10   | 20    | true   | JsonInt64   |
	| 20   | 10    | false  | JsonInt64   |
	| 15   | 15    | false  | JsonInt64   |
	| 10   | 20    | true   | JsonInt32   |
	| 20   | 10    | false  | JsonInt32   |
	| 15   | 15    | false  | JsonInt32   |
	| 10   | 20    | true   | JsonInt16   |
	| 20   | 10    | false  | JsonInt16   |
	| 15   | 15    | false  | JsonInt16   |`
	| 10   | 20    | true   | JsonSByte   |
	| 20   | 10    | false  | JsonSByte   |
	| 15   | 15    | false  | JsonSByte   |
	| 10   | 20    | true   | JsonUInt64  |
	| 20   | 10    | false  | JsonUInt64  |
	| 15   | 15    | false  | JsonUInt64  |
	| 10   | 20    | true   | JsonUInt32  |
	| 20   | 10    | false  | JsonUInt32  |
	| 15   | 15    | false  | JsonUInt32  |
	| 10   | 20    | true   | JsonUInt16  |
	| 20   | 10    | false  | JsonUInt16  |
	| 15   | 15    | false  | JsonUInt16  |
	| 10   | 20    | true   | JsonByte    |
	| 20   | 10    | false  | JsonByte    |
	| 15   | 15    | false  | JsonByte    |

Scenario Outline: Compare two dotnet backed values using the less than or equal operator
	Given the dotnet backed <TargetType> <left>
	When I compare the <TargetType> as less than or equal to the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | true   | JsonInteger |
	| 20   | 10    | false  | JsonInteger |
	| 15   | 15    | true   | JsonInteger |
	| 10   | 20    | true   | JsonInt64   |
	| 20   | 10    | false  | JsonInt64   |
	| 15   | 15    | true   | JsonInt64   |
	| 10   | 20    | true   | JsonInt32   |
	| 20   | 10    | false  | JsonInt32   |
	| 15   | 15    | true   | JsonInt32   |
	| 10   | 20    | true   | JsonInt16   |
	| 20   | 10    | false  | JsonInt16   |
	| 15   | 15    | true   | JsonInt16   |
	| 10   | 20    | true   | JsonSByte   |
	| 20   | 10    | false  | JsonSByte   |
	| 15   | 15    | true   | JsonSByte   |
	| 10   | 20    | true   | JsonUInt64  |
	| 20   | 10    | false  | JsonUInt64  |
	| 15   | 15    | true   | JsonUInt64  |
	| 10   | 20    | true   | JsonUInt32  |
	| 20   | 10    | false  | JsonUInt32  |
	| 15   | 15    | true   | JsonUInt32  |
	| 10   | 20    | true   | JsonUInt16  |
	| 20   | 10    | false  | JsonUInt16  |
	| 15   | 15    | true   | JsonUInt16  |
	| 10   | 20    | true   | JsonByte    |
	| 20   | 10    | false  | JsonByte    |
	| 15   | 15    | true   | JsonByte    |

Scenario Outline: Compare two dotnet backed values using the greater than operator
	Given the dotnet backed <TargetType> <left>
	When I compare the <TargetType> as greater than the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | false  | JsonInteger |
	| 20   | 10    | true   | JsonInteger |
	| 15   | 15    | false  | JsonInteger |
	| 10   | 20    | false  | JsonInt64   |
	| 20   | 10    | true   | JsonInt64   |
	| 15   | 15    | false  | JsonInt64   |
	| 10   | 20    | false  | JsonInt32   |
	| 20   | 10    | true   | JsonInt32   |
	| 15   | 15    | false  | JsonInt32   |
	| 10   | 20    | false  | JsonInt16   |
	| 20   | 10    | true   | JsonInt16   |
	| 15   | 15    | false  | JsonInt16   |
	| 10   | 20    | false  | JsonSByte   |
	| 20   | 10    | true   | JsonSByte   |
	| 15   | 15    | false  | JsonSByte   |
	| 10   | 20    | false  | JsonUInt64  |
	| 20   | 10    | true   | JsonUInt64  |
	| 15   | 15    | false  | JsonUInt64  |
	| 10   | 20    | false  | JsonUInt32  |
	| 20   | 10    | true   | JsonUInt32  |
	| 15   | 15    | false  | JsonUInt32  |
	| 10   | 20    | false  | JsonUInt16  |
	| 20   | 10    | true   | JsonUInt16  |
	| 15   | 15    | false  | JsonUInt16  |
	| 10   | 20    | false  | JsonByte    |
	| 20   | 10    | true   | JsonByte    |
	| 15   | 15    | false  | JsonByte    |

Scenario Outline: Compare two dotnet backed values using the greater than or equal operator
	Given the dotnet backed <TargetType> <left>
	When I compare the <TargetType> as greater than or equal to the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | false  | JsonInteger |
	| 20   | 10    | true   | JsonInteger |
	| 15   | 15    | true   | JsonInteger |
	| 10   | 20    | false  | JsonInt64   |
	| 20   | 10    | true   | JsonInt64   |
	| 15   | 15    | true   | JsonInt64   |
	| 10   | 20    | false  | JsonInt32   |
	| 20   | 10    | true   | JsonInt32   |
	| 15   | 15    | true   | JsonInt32   |
	| 10   | 20    | false  | JsonInt16   |
	| 20   | 10    | true   | JsonInt16   |
	| 15   | 15    | true   | JsonInt16   |
	| 10   | 20    | false  | JsonSByte   |
	| 20   | 10    | true   | JsonSByte   |
	| 15   | 15    | true   | JsonSByte   |
	| 10   | 20    | false  | JsonUInt64  |
	| 20   | 10    | true   | JsonUInt64  |
	| 15   | 15    | true   | JsonUInt64  |
	| 10   | 20    | false  | JsonUInt32  |
	| 20   | 10    | true   | JsonUInt32  |
	| 15   | 15    | true   | JsonUInt32  |
	| 10   | 20    | false  | JsonUInt16  |
	| 20   | 10    | true   | JsonUInt16  |
	| 15   | 15    | true   | JsonUInt16  |
	| 10   | 20    | false  | JsonByte    |
	| 20   | 10    | true   | JsonByte    |
	| 15   | 15    | true   | JsonByte    |

Scenario Outline: Compare two JsonElement backed values using the less than operator
	Given the JsonElement backed <TargetType> <left>
	When I compare the <TargetType> as less than the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | true   | JsonInteger |
	| 20   | 10    | false  | JsonInteger |
	| 15   | 15    | false  | JsonInteger |
	| 10   | 20    | true   | JsonInt64   |
	| 20   | 10    | false  | JsonInt64   |
	| 15   | 15    | false  | JsonInt64   |
	| 10   | 20    | true   | JsonInt32   |
	| 20   | 10    | false  | JsonInt32   |
	| 15   | 15    | false  | JsonInt32   |
	| 10   | 20    | true   | JsonInt16   |
	| 20   | 10    | false  | JsonInt16   |
	| 15   | 15    | false  | JsonInt16   |
	| 10   | 20    | true   | JsonSByte   |
	| 20   | 10    | false  | JsonSByte   |
	| 15   | 15    | false  | JsonSByte   |
	| 10   | 20    | true   | JsonUInt64  |
	| 20   | 10    | false  | JsonUInt64  |
	| 15   | 15    | false  | JsonUInt64  |
	| 10   | 20    | true   | JsonUInt32  |
	| 20   | 10    | false  | JsonUInt32  |
	| 15   | 15    | false  | JsonUInt32  |
	| 10   | 20    | true   | JsonUInt16  |
	| 20   | 10    | false  | JsonUInt16  |
	| 15   | 15    | false  | JsonUInt16  |
	| 10   | 20    | true   | JsonByte    |
	| 20   | 10    | false  | JsonByte    |
	| 15   | 15    | false  | JsonByte    |

Scenario Outline: Compare two JsonElement backed values using the less than or equal operator
	Given the JsonElement backed <TargetType> <left>
	When I compare the <TargetType> as less than or equal to the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | true   | JsonInteger |
	| 20   | 10    | false  | JsonInteger |
	| 15   | 15    | true   | JsonInteger |
	| 10   | 20    | true   | JsonInt64   |
	| 20   | 10    | false  | JsonInt64   |
	| 15   | 15    | true   | JsonInt64   |
	| 10   | 20    | true   | JsonInt32   |
	| 20   | 10    | false  | JsonInt32   |
	| 15   | 15    | true   | JsonInt32   |
	| 10   | 20    | true   | JsonInt16   |
	| 20   | 10    | false  | JsonInt16   |
	| 15   | 15    | true   | JsonInt16   |
	| 10   | 20    | true   | JsonSByte   |
	| 20   | 10    | false  | JsonSByte   |
	| 15   | 15    | true   | JsonSByte   |
	| 10   | 20    | true   | JsonUInt64  |
	| 20   | 10    | false  | JsonUInt64  |
	| 15   | 15    | true   | JsonUInt64  |
	| 10   | 20    | true   | JsonUInt32  |
	| 20   | 10    | false  | JsonUInt32  |
	| 15   | 15    | true   | JsonUInt32  |
	| 10   | 20    | true   | JsonUInt16  |
	| 20   | 10    | false  | JsonUInt16  |
	| 15   | 15    | true   | JsonUInt16  |
	| 10   | 20    | true   | JsonByte    |
	| 20   | 10    | false  | JsonByte    |
	| 15   | 15    | true   | JsonByte    |

Scenario Outline: Compare two JsonElement backed values using the greater than operator
	Given the JsonElement backed <TargetType> <left>
	When I compare the <TargetType> as greater than the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | false  | JsonInteger |
	| 20   | 10    | true   | JsonInteger |
	| 15   | 15    | false  | JsonInteger |
	| 10   | 20    | false  | JsonInt64   |
	| 20   | 10    | true   | JsonInt64   |
	| 15   | 15    | false  | JsonInt64   |
	| 10   | 20    | false  | JsonInt32   |
	| 20   | 10    | true   | JsonInt32   |
	| 15   | 15    | false  | JsonInt32   |
	| 10   | 20    | false  | JsonInt16   |
	| 20   | 10    | true   | JsonInt16   |
	| 15   | 15    | false  | JsonInt16   |
	| 10   | 20    | false  | JsonSByte   |
	| 20   | 10    | true   | JsonSByte   |
	| 15   | 15    | false  | JsonSByte   |
	| 10   | 20    | false  | JsonUInt64  |
	| 20   | 10    | true   | JsonUInt64  |
	| 15   | 15    | false  | JsonUInt64  |
	| 10   | 20    | false  | JsonUInt32  |
	| 20   | 10    | true   | JsonUInt32  |
	| 15   | 15    | false  | JsonUInt32  |
	| 10   | 20    | false  | JsonUInt16  |
	| 20   | 10    | true   | JsonUInt16  |
	| 15   | 15    | false  | JsonUInt16  |
	| 10   | 20    | false  | JsonByte    |
	| 20   | 10    | true   | JsonByte    |
	| 15   | 15    | false  | JsonByte    |

Scenario Outline: Compare two JsonElement backed values using the greater than or equal operator
	Given the JsonElement backed <TargetType> <left>
	When I compare the <TargetType> as greater than or equal to the <TargetType> <right>
	Then the result should be <result>

Examples:
	| left | right | result | TargetType  |
	| 10   | 20    | false  | JsonInteger |
	| 20   | 10    | true   | JsonInteger |
	| 15   | 15    | true   | JsonInteger |
	| 10   | 20    | false  | JsonInt64   |
	| 20   | 10    | true   | JsonInt64   |
	| 15   | 15    | true   | JsonInt64   |
	| 10   | 20    | false  | JsonInt32   |
	| 20   | 10    | true   | JsonInt32   |
	| 15   | 15    | true   | JsonInt32   |
	| 10   | 20    | false  | JsonInt16   |
	| 20   | 10    | true   | JsonInt16   |
	| 15   | 15    | true   | JsonInt16   |
	| 10   | 20    | false  | JsonSByte   |
	| 20   | 10    | true   | JsonSByte   |
	| 15   | 15    | true   | JsonSByte   |
	| 10   | 20    | false  | JsonUInt64  |
	| 20   | 10    | true   | JsonUInt64  |
	| 15   | 15    | true   | JsonUInt64  |
	| 10   | 20    | false  | JsonUInt32  |
	| 20   | 10    | true   | JsonUInt32  |
	| 15   | 15    | true   | JsonUInt32  |
	| 10   | 20    | false  | JsonUInt16  |
	| 20   | 10    | true   | JsonUInt16  |
	| 15   | 15    | true   | JsonUInt16  |
	| 10   | 20    | false  | JsonByte    |
	| 20   | 10    | true   | JsonByte    |
	| 15   | 15    | true   | JsonByte    |
