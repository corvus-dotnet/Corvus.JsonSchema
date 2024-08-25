@draft2019-09

Feature: format draft2019-09
    In order to use json-schema
    As a developer
    I want to support numeric formats in draft2019-09

Scenario Outline: byte format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "byte"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| 255       | true  |
	| -1        | false |
	| 256       | false |

Scenario Outline: sbyte format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "sbyte"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| -128      | true  |
	| 127       | true  |
	| 128       | false |
	| -129      | false |

Scenario Outline: int16 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "int16"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| -32768    | true  |
	| 32767     | true  |
	| -32769    | false |
	| 32768     | false |

Scenario Outline: uint16 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "uint16"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| 0         | true  |
	| 65535     | true  |
	| 65536     | false |
	| -1        | false |

Scenario Outline: int32 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "int32"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData   | valid |
	| -2147483648 | true  |
	| 2147483647  | true  |
	| -2147483649 | false |
	| 2147483648  | false |

Scenario Outline: uint32 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "uint32"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData  | valid |
	| 0          | true  |
	| 4294967295 | true  |
	| 4294967296 | false |
	| -1         | false |

Scenario Outline: int64 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "int64"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData            | valid |
	| -9223372036854775808 | true  |
	| 9223372036854775807  | true  |
	| -9223372036854775809 | false |
	| 9223372036854775808  | false |

Scenario Outline: uint64 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "uint64"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData            | valid |
	| 0                    | true  |
	| 18446744073709551615 | true  |
	| 18446744073709551616 | false |
	| -1                   | false |

Scenario Outline: int128 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "int128"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                | valid |
	| -170141183460469231731687303715884105728 | true  |
	| 170141183460469231731687303715884105727  | true  |
	| -170141183460469231731687303715884105729 | false |
	| 170141183460469231731687303715884105728  | false |

Scenario Outline: uint128 format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "uint128"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                               | valid |
	| 0                                       | true  |
	| 340282366920938463463374607431768211455 | true  |
	| 340282366920938463463374607431768211456 | false |
	| -1                                      | false |

Scenario Outline: half format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "half"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	| -65500    | true  |
	| 65500     | true  |
	# the behaviour is just to truncate
	| -65501    | true  |
	| 65501     | true  |

Scenario Outline: single format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "single"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                | valid |
	| -340282346638528859811704183484516925440 | true  |
	| 340282346638528859811704183484516925440  | true  |
	# the behaviour is just to truncate
	| -340282346638528859811704183484516925441 | true  |
	| 340282346638528859811704183484516925441  | true  |

Scenario Outline: double format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "double"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData | valid |
	# Any input will be fine because the behaviour is just to truncate
	| -65500    | true  |
	| 65500     | true  |

Scenario Outline: decimal format
	Given a schema file
		"""
		   {
		       "$schema": "https://json-schema.org/draft/2019-09/schema",
		       "format": "decimal"
		   }
		"""

	And the schema at "#/0/schema"
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                      | valid |
	| -79228162514264337593543950335 | true  |
	| 79228162514264337593543950335  | true  |
	# Decimal, on the other hand, validates these ranges correctly because of the precise
	# nature of the type.
	| -79228162514264337593543950336 | false |
	| 79228162514264337593543950336  | false |

