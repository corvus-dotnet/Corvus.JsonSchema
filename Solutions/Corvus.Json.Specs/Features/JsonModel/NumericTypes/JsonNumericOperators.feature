Feature: Numeric Operators

Scenario Outline: JsonEement backed Binary operators
	Given the JsonElement backed <TargetType> <value>
	When I apply <operator> to the <TargetType> with <operand>
	Then the result of the operator is the <TargetType> <result>
Examples:
	| TargetType  | value | operator | operand | result |
	| JsonNumber  | -2    | add      | 3       | 1      |
	| JsonNumber  | 2     | add      | 3       | 5      |
	| JsonNumber  | 2     | sub      | 3       | -1     |
	| JsonNumber  | -2    | sub      | 3       | -5     |
	| JsonNumber  | -2    | mul      | 3       | -6     |
	| JsonNumber  | 2     | mul      | 3       | 6      |
	| JsonNumber  | -15   | div      | 3       | -5     |
	| JsonNumber  | 15    | div      | 3       | 5      |

	| JsonInteger | -2    | add      | 3       | 1      |
	| JsonInteger | 2     | add      | 3       | 5      |
	| JsonInteger | 2     | sub      | 3       | -1     |
	| JsonInteger | -2    | sub      | 3       | -5     |
	| JsonInteger | -2    | mul      | 3       | -6     |
	| JsonInteger | 2     | mul      | 3       | 6      |
	| JsonInteger | -15   | div      | 3       | -5     |
	| JsonInteger | 15    | div      | 3       | 5      |

	| JsonHalf    | -2    | add      | 3       | 1      |
	| JsonHalf    | 2     | add      | 3       | 5      |
	| JsonHalf    | 2     | sub      | 3       | -1     |
	| JsonHalf    | -2    | sub      | 3       | -5     |
	| JsonHalf    | -2    | mul      | 3       | -6     |
	| JsonHalf    | 2     | mul      | 3       | 6      |
	| JsonHalf    | -15   | div      | 3       | -5     |
	| JsonHalf    | 15    | div      | 3       | 5      |

	| JsonSingle  | -2    | add      | 3       | 1      |
	| JsonSingle  | 2     | add      | 3       | 5      |
	| JsonSingle  | 2     | sub      | 3       | -1     |
	| JsonSingle  | -2    | sub      | 3       | -5     |
	| JsonSingle  | -2    | mul      | 3       | -6     |
	| JsonSingle  | 2     | mul      | 3       | 6      |
	| JsonSingle  | -15   | div      | 3       | -5     |
	| JsonSingle  | 15    | div      | 3       | 5      |

	| JsonDouble  | -2    | add      | 3       | 1      |
	| JsonDouble  | 2     | add      | 3       | 5      |
	| JsonDouble  | 2     | sub      | 3       | -1     |
	| JsonDouble  | -2    | sub      | 3       | -5     |
	| JsonDouble  | -2    | mul      | 3       | -6     |
	| JsonDouble  | 2     | mul      | 3       | 6      |
	| JsonDouble  | -15   | div      | 3       | -5     |
	| JsonDouble  | 15    | div      | 3       | 5      |

	| JsonDecimal | -2    | add      | 3       | 1      |
	| JsonDecimal | 2     | add      | 3       | 5      |
	| JsonDecimal | 2     | sub      | 3       | -1     |
	| JsonDecimal | -2    | sub      | 3       | -5     |
	| JsonDecimal | -2    | mul      | 3       | -6     |
	| JsonDecimal | 2     | mul      | 3       | 6      |
	| JsonDecimal | -15   | div      | 3       | -5     |
	| JsonDecimal | 15    | div      | 3       | 5      |

	| JsonInt128  | -2    | add      | 3       | 1      |
	| JsonInt128  | 2     | add      | 3       | 5      |
	| JsonInt128  | 2     | sub      | 3       | -1     |
	| JsonInt128  | -2    | sub      | 3       | -5     |
	| JsonInt128  | -2    | mul      | 3       | -6     |
	| JsonInt128  | 2     | mul      | 3       | 6      |
	| JsonInt128  | -15   | div      | 3       | -5     |
	| JsonInt128  | 15    | div      | 3       | 5      |

	| JsonInt64   | -2    | add      | 3       | 1      |
	| JsonInt64   | 2     | add      | 3       | 5      |
	| JsonInt64   | 2     | sub      | 3       | -1     |
	| JsonInt64   | -2    | sub      | 3       | -5     |
	| JsonInt64   | -2    | mul      | 3       | -6     |
	| JsonInt64   | 2     | mul      | 3       | 6      |
	| JsonInt64   | -15   | div      | 3       | -5     |
	| JsonInt64   | 15    | div      | 3       | 5      |

	| JsonInt32   | -2    | add      | 3       | 1      |
	| JsonInt32   | 2     | add      | 3       | 5      |
	| JsonInt32   | 2     | sub      | 3       | -1     |
	| JsonInt32   | -2    | sub      | 3       | -5     |
	| JsonInt32   | -2    | mul      | 3       | -6     |
	| JsonInt32   | 2     | mul      | 3       | 6      |
	| JsonInt32   | -15   | div      | 3       | -5     |
	| JsonInt32   | 15    | div      | 3       | 5      |

	| JsonInt16   | -2    | add      | 3       | 1      |
	| JsonInt16   | 2     | add      | 3       | 5      |
	| JsonInt16   | 2     | sub      | 3       | -1     |
	| JsonInt16   | -2    | sub      | 3       | -5     |
	| JsonInt16   | -2    | mul      | 3       | -6     |
	| JsonInt16   | 2     | mul      | 3       | 6      |
	| JsonInt16   | -15   | div      | 3       | -5     |
	| JsonInt16   | 15    | div      | 3       | 5      |

	| JsonSByte   | -2    | add      | 3       | 1      |
	| JsonSByte   | 2     | add      | 3       | 5      |
	| JsonSByte   | 2     | sub      | 3       | -1     |
	| JsonSByte   | -2    | sub      | 3       | -5     |
	| JsonSByte   | -2    | mul      | 3       | -6     |
	| JsonSByte   | 2     | mul      | 3       | 6      |
	| JsonSByte   | -15   | div      | 3       | -5     |
	| JsonSByte   | 15    | div      | 3       | 5      |

	| JsonUInt128 | 2     | add      | 3       | 5      |
	| JsonUInt128 | 3     | sub      | 2       | 1      |
	| JsonUInt128 | 2     | mul      | 3       | 6      |
	| JsonUInt128 | 15    | div      | 3       | 5      |

	| JsonUInt64  | 2     | add      | 3       | 5      |
	| JsonUInt64  | 3     | sub      | 2       | 1      |
	| JsonUInt64  | 2     | mul      | 3       | 6      |
	| JsonUInt64  | 15    | div      | 3       | 5      |

	| JsonUInt32  | 2     | add      | 3       | 5      |
	| JsonUInt32  | 3     | sub      | 2       | 1      |
	| JsonUInt32  | 2     | mul      | 3       | 6      |
	| JsonUInt32  | 15    | div      | 3       | 5      |

	| JsonUInt16  | 2     | add      | 3       | 5      |
	| JsonUInt16  | 3     | sub      | 2       | 1      |
	| JsonUInt16  | 2     | mul      | 3       | 6      |
	| JsonUInt16  | 15    | div      | 3       | 5      |

	| JsonByte    | 2     | add      | 3       | 5      |
	| JsonByte    | 3     | sub      | 2       | 1      |
	| JsonByte    | 2     | mul      | 3       | 6      |
	| JsonByte    | 15    | div      | 3       | 5      |

Scenario Outline: Dotnet backed Binary operators
	Given the dotnet backed <TargetType> <value>
	When I apply <operator> to the <TargetType> with <operand>
	Then the result of the operator is the <TargetType> <result>
Examples:
	| TargetType  | value | operator | operand | result |
	| JsonNumber  | -2    | add      | 3       | 1      |
	| JsonNumber  | 2     | add      | 3       | 5      |
	| JsonNumber  | 2     | sub      | 3       | -1     |
	| JsonNumber  | -2    | sub      | 3       | -5     |
	| JsonNumber  | -2    | mul      | 3       | -6     |
	| JsonNumber  | 2     | mul      | 3       | 6      |
	| JsonNumber  | -15   | div      | 3       | -5     |
	| JsonNumber  | 15    | div      | 3       | 5      |

	| JsonInteger | -2    | add      | 3       | 1      |
	| JsonInteger | 2     | add      | 3       | 5      |
	| JsonInteger | 2     | sub      | 3       | -1     |
	| JsonInteger | -2    | sub      | 3       | -5     |
	| JsonInteger | -2    | mul      | 3       | -6     |
	| JsonInteger | 2     | mul      | 3       | 6      |
	| JsonInteger | -15   | div      | 3       | -5     |
	| JsonInteger | 15    | div      | 3       | 5      |

	| JsonHalf    | -2    | add      | 3       | 1      |
	| JsonHalf    | 2     | add      | 3       | 5      |
	| JsonHalf    | 2     | sub      | 3       | -1     |
	| JsonHalf    | -2    | sub      | 3       | -5     |
	| JsonHalf    | -2    | mul      | 3       | -6     |
	| JsonHalf    | 2     | mul      | 3       | 6      |
	| JsonHalf    | -15   | div      | 3       | -5     |
	| JsonHalf    | 15    | div      | 3       | 5      |

	| JsonSingle  | -2    | add      | 3       | 1      |
	| JsonSingle  | 2     | add      | 3       | 5      |
	| JsonSingle  | 2     | sub      | 3       | -1     |
	| JsonSingle  | -2    | sub      | 3       | -5     |
	| JsonSingle  | -2    | mul      | 3       | -6     |
	| JsonSingle  | 2     | mul      | 3       | 6      |
	| JsonSingle  | -15   | div      | 3       | -5     |
	| JsonSingle  | 15    | div      | 3       | 5      |

	| JsonDouble  | -2    | add      | 3       | 1      |
	| JsonDouble  | 2     | add      | 3       | 5      |
	| JsonDouble  | 2     | sub      | 3       | -1     |
	| JsonDouble  | -2    | sub      | 3       | -5     |
	| JsonDouble  | -2    | mul      | 3       | -6     |
	| JsonDouble  | 2     | mul      | 3       | 6      |
	| JsonDouble  | -15   | div      | 3       | -5     |
	| JsonDouble  | 15    | div      | 3       | 5      |

	| JsonDecimal | -2    | add      | 3       | 1      |
	| JsonDecimal | 2     | add      | 3       | 5      |
	| JsonDecimal | 2     | sub      | 3       | -1     |
	| JsonDecimal | -2    | sub      | 3       | -5     |
	| JsonDecimal | -2    | mul      | 3       | -6     |
	| JsonDecimal | 2     | mul      | 3       | 6      |
	| JsonDecimal | -15   | div      | 3       | -5     |
	| JsonDecimal | 15    | div      | 3       | 5      |

	| JsonInt128  | -2    | add      | 3       | 1      |
	| JsonInt128  | 2     | add      | 3       | 5      |
	| JsonInt128  | 2     | sub      | 3       | -1     |
	| JsonInt128  | -2    | sub      | 3       | -5     |
	| JsonInt128  | -2    | mul      | 3       | -6     |
	| JsonInt128  | 2     | mul      | 3       | 6      |
	| JsonInt128  | -15   | div      | 3       | -5     |
	| JsonInt128  | 15    | div      | 3       | 5      |

	| JsonInt64   | -2    | add      | 3       | 1      |
	| JsonInt64   | 2     | add      | 3       | 5      |
	| JsonInt64   | 2     | sub      | 3       | -1     |
	| JsonInt64   | -2    | sub      | 3       | -5     |
	| JsonInt64   | -2    | mul      | 3       | -6     |
	| JsonInt64   | 2     | mul      | 3       | 6      |
	| JsonInt64   | -15   | div      | 3       | -5     |
	| JsonInt64   | 15    | div      | 3       | 5      |

	| JsonInt32   | -2    | add      | 3       | 1      |
	| JsonInt32   | 2     | add      | 3       | 5      |
	| JsonInt32   | 2     | sub      | 3       | -1     |
	| JsonInt32   | -2    | sub      | 3       | -5     |
	| JsonInt32   | -2    | mul      | 3       | -6     |
	| JsonInt32   | 2     | mul      | 3       | 6      |
	| JsonInt32   | -15   | div      | 3       | -5     |
	| JsonInt32   | 15    | div      | 3       | 5      |

	| JsonInt16   | -2    | add      | 3       | 1      |
	| JsonInt16   | 2     | add      | 3       | 5      |
	| JsonInt16   | 2     | sub      | 3       | -1     |
	| JsonInt16   | -2    | sub      | 3       | -5     |
	| JsonInt16   | -2    | mul      | 3       | -6     |
	| JsonInt16   | 2     | mul      | 3       | 6      |
	| JsonInt16   | -15   | div      | 3       | -5     |
	| JsonInt16   | 15    | div      | 3       | 5      |

	| JsonSByte   | -2    | add      | 3       | 1      |
	| JsonSByte   | 2     | add      | 3       | 5      |
	| JsonSByte   | 2     | sub      | 3       | -1     |
	| JsonSByte   | -2    | sub      | 3       | -5     |
	| JsonSByte   | -2    | mul      | 3       | -6     |
	| JsonSByte   | 2     | mul      | 3       | 6      |
	| JsonSByte   | -15   | div      | 3       | -5     |
	| JsonSByte   | 15    | div      | 3       | 5      |

	| JsonUInt128 | 2     | add      | 3       | 5      |
	| JsonUInt128 | 3     | sub      | 2       | 1      |
	| JsonUInt128 | 2     | mul      | 3       | 6      |
	| JsonUInt128 | 15    | div      | 3       | 5      |

	| JsonUInt64  | 2     | add      | 3       | 5      |
	| JsonUInt64  | 3     | sub      | 2       | 1      |
	| JsonUInt64  | 2     | mul      | 3       | 6      |
	| JsonUInt64  | 15    | div      | 3       | 5      |

	| JsonUInt32  | 2     | add      | 3       | 5      |
	| JsonUInt32  | 3     | sub      | 2       | 1      |
	| JsonUInt32  | 2     | mul      | 3       | 6      |
	| JsonUInt32  | 15    | div      | 3       | 5      |

	| JsonUInt16  | 2     | add      | 3       | 5      |
	| JsonUInt16  | 3     | sub      | 2       | 1      |
	| JsonUInt16  | 2     | mul      | 3       | 6      |
	| JsonUInt16  | 15    | div      | 3       | 5      |

	| JsonByte    | 2     | add      | 3       | 5      |
	| JsonByte    | 3     | sub      | 2       | 1      |
	| JsonByte    | 2     | mul      | 3       | 6      |
	| JsonByte    | 15    | div      | 3       | 5      |
