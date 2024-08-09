Feature: JsonIntegerCastNet80
	Validate the Json cast operators

Scenario Outline: Cast to JsonAny for json element backed value as a integer
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to JsonAny for dotnet backed value as a integer
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to JsonNumber for json element backed value as a integer
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to JsonNumber
	Then the result should equal the JsonNumber 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to JsonNumber for dotnet backed value as a integer
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to JsonNumber
	Then the result should equal the JsonNumber 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to long for json element backed value as a integer
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to long
	Then the result should equal the long 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to long for dotnet backed value as a integer
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to long
	Then the result should equal the long 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast from long for json element backed value as a integer
	Given the long for 12
	When I cast the long to <TargetType>
	Then the result should equal the <TargetType> 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to double for json element backed value as a integer
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to double
	Then the result should equal the double 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to double for dotnet backed value as a integer
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to double
	Then the result should equal the double 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast from double for json element backed value as a integer
	Given the double for 12
	When I cast the double to <TargetType>
	Then the result should equal the <TargetType> 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to int for json element backed value as a integer
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to int
	Then the result should equal the int 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to int for dotnet backed value as a integer
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to int
	Then the result should equal the int 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast from int for json element backed value as a integer
	Given the int for 12
	When I cast the int to <TargetType>
	Then the result should equal the <TargetType> 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to float for json element backed value as a integer
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to float
	Then the result should equal the float 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to float for dotnet backed value as a integer
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to float
	Then the result should equal the float 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast from float for json element backed value as a integer
	Given the float for 12
	When I cast the float to <TargetType>
	Then the result should equal the <TargetType> 12
Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to short for json element backed value as a shorteger
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to short
	Then the result should equal the short 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to short for dotnet backed value as a shorteger
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to short
	Then the result should equal the short 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to ushort for json element backed value as a ushorteger
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to ushort
	Then the result should equal the ushort 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to ushort for dotnet backed value as a ushorteger
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to ushort
	Then the result should equal the ushort 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to byte for json element backed value as a byteeger
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to byte
	Then the result should equal the byte 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to byte for dotnet backed value as a byteeger
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to byte
	Then the result should equal the byte 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to sbyte for json element backed value as a sbyteeger
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to sbyte
	Then the result should equal the sbyte 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to sbyte for dotnet backed value as a sbyteeger
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to sbyte
	Then the result should equal the sbyte 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to uint for json element backed value as a uinteger
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to uint
	Then the result should equal the uint 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to uint for dotnet backed value as a uinteger
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to uint
	Then the result should equal the uint 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to ulong for json element backed value as a ulongeger
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to ulong
	Then the result should equal the ulong 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |

Scenario Outline: Cast to ulong for dotnet backed value as a ulongeger
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to ulong
	Then the result should equal the ulong 12

Examples:
	| TargetType  |
	| JsonInt128  |
	| JsonUInt128 |
