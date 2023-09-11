@draft2019-09

Feature: recursiveRef draft2019-09
    In order to use json-schema
    As a developer
    I want to support recursiveRef in draft2019-09

Scenario Outline: $recursiveRef without $recursiveAnchor works like $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": { "$recursiveRef": "#" }
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | match                                                                            |
        | #/000/tests/001/data | true  | recursive match                                                                  |
        | #/000/tests/002/data | false | mismatch                                                                         |
        | #/000/tests/003/data | false | recursive mismatch                                                               |

Scenario Outline: $recursiveRef without using nesting
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:4242/draft2019-09/recursiveRef2/schema.json",
            "$defs": {
                "myobject": {
                    "$id": "myobject.json",
                    "$recursiveAnchor": true,
                    "anyOf": [
                        { "type": "string" },
                        {
                            "type": "object",
                            "additionalProperties": { "$recursiveRef": "#" }
                        }
                    ]
                }
            },
            "anyOf": [
                { "type": "integer" },
                { "$ref": "#/$defs/myobject" }
            ]
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | integer matches at the outer level                                               |
        | #/001/tests/001/data | true  | single level match                                                               |
        | #/001/tests/002/data | false | integer does not match as a property value                                       |
        | #/001/tests/003/data | true  | two levels, properties match with inner definition                               |
        | #/001/tests/004/data | false | two levels, no match                                                             |

Scenario Outline: $recursiveRef with nesting
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:4242/draft2019-09/recursiveRef3/schema.json",
            "$recursiveAnchor": true,
            "$defs": {
                "myobject": {
                    "$id": "myobject.json",
                    "$recursiveAnchor": true,
                    "anyOf": [
                        { "type": "string" },
                        {
                            "type": "object",
                            "additionalProperties": { "$recursiveRef": "#" }
                        }
                    ]
                }
            },
            "anyOf": [
                { "type": "integer" },
                { "$ref": "#/$defs/myobject" }
            ]
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | integer matches at the outer level                                               |
        | #/002/tests/001/data | true  | single level match                                                               |
        | #/002/tests/002/data | true  | integer now matches as a property value                                          |
        | #/002/tests/003/data | true  | two levels, properties match with inner definition                               |
        | #/002/tests/004/data | true  | two levels, properties match with $recursiveRef                                  |

Scenario Outline: $recursiveRef with $recursiveAnchor: false works like $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:4242/draft2019-09/recursiveRef4/schema.json",
            "$recursiveAnchor": false,
            "$defs": {
                "myobject": {
                    "$id": "myobject.json",
                    "$recursiveAnchor": false,
                    "anyOf": [
                        { "type": "string" },
                        {
                            "type": "object",
                            "additionalProperties": { "$recursiveRef": "#" }
                        }
                    ]
                }
            },
            "anyOf": [
                { "type": "integer" },
                { "$ref": "#/$defs/myobject" }
            ]
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | integer matches at the outer level                                               |
        | #/003/tests/001/data | true  | single level match                                                               |
        | #/003/tests/002/data | false | integer does not match as a property value                                       |
        | #/003/tests/003/data | true  | two levels, properties match with inner definition                               |
        | #/003/tests/004/data | false | two levels, integer does not match as a property value                           |

Scenario Outline: $recursiveRef with no $recursiveAnchor works like $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:4242/draft2019-09/recursiveRef5/schema.json",
            "$defs": {
                "myobject": {
                    "$id": "myobject.json",
                    "$recursiveAnchor": false,
                    "anyOf": [
                        { "type": "string" },
                        {
                            "type": "object",
                            "additionalProperties": { "$recursiveRef": "#" }
                        }
                    ]
                }
            },
            "anyOf": [
                { "type": "integer" },
                { "$ref": "#/$defs/myobject" }
            ]
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | integer matches at the outer level                                               |
        | #/004/tests/001/data | true  | single level match                                                               |
        | #/004/tests/002/data | false | integer does not match as a property value                                       |
        | #/004/tests/003/data | true  | two levels, properties match with inner definition                               |
        | #/004/tests/004/data | false | two levels, integer does not match as a property value                           |

Scenario Outline: $recursiveRef with no $recursiveAnchor in the initial target schema resource
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:4242/draft2019-09/recursiveRef6/base.json",
            "$recursiveAnchor": true,
            "anyOf": [
                { "type": "boolean" },
                {
                    "type": "object",
                    "additionalProperties": {
                        "$id": "http://localhost:4242/draft2019-09/recursiveRef6/inner.json",
                        "$comment": "there is no $recursiveAnchor: true here, so we do NOT recurse to the base",
                        "anyOf": [
                            { "type": "integer" },
                            { "type": "object", "additionalProperties": { "$recursiveRef": "#" } }
                        ]
                    }
                }
            ]
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | false | leaf node does not match; no recursion                                           |
        | #/005/tests/001/data | true  | leaf node matches: recursion uses the inner schema                               |
        | #/005/tests/002/data | false | leaf node does not match: recursion uses the inner schema                        |

Scenario Outline: $recursiveRef with no $recursiveAnchor in the outer schema resource
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:4242/draft2019-09/recursiveRef7/base.json",
            "anyOf": [
                { "type": "boolean" },
                {
                    "type": "object",
                    "additionalProperties": {
                        "$id": "http://localhost:4242/draft2019-09/recursiveRef7/inner.json",
                        "$recursiveAnchor": true,
                        "anyOf": [
                            { "type": "integer" },
                            { "type": "object", "additionalProperties": { "$recursiveRef": "#" } }
                        ]
                    }
                }
            ]
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | false | leaf node does not match; no recursion                                           |
        | #/006/tests/001/data | true  | leaf node matches: recursion only uses inner schema                              |
        | #/006/tests/002/data | false | leaf node does not match: recursion only uses inner schema                       |

Scenario Outline: multiple dynamic paths to the $recursiveRef keyword
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "https://example.com/recursiveRef8_main.json",
            "$defs": {
                "inner": {
                    "$id": "recursiveRef8_inner.json",
                    "$recursiveAnchor": true,
                    "title": "inner",
                    "additionalProperties": {
                        "$recursiveRef": "#"
                    }
                }
            },
            "if": {
                "propertyNames": {
                    "pattern": "^[a-m]"
                }
            },
            "then": {
                "title": "any type of node",
                "$id": "recursiveRef8_anyLeafNode.json",
                "$recursiveAnchor": true,
                "$ref": "recursiveRef8_inner.json"
            },
            "else": {
                "title": "integer node",
                "$id": "recursiveRef8_integerNode.json",
                "$recursiveAnchor": true,
                "type": [ "object", "integer" ],
                "$ref": "recursiveRef8_inner.json"
            }
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | recurse to anyLeafNode - floats are allowed                                      |
        | #/007/tests/001/data | false | recurse to integerNode - floats are not allowed                                  |

Scenario Outline: dynamic $recursiveRef destination (not predictable at schema compile time)
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "https://example.com/main.json",
            "$defs": {
                "inner": {
                    "$id": "inner.json",
                    "$recursiveAnchor": true,
                    "title": "inner",
                    "additionalProperties": {
                        "$recursiveRef": "#"
                    }
                }

            },
            "if": { "propertyNames": { "pattern": "^[a-m]" } },
            "then": {
                "title": "any type of node",
                "$id": "anyLeafNode.json",
                "$recursiveAnchor": true,
                "$ref": "main.json#/$defs/inner"
            },
            "else": {
                "title": "integer node",
                "$id": "integerNode.json",
                "$recursiveAnchor": true,
                "type": [ "object", "integer" ],
                "$ref": "main.json#/$defs/inner"
            }
        }
*/
    Given the input JSON file "recursiveRef.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | numeric node                                                                     |
        | #/008/tests/001/data | false | integer node                                                                     |
