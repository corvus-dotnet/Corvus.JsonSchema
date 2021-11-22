Feature: ValidationContext
	Storing validation results in a ValidatioContext

Scenario Outline: Add results to the stack
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	When I add a <validOrInvalidResult> with the message <message>
	Then the validationResult should be <validationResultValidOrInvalid>

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | validOrInvalidResult | message     | validationResultValidOrInvalid |
		| valid          | without results      | with a stack       | without evaluated properties     | without evaluated items     | valid result         | <null>      | valid                          |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | valid result         | <null>      | valid                          |
		| valid          | without results      | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| invalid        | without results      | with a stack       | without evaluated properties     | without evaluated items     | valid result         | <null>      | invalid                        |
		| invalid        | without results      | without a stack    | without evaluated properties     | without evaluated items     | valid result         | <null>      | invalid                        |
		| invalid        | without results      | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| invalid        | without results      | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| valid          | without results      | with a stack       | without evaluated properties     | without evaluated items     | valid result         | "A message" | valid                          |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | valid result         | "A message" | valid                          |
		| valid          | without results      | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| invalid        | without results      | with a stack       | without evaluated properties     | without evaluated items     | valid result         | "A message" | invalid                        |
		| invalid        | without results      | without a stack    | without evaluated properties     | without evaluated items     | valid result         | "A message" | invalid                        |
		| invalid        | without results      | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| invalid        | without results      | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| valid          | with results         | with a stack       | without evaluated properties     | without evaluated items     | valid result         | <null>      | valid                          |
		| valid          | with results         | without a stack    | without evaluated properties     | without evaluated items     | valid result         | <null>      | valid                          |
		| valid          | with results         | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| valid          | with results         | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| invalid        | with results         | with a stack       | without evaluated properties     | without evaluated items     | valid result         | <null>      | invalid                        |
		| invalid        | with results         | without a stack    | without evaluated properties     | without evaluated items     | valid result         | <null>      | invalid                        |
		| invalid        | with results         | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| invalid        | with results         | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | <null>      | invalid                        |
		| valid          | with results         | with a stack       | without evaluated properties     | without evaluated items     | valid result         | "A message" | valid                          |
		| valid          | with results         | without a stack    | without evaluated properties     | without evaluated items     | valid result         | "A message" | valid                          |
		| valid          | with results         | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| valid          | with results         | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| invalid        | with results         | with a stack       | without evaluated properties     | without evaluated items     | valid result         | "A message" | invalid                        |
		| invalid        | with results         | without a stack    | without evaluated properties     | without evaluated items     | valid result         | "A message" | invalid                        |
		| invalid        | with results         | with a stack       | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |
		| invalid        | with results         | without a stack    | without evaluated properties     | without evaluated items     | invalid result       | "A message" | invalid                        |

Scenario Outline: With evaluated properties
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	When I evaluate the properties at [<evaluateIndices>]
	Then the properties at [<notEvaluatedIndices>] should not be evaluated
	And the properties at [<evaluatedIndices>] should be evaluated

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluatedIndices | notEvaluatedIndices                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | <none>           | 0,1,2,3,4                                             |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 1,2,3           | 1,2,3            | 0,4                                                   |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 66,129          | 66,129           | 0,1,2,3,4,63,64,65,67,68,126,127,128,130              |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 65536           | 65536            | 0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144 |

Scenario Outline: With evaluated items
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	When I evaluate the items at [<evaluateIndices>]
	Then the items at [<notEvaluatedIndices>] should not be evaluated
	And the items at [<evaluatedIndices>] should be evaluated

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluatedIndices | notEvaluatedIndices                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | <none>           | 0,1,2,3,4                                             |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 1,2,3           | 1,2,3            | 0,4                                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 66,129          | 66,129           | 0,1,2,3,4,63,64,65,67,68,126,127,128,130              |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 65536           | 65536            | 0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144 |

Scenario Outline: With merged child context properties
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	When I create a child context
	And I evaluate the properties at [<evaluateIndices>]
	And I merge the child context
	Then the properties at [<notEvaluatedIndices>] should not be evaluated locally
	And the properties at [<evaluatedIndices>] should not be evaluated locally
	And the properties at [<notEvaluatedIndices>] should not be evaluated locally or applied
	And the properties at [<evaluatedIndices>] should be evaluated locally or applied

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluatedIndices | notEvaluatedIndices                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | <none>           | 0,1,2,3,4                                             |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 1,2,3           | 1,2,3            | 0,4                                                   |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 66,129          | 66,129           | 0,1,2,3,4,63,64,65,67,68,126,127,128,130              |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 65536           | 65536            | 0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144 |

Scenario Outline: With merged child context items
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	When I create a child context
	And I evaluate the items at [<evaluateIndices>]
	And I merge the child context
	Then the items at [<notEvaluatedIndices>] should not be evaluated locally
	And the items at [<evaluatedIndices>] should not be evaluated locally
	And the items at [<notEvaluatedIndices>] should not be evaluated locally or applied
	And the items at [<evaluatedIndices>] should be evaluated locally or applied

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluatedIndices | notEvaluatedIndices                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | <none>           | 0,1,2,3,4                                             |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 1,2,3           | 1,2,3            | 0,4                                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 66,129          | 66,129           | 0,1,2,3,4,63,64,65,67,68,126,127,128,130              |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 65536           | 65536            | 0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144 |

Scenario Outline: With merged child context whose properties have been evaluated before merging
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	And I evaluate the properties at [<evaluateIndices>]
	When I create a child context
	And I merge the child context
	Then the properties at [<notEvaluatedIndices>] should not be evaluated locally
	And the properties at [<evaluatedIndices>] should be evaluated locally
	And the properties at [<notEvaluatedIndices>] should not be evaluated locally or applied
	And the properties at [<evaluatedIndices>] should be evaluated locally or applied

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluatedIndices | notEvaluatedIndices                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | <none>           | 0,1,2,3,4                                             |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 1,2,3           | 1,2,3            | 0,4                                                   |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 66,129          | 66,129           | 0,1,2,3,4,63,64,65,67,68,126,127,128,130              |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 65536           | 65536            | 0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144 |

Scenario Outline: With merged child context whose items have been evaluated before merging
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	And I evaluate the items at [<evaluateIndices>]
	When I create a child context
	And I merge the child context
	Then the items at [<notEvaluatedIndices>] should not be evaluated locally
	And the items at [<evaluatedIndices>] should be evaluated locally
	And the items at [<notEvaluatedIndices>] should not be evaluated locally or applied
	And the items at [<evaluatedIndices>] should be evaluated locally or applied

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluatedIndices | notEvaluatedIndices                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | <none>           | 0,1,2,3,4                                             |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 1,2,3           | 1,2,3            | 0,4                                                   |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 66,129          | 66,129           | 0,1,2,3,4,63,64,65,67,68,126,127,128,130              |
		| valid          | without results      | without a stack    | without evaluated properties     | with evaluated items        | 65536           | 65536            | 0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144 |

Scenario Outline: With merged child context whose properties have been evaluated both before and after merging
	Given a <validOrInvalid> validation context <withOrWithoutResults>, <withOrWithoutStack>, <withOrWithoutEvaluatedProperties>, and <withOrWithoutEvaluatedItems>
	And I evaluate the properties at [<evaluateIndices>]
	When I create a child context
	And I evaluate the properties at [<evaluateAfterIndices>]
	And I merge the child context
	Then the properties at [<notEvaluatedIndices>] should not be evaluated locally
	And the properties at [<evaluatedIndices>] should be evaluated locally
	And the properties at [<evaluatedAfterIndices>] should not be evaluated locally
	And the properties at [<notEvaluatedIndices>] should not be evaluated locally or applied
	And the properties at [<evaluatedIndices>] should be evaluated locally or applied
	And the properties at [<evaluatedAfterIndices>] should be evaluated locally or applied

	Examples:
		| validOrInvalid | withOrWithoutResults | withOrWithoutStack | withOrWithoutEvaluatedProperties | withOrWithoutEvaluatedItems | evaluateIndices | evaluateAfterIndices | evaluatedIndices | notEvaluatedIndices                                 | evaluatedAfterIndices |
		| valid          | without results      | without a stack    | without evaluated properties     | without evaluated items     | 1,2,3           | 4                    | <none>           | 0,1,2,3,4                                           | <none>                |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 1,2,3           | 4                    | 1,2,3            | 0                                                   | 4                     |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 66,129          | 4                    | 66,129           | 0,1,2,3,63,64,65,67,68,126,127,128,130              | 4                     |
		| valid          | without results      | without a stack    | with evaluated properties        | without evaluated items     | 65536           | 4                    | 65536            | 0,1,2,3,63,64,65,67,68,126,127,128,130,65535,262144 | 4                     |