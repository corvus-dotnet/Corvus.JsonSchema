Feature: JsonIntegerComparison
	Validate the less than and greater than operators

Scenario Outline: Less than for json element backed value as an integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | false  | JsonInteger |
	| 2         | 1               | false  | JsonInteger |
	| 1         | 3               | true   | JsonInteger |
	| 1         | long.MaxValue   | true   | JsonInteger |
	| 1         | long.MinValue   | false  | JsonInteger |
	| null      | null            | false  | JsonInteger |
	| null      | 1               | false  | JsonInteger |
	| 1         | 1               | false  | JsonInt64   |
	| 2         | 1               | false  | JsonInt64   |
	| 1         | 3               | true   | JsonInt64   |
	| 1         | long.MaxValue   | true   | JsonInt64   |
	| 1         | long.MinValue   | false  | JsonInt64   |
	| null      | null            | false  | JsonInt64   |
	| null      | 1               | false  | JsonInt64   |
	| 1         | 1               | false  | JsonInt32   |
	| 2         | 1               | false  | JsonInt32   |
	| 1         | 3               | true   | JsonInt32   |
	| 1         | int.MaxValue    | true   | JsonInt32   |
	| 1         | int.MinValue    | false  | JsonInt32   |
	| null      | null            | false  | JsonInt32   |
	| null      | 1               | false  | JsonInt32   |
	| 1         | 1               | false  | JsonInt16   |
	| 2         | 1               | false  | JsonInt16   |
	| 1         | 3               | true   | JsonInt16   |
	| 1         | short.MaxValue  | true   | JsonInt16   |
	| 1         | short.MinValue  | false  | JsonInt16   |
	| null      | null            | false  | JsonInt16   |
	| null      | 1               | false  | JsonInt16   |
	| 1         | 1               | false  | JsonSByte   |
	| 2         | 1               | false  | JsonSByte   |
	| 1         | 3               | true   | JsonSByte   |
	| 1         | sbyte.MaxValue  | true   | JsonSByte   |
	| 1         | sbyte.MinValue  | false  | JsonSByte   |
	| null      | null            | false  | JsonSByte   |
	| null      | 1               | false  | JsonSByte   |
	| 1         | 1               | false  | JsonUInt64  |
	| 2         | 1               | false  | JsonUInt64  |
	| 1         | 3               | true   | JsonUInt64  |
	| 1         | ulong.MaxValue  | true   | JsonUInt64  |
	| 1         | ulong.MinValue  | false  | JsonUInt64  |
	| null      | null            | false  | JsonUInt64  |
	| null      | 1               | false  | JsonUInt64  |
	| 1         | 1               | false  | JsonUInt32  |
	| 2         | 1               | false  | JsonUInt32  |
	| 1         | 3               | true   | JsonUInt32  |
	| 1         | uint.MaxValue   | true   | JsonUInt32  |
	| 1         | uint.MinValue   | false  | JsonUInt32  |
	| null      | null            | false  | JsonUInt32  |
	| null      | 1               | false  | JsonUInt32  |
	| 1         | 1               | false  | JsonUInt16  |
	| 2         | 1               | false  | JsonUInt16  |
	| 1         | 3               | true   | JsonUInt16  |
	| 1         | ushort.MaxValue | true   | JsonUInt16  |
	| 1         | ushort.MinValue | false  | JsonUInt16  |
	| null      | null            | false  | JsonUInt16  |
	| null      | 1               | false  | JsonUInt16  |
	| 1         | 1               | false  | JsonByte    |
	| 2         | 1               | false  | JsonByte    |
	| 1         | 3               | true   | JsonByte    |
	| 1         | byte.MaxValue   | true   | JsonByte    |
	| 1         | byte.MinValue   | false  | JsonByte    |
	| null      | null            | false  | JsonByte    |
	| null      | 1               | false  | JsonByte    |

Scenario Outline: Less than for dotnet backed value as an integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | false  | JsonInteger |
	| 2         | 1               | false  | JsonInteger |
	| 1         | 3               | true   | JsonInteger |
	| 1         | long.MaxValue   | true   | JsonInteger |
	| 1         | long.MinValue   | false  | JsonInteger |
	| 1         | 1               | false  | JsonInt64   |
	| 2         | 1               | false  | JsonInt64   |
	| 1         | 3               | true   | JsonInt64   |
	| 1         | long.MaxValue   | true   | JsonInt64   |
	| 1         | long.MinValue   | false  | JsonInt64   |
	| 1         | 1               | false  | JsonInt32   |
	| 2         | 1               | false  | JsonInt32   |
	| 1         | 3               | true   | JsonInt32   |
	| 1         | int.MaxValue    | true   | JsonInt32   |
	| 1         | int.MinValue    | false  | JsonInt32   |
	| 1         | 1               | false  | JsonInt16   |
	| 2         | 1               | false  | JsonInt16   |
	| 1         | 3               | true   | JsonInt16   |
	| 1         | short.MaxValue  | true   | JsonInt16   |
	| 1         | short.MinValue  | false  | JsonInt16   |
	| 1         | 1               | false  | JsonSByte   |
	| 2         | 1               | false  | JsonSByte   |
	| 1         | 3               | true   | JsonSByte   |
	| 1         | sbyte.MaxValue  | true   | JsonSByte   |
	| 1         | sbyte.MinValue  | false  | JsonSByte   |
	| 1         | 1               | false  | JsonUInt64  |
	| 2         | 1               | false  | JsonUInt64  |
	| 1         | 3               | true   | JsonUInt64  |
	| 1         | ulong.MaxValue  | true   | JsonUInt64  |
	| 1         | ulong.MinValue  | false  | JsonUInt64  |
	| 1         | 1               | false  | JsonUInt32  |
	| 2         | 1               | false  | JsonUInt32  |
	| 1         | 3               | true   | JsonUInt32  |
	| 1         | uint.MaxValue   | true   | JsonUInt32  |
	| 1         | uint.MinValue   | false  | JsonUInt32  |
	| 1         | 1               | false  | JsonUInt16  |
	| 2         | 1               | false  | JsonUInt16  |
	| 1         | 3               | true   | JsonUInt16  |
	| 1         | ushort.MaxValue | true   | JsonUInt16  |
	| 1         | ushort.MinValue | false  | JsonUInt16  |
	| 1         | 1               | false  | JsonByte    |
	| 2         | 1               | false  | JsonByte    |
	| 1         | 3               | true   | JsonByte    |
	| 1         | byte.MaxValue   | true   | JsonByte    |
	| 1         | byte.MinValue   | false  | JsonByte    |

Scenario Outline: Greater than for json element backed value as a integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | false  | JsonInteger |
	| 2         | 1               | true   | JsonInteger |
	| 1         | 3               | false  | JsonInteger |
	| 1         | long.MaxValue   | false  | JsonInteger |
	| 1         | long.MinValue   | true   | JsonInteger |
	| null      | null            | false  | JsonInteger |
	| null      | 1               | false  | JsonInteger |
	| 1         | 1               | false  | JsonInt64   |
	| 2         | 1               | true   | JsonInt64   |
	| 1         | 3               | false  | JsonInt64   |
	| 1         | long.MaxValue   | false  | JsonInt64   |
	| 1         | long.MinValue   | true   | JsonInt64   |
	| null      | null            | false  | JsonInt64   |
	| null      | 1               | false  | JsonInt64   |
	| 1         | 1               | false  | JsonInt32   |
	| 2         | 1               | true   | JsonInt32   |
	| 1         | 3               | false  | JsonInt32   |
	| 1         | int.MaxValue    | false  | JsonInt32   |
	| 1         | int.MinValue    | true   | JsonInt32   |
	| null      | null            | false  | JsonInt32   |
	| null      | 1               | false  | JsonInt32   |
	| 1         | 1               | false  | JsonInt16   |
	| 2         | 1               | true   | JsonInt16   |
	| 1         | 3               | false  | JsonInt16   |
	| 1         | short.MaxValue  | false  | JsonInt16   |
	| 1         | short.MinValue  | true   | JsonInt16   |
	| null      | null            | false  | JsonInt16   |
	| null      | 1               | false  | JsonInt16   |
	| 1         | 1               | false  | JsonSByte   |
	| 2         | 1               | true   | JsonSByte   |
	| 1         | 3               | false  | JsonSByte   |
	| 1         | sbyte.MaxValue  | false  | JsonSByte   |
	| 1         | sbyte.MinValue  | true   | JsonSByte   |
	| null      | null            | false  | JsonSByte   |
	| null      | 1               | false  | JsonSByte   |
	| 1         | 1               | false  | JsonUInt64  |
	| 2         | 1               | true   | JsonUInt64  |
	| 1         | 3               | false  | JsonUInt64  |
	| 1         | ulong.MaxValue  | false  | JsonUInt64  |
	| 1         | ulong.MinValue  | true   | JsonUInt64  |
	| null      | null            | false  | JsonUInt64  |
	| null      | 1               | false  | JsonUInt64  |
	| 1         | 1               | false  | JsonUInt32  |
	| 2         | 1               | true   | JsonUInt32  |
	| 1         | 3               | false  | JsonUInt32  |
	| 1         | uint.MaxValue   | false  | JsonUInt32  |
	| 1         | uint.MinValue   | true   | JsonUInt32  |
	| null      | null            | false  | JsonUInt32  |
	| null      | 1               | false  | JsonUInt32  |
	| 1         | 1               | false  | JsonUInt16  |
	| 2         | 1               | true   | JsonUInt16  |
	| 1         | 3               | false  | JsonUInt16  |
	| 1         | ushort.MaxValue | false  | JsonUInt16  |
	| 1         | ushort.MinValue | true   | JsonUInt16  |
	| null      | null            | false  | JsonUInt16  |
	| null      | 1               | false  | JsonUInt16  |
	| 1         | 1               | false  | JsonByte    |
	| 2         | 1               | true   | JsonByte    |
	| 1         | 3               | false  | JsonByte    |
	| 1         | byte.MaxValue   | false  | JsonByte    |
	| 1         | byte.MinValue   | true   | JsonByte    |
	| null      | null            | false  | JsonByte    |
	| null      | 1               | false  | JsonByte    |

Scenario Outline: Greater than for dotnet backed value as a integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | false  | JsonInteger |
	| 2         | 1               | true   | JsonInteger |
	| 1         | 3               | false  | JsonInteger |
	| 1         | long.MaxValue   | false  | JsonInteger |
	| 1         | long.MinValue   | true   | JsonInteger |
	| 1         | 1               | false  | JsonInt64   |
	| 2         | 1               | true   | JsonInt64   |
	| 1         | 3               | false  | JsonInt64   |
	| 1         | long.MaxValue   | false  | JsonInt64   |
	| 1         | long.MinValue   | true   | JsonInt64   |
	| 1         | 1               | false  | JsonInt32   |
	| 2         | 1               | true   | JsonInt32   |
	| 1         | 3               | false  | JsonInt32   |
	| 1         | int.MaxValue    | false  | JsonInt32   |
	| 1         | int.MinValue    | true   | JsonInt32   |
	| 1         | 1               | false  | JsonInt16   |
	| 2         | 1               | true   | JsonInt16   |
	| 1         | 3               | false  | JsonInt16   |
	| 1         | short.MaxValue  | false  | JsonInt16   |
	| 1         | short.MinValue  | true   | JsonInt16   |
	| 1         | 1               | false  | JsonSByte   |
	| 2         | 1               | true   | JsonSByte   |
	| 1         | 3               | false  | JsonSByte   |
	| 1         | sbyte.MaxValue  | false  | JsonSByte   |
	| 1         | sbyte.MinValue  | true   | JsonSByte   |
	| 1         | 1               | false  | JsonUInt64  |
	| 2         | 1               | true   | JsonUInt64  |
	| 1         | 3               | false  | JsonUInt64  |
	| 1         | ulong.MaxValue  | false  | JsonUInt64  |
	| 1         | ulong.MinValue  | true   | JsonUInt64  |
	| 1         | 1               | false  | JsonUInt32  |
	| 2         | 1               | true   | JsonUInt32  |
	| 1         | 3               | false  | JsonUInt32  |
	| 1         | uint.MaxValue   | false  | JsonUInt32  |
	| 1         | uint.MinValue   | true   | JsonUInt32  |
	| 1         | 1               | false  | JsonUInt16  |
	| 2         | 1               | true   | JsonUInt16  |
	| 1         | 3               | false  | JsonUInt16  |
	| 1         | ushort.MaxValue | false  | JsonUInt16  |
	| 1         | ushort.MinValue | true   | JsonUInt16  |
	| 1         | 1               | false  | JsonByte    |
	| 2         | 1               | true   | JsonByte    |
	| 1         | 3               | false  | JsonByte    |
	| 1         | byte.MaxValue   | false  | JsonByte    |
	| 1         | byte.MinValue   | true   | JsonByte    |


Scenario Outline: Less than or equal to for json element backed value as an integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than or equal to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | true   | JsonInteger |
	| 2         | 1               | false  | JsonInteger |
	| 1         | 3               | true   | JsonInteger |
	| 1         | long.MaxValue   | true   | JsonInteger |
	| 1         | long.MinValue   | false  | JsonInteger |
	| null      | null            | false  | JsonInteger |
	| null      | 1               | false  | JsonInteger |
	| 1         | 1               | true   | JsonInt64   |
	| 2         | 1               | false  | JsonInt64   |
	| 1         | 3               | true   | JsonInt64   |
	| 1         | long.MaxValue   | true   | JsonInt64   |
	| 1         | long.MinValue   | false  | JsonInt64   |
	| null      | null            | false  | JsonInt64   |
	| null      | 1               | false  | JsonInt64   |
	| 1         | 1               | true   | JsonInt32   |
	| 2         | 1               | false  | JsonInt32   |
	| 1         | 3               | true   | JsonInt32   |
	| 1         | int.MaxValue    | true   | JsonInt32   |
	| 1         | int.MinValue    | false  | JsonInt32   |
	| null      | null            | false  | JsonInt32   |
	| null      | 1               | false  | JsonInt32   |
	| 1         | 1               | true   | JsonInt16   |
	| 2         | 1               | false  | JsonInt16   |
	| 1         | 3               | true   | JsonInt16   |
	| 1         | short.MaxValue  | true   | JsonInt16   |
	| 1         | short.MinValue  | false  | JsonInt16   |
	| null      | null            | false  | JsonInt16   |
	| null      | 1               | false  | JsonInt16   |
	| 1         | 1               | true   | JsonSByte   |
	| 2         | 1               | false  | JsonSByte   |
	| 1         | 3               | true   | JsonSByte   |
	| 1         | sbyte.MaxValue  | true   | JsonSByte   |
	| 1         | sbyte.MinValue  | false  | JsonSByte   |
	| null      | null            | false  | JsonSByte   |
	| null      | 1               | false  | JsonSByte   |
	| 1         | 1               | true   | JsonUInt64  |
	| 2         | 1               | false  | JsonUInt64  |
	| 1         | 3               | true   | JsonUInt64  |
	| 1         | ulong.MaxValue  | true   | JsonUInt64  |
	| 1         | ulong.MinValue  | false  | JsonUInt64  |
	| null      | null            | false  | JsonUInt64  |
	| null      | 1               | false  | JsonUInt64  |
	| 1         | 1               | true   | JsonUInt32  |
	| 2         | 1               | false  | JsonUInt32  |
	| 1         | 3               | true   | JsonUInt32  |
	| 1         | uint.MaxValue   | true   | JsonUInt32  |
	| 1         | uint.MinValue   | false  | JsonUInt32  |
	| null      | null            | false  | JsonUInt32  |
	| null      | 1               | false  | JsonUInt32  |
	| 1         | 1               | true   | JsonUInt16  |
	| 2         | 1               | false  | JsonUInt16  |
	| 1         | 3               | true   | JsonUInt16  |
	| 1         | ushort.MaxValue | true   | JsonUInt16  |
	| 1         | ushort.MinValue | false  | JsonUInt16  |
	| null      | null            | false  | JsonUInt16  |
	| null      | 1               | false  | JsonUInt16  |
	| 1         | 1               | true   | JsonByte    |
	| 2         | 1               | false  | JsonByte    |
	| 1         | 3               | true   | JsonByte    |
	| 1         | byte.MaxValue   | true   | JsonByte    |
	| 1         | byte.MinValue   | false  | JsonByte    |
	| null      | null            | false  | JsonByte    |
	| null      | 1               | false  | JsonByte    |

Scenario Outline: Less than or equal to for dotnet backed value as an integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as less than or equal to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | true   | JsonInteger |
	| 2         | 1               | false  | JsonInteger |
	| 1         | 3               | true   | JsonInteger |
	| 1         | long.MaxValue   | true   | JsonInteger |
	| 1         | long.MinValue   | false  | JsonInteger |
	| 1         | 1               | true   | JsonInt64   |
	| 2         | 1               | false  | JsonInt64   |
	| 1         | 3               | true   | JsonInt64   |
	| 1         | long.MaxValue   | true   | JsonInt64   |
	| 1         | long.MinValue   | false  | JsonInt64   |
	| 1         | 1               | true   | JsonInt32   |
	| 2         | 1               | false  | JsonInt32   |
	| 1         | 3               | true   | JsonInt32   |
	| 1         | int.MaxValue    | true   | JsonInt32   |
	| 1         | int.MinValue    | false  | JsonInt32   |
	| 1         | 1               | true   | JsonInt16   |
	| 2         | 1               | false  | JsonInt16   |
	| 1         | 3               | true   | JsonInt16   |
	| 1         | short.MaxValue  | true   | JsonInt16   |
	| 1         | short.MinValue  | false  | JsonInt16   |
	| 1         | 1               | true   | JsonSByte   |
	| 2         | 1               | false  | JsonSByte   |
	| 1         | 3               | true   | JsonSByte   |
	| 1         | sbyte.MaxValue  | true   | JsonSByte   |
	| 1         | sbyte.MinValue  | false  | JsonSByte   |
	| 1         | 1               | true   | JsonUInt64  |
	| 2         | 1               | false  | JsonUInt64  |
	| 1         | 3               | true   | JsonUInt64  |
	| 1         | ulong.MaxValue  | true   | JsonUInt64  |
	| 1         | ulong.MinValue  | false  | JsonUInt64  |
	| 1         | 1               | true   | JsonUInt32  |
	| 2         | 1               | false  | JsonUInt32  |
	| 1         | 3               | true   | JsonUInt32  |
	| 1         | uint.MaxValue   | true   | JsonUInt32  |
	| 1         | uint.MinValue   | false  | JsonUInt32  |
	| 1         | 1               | true   | JsonUInt16  |
	| 2         | 1               | false  | JsonUInt16  |
	| 1         | 3               | true   | JsonUInt16  |
	| 1         | ushort.MaxValue | true   | JsonUInt16  |
	| 1         | ushort.MinValue | false  | JsonUInt16  |
	| 1         | 1               | true   | JsonByte    |
	| 2         | 1               | false  | JsonByte    |
	| 1         | 3               | true   | JsonByte    |
	| 1         | byte.MaxValue   | true   | JsonByte    |
	| 1         | byte.MinValue   | false  | JsonByte    |

Scenario Outline: Greater than or equal to for json element backed value as a integer
	Given the JsonElement backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than or equal to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | true   | JsonInteger |
	| 2         | 1               | true   | JsonInteger |
	| 1         | 3               | false  | JsonInteger |
	| 1         | long.MaxValue   | false  | JsonInteger |
	| 1         | long.MinValue   | true   | JsonInteger |
	| null      | null            | false  | JsonInteger |
	| null      | 1               | false  | JsonInteger |
	| 1         | 1               | true   | JsonInt64   |
	| 2         | 1               | true   | JsonInt64   |
	| 1         | 3               | false  | JsonInt64   |
	| 1         | long.MaxValue   | false  | JsonInt64   |
	| 1         | long.MinValue   | true   | JsonInt64   |
	| null      | null            | false  | JsonInt64   |
	| null      | 1               | false  | JsonInt64   |
	| 1         | 1               | true   | JsonInt32   |
	| 2         | 1               | true   | JsonInt32   |
	| 1         | 3               | false  | JsonInt32   |
	| 1         | int.MaxValue    | false  | JsonInt32   |
	| 1         | int.MinValue    | true   | JsonInt32   |
	| null      | null            | false  | JsonInt32   |
	| null      | 1               | false  | JsonInt32   |
	| 1         | 1               | true   | JsonInt16   |
	| 2         | 1               | true   | JsonInt16   |
	| 1         | 3               | false  | JsonInt16   |
	| 1         | short.MaxValue  | false  | JsonInt16   |
	| 1         | short.MinValue  | true   | JsonInt16   |
	| null      | null            | false  | JsonInt16   |
	| null      | 1               | false  | JsonInt16   |
	| 1         | 1               | true   | JsonSByte   |
	| 2         | 1               | true   | JsonSByte   |
	| 1         | 3               | false  | JsonSByte   |
	| 1         | sbyte.MaxValue  | false  | JsonSByte   |
	| 1         | sbyte.MinValue  | true   | JsonSByte   |
	| null      | null            | false  | JsonSByte   |
	| null      | 1               | false  | JsonSByte   |
	| 1         | 1               | true   | JsonUInt64  |
	| 2         | 1               | true   | JsonUInt64  |
	| 1         | 3               | false  | JsonUInt64  |
	| 1         | ulong.MaxValue  | false  | JsonUInt64  |
	| 1         | ulong.MinValue  | true   | JsonUInt64  |
	| null      | null            | false  | JsonUInt64  |
	| null      | 1               | false  | JsonUInt64  |
	| 1         | 1               | true   | JsonUInt32  |
	| 2         | 1               | true   | JsonUInt32  |
	| 1         | 3               | false  | JsonUInt32  |
	| 1         | uint.MaxValue   | false  | JsonUInt32  |
	| 1         | uint.MinValue   | true   | JsonUInt32  |
	| null      | null            | false  | JsonUInt32  |
	| null      | 1               | false  | JsonUInt32  |
	| 1         | 1               | true   | JsonUInt16  |
	| 2         | 1               | true   | JsonUInt16  |
	| 1         | 3               | false  | JsonUInt16  |
	| 1         | ushort.MaxValue | false  | JsonUInt16  |
	| 1         | ushort.MinValue | true   | JsonUInt16  |
	| null      | null            | false  | JsonUInt16  |
	| null      | 1               | false  | JsonUInt16  |
	| 1         | 1               | true   | JsonByte    |
	| 2         | 1               | true   | JsonByte    |
	| 1         | 3               | false  | JsonByte    |
	| 1         | byte.MaxValue   | false  | JsonByte    |
	| 1         | byte.MinValue   | true   | JsonByte    |
	| null      | null            | false  | JsonByte    |
	| null      | 1               | false  | JsonByte    |

Scenario Outline: Greater than or equal to for dotnet backed value as a integer
	Given the dotnet backed <TargetType> <jsonValue>
	When I compare the <TargetType> as greater than or equal to the integer <value>
	Then the result should be <result>

Examples:
	| jsonValue | value           | result | TargetType  |
	| 1         | 1               | true   | JsonInteger |
	| 2         | 1               | true   | JsonInteger |
	| 1         | 3               | false  | JsonInteger |
	| 1         | long.MaxValue   | false  | JsonInteger |
	| 1         | long.MinValue   | true   | JsonInteger |
	| 1         | 1               | true   | JsonInt64   |
	| 2         | 1               | true   | JsonInt64   |
	| 1         | 3               | false  | JsonInt64   |
	| 1         | long.MaxValue   | false  | JsonInt64   |
	| 1         | long.MinValue   | true   | JsonInt64   |
	| 1         | 1               | true   | JsonInt32   |
	| 2         | 1               | true   | JsonInt32   |
	| 1         | 3               | false  | JsonInt32   |
	| 1         | int.MaxValue    | false  | JsonInt32   |
	| 1         | int.MinValue    | true   | JsonInt32   |
	| 1         | 1               | true   | JsonInt16   |
	| 2         | 1               | true   | JsonInt16   |
	| 1         | 3               | false  | JsonInt16   |
	| 1         | short.MaxValue  | false  | JsonInt16   |
	| 1         | short.MinValue  | true   | JsonInt16   |
	| 1         | 1               | true   | JsonSByte   |
	| 2         | 1               | true   | JsonSByte   |
	| 1         | 3               | false  | JsonSByte   |
	| 1         | sbyte.MaxValue  | false  | JsonSByte   |
	| 1         | sbyte.MinValue  | true   | JsonSByte   |
	| 1         | 1               | true   | JsonUInt64  |
	| 2         | 1               | true   | JsonUInt64  |
	| 1         | 3               | false  | JsonUInt64  |
	| 1         | ulong.MaxValue  | false  | JsonUInt64  |
	| 1         | ulong.MinValue  | true   | JsonUInt64  |
	| 1         | 1               | true   | JsonUInt32  |
	| 2         | 1               | true   | JsonUInt32  |
	| 1         | 3               | false  | JsonUInt32  |
	| 1         | uint.MaxValue   | false  | JsonUInt32  |
	| 1         | uint.MinValue   | true   | JsonUInt32  |
	| 1         | 1               | true   | JsonUInt16  |
	| 2         | 1               | true   | JsonUInt16  |
	| 1         | 3               | false  | JsonUInt16  |
	| 1         | ushort.MaxValue | false  | JsonUInt16  |
	| 1         | ushort.MinValue | true   | JsonUInt16  |
	| 1         | 1               | true   | JsonByte    |
	| 2         | 1               | true   | JsonByte    |
	| 1         | 3               | false  | JsonByte    |
	| 1         | byte.MaxValue   | false  | JsonByte    |
	| 1         | byte.MinValue   | true   | JsonByte    |