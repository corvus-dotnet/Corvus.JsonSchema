Feature: JsonNumberCastNet80
	Validate the Json cast operators

Scenario Outline: Cast to JsonAny for json element backed value as a number
	Given the JsonElement backed <TargetType> 1.2
	When I cast the <TargetType> to JsonAny
	Then the result should equal the JsonAny 1.2

Examples:
	| TargetType |
	| JsonHalf   |

Scenario Outline: Cast to JsonAny for dotnet backed value as a number
	Given the dotnet backed <TargetType> 1.2
	When I cast the <TargetType> to JsonAny
	Then the result should equal within 0.001 the JsonAny 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to long for json element backed value as a number
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to long
	Then the result should equal the long 12

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to long for dotnet backed value as a number
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to long
	Then the result should equal the long 12

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast from long for json element backed value as a number
	Given the long for 12
	When I cast the long to <TargetType>
	Then the result should equal the <TargetType> 12

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to double for json element backed value as a number
	Given the JsonElement backed <TargetType> 1.2
	When I cast the <TargetType> to double
	Then the result should equal within 0.001 the double 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to double for dotnet backed value as a number
	Given the dotnet backed <TargetType> 1.2
	When I cast the <TargetType> to double
	Then the result should equal within 0.001 the double 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast from double for json element backed value as a number
	Given the double for 1.2
	When I cast the double to <TargetType>
	Then the result should equal the <TargetType> 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to decimal for json element backed value as a number
	Given the JsonElement backed <TargetType> 1.2
	When I cast the <TargetType> to decimal
	Then the result should equal within 0.001 the decimal 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to decimal for dotnet backed value as a number
	Given the dotnet backed <TargetType> 1.2
	When I cast the <TargetType> to decimal
	Then the result should equal within 0.001 the decimal 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast from decimal for json element backed value as a number
	Given the decimal for 1.2
	When I cast the decimal to <TargetType>
	Then the result should equal the <TargetType> 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to int for json element backed value as a number
	Given the JsonElement backed <TargetType> 12
	When I cast the <TargetType> to int
	Then the result should equal the int 12

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to int for dotnet backed value as a number
	Given the dotnet backed <TargetType> 12
	When I cast the <TargetType> to int
	Then the result should equal the int 12

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast from int for json element backed value as a number
	Given the int for 12
	When I cast the int to <TargetType>
	Then the result should equal the <TargetType> 12

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to float for json element backed value as a number
	Given the JsonElement backed <TargetType> 1.2
	When I cast the <TargetType> to float
	Then the result should equal the float 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast to float for dotnet backed value as a number
	Given the dotnet backed <TargetType> 1.2
	When I cast the <TargetType> to float
	Then the result should equal within 0.001 the float 1.2

Examples:
	| TargetType  |
	| JsonHalf   |

Scenario Outline: Cast from float for json element backed value as a number
	Given the float for 1.2
	When I cast the float to <TargetType>
	Then the result should equal the <TargetType> 1.2

Examples:
	| TargetType  |
	| JsonHalf   |
