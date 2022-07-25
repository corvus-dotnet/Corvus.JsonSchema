@draft2019-09

Feature: unevaluatedItems draft2019-09
    In order to use json-schema
    As a developer
    I want to support unevaluatedItems in draft2019-09

Scenario Outline: unevaluatedItems true
/* Schema: 
{
            "type": "array",
            "unevaluatedItems": true
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | with no unevaluated items                                                        |
        | #/000/tests/001/data | true  | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems false
/* Schema: 
{
            "type": "array",
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | with no unevaluated items                                                        |
        | #/001/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems as schema
/* Schema: 
{
            "type": "array",
            "unevaluatedItems": { "type": "string" }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | with no unevaluated items                                                        |
        | #/002/tests/001/data | true  | with valid unevaluated items                                                     |
        | #/002/tests/002/data | false | with invalid unevaluated items                                                   |

Scenario Outline: unevaluatedItems with uniform items
/* Schema: 
{
            "type": "array",
            "items": { "type": "string" },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | unevaluatedItems doesn't apply                                                   |

Scenario Outline: unevaluatedItems with tuple
/* Schema: 
{
            "type": "array",
            "items": [
                { "type": "string" }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | with no unevaluated items                                                        |
        | #/004/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with additionalItems
/* Schema: 
{
            "type": "array",
            "items": [
                { "type": "string" }
            ],
            "additionalItems": true,
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | unevaluatedItems doesn't apply                                                   |

Scenario Outline: unevaluatedItems with nested tuple
/* Schema: 
{
            "type": "array",
            "items": [
                { "type": "string" }
            ],
            "allOf": [
                {
                    "items": [
                        true,
                        { "type": "number" }
                    ]
                }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | with no unevaluated items                                                        |
        | #/006/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with nested additionalItems
/* Schema: 
{
            "type": "array",
            "allOf": [
                {
                    "items": [
                        { "type": "string" }
                    ],
                    "additionalItems": true
                }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | with no additional items                                                         |
        | #/007/tests/001/data | true  | with additional items                                                            |

Scenario Outline: unevaluatedItems with nested unevaluatedItems
/* Schema: 
{
            "type": "array",
            "allOf": [
                {
                    "items": [
                        { "type": "string" }
                    ]
                },
                {
                    "unevaluatedItems": true
                }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | with no additional items                                                         |
        | #/008/tests/001/data | true  | with additional items                                                            |

Scenario Outline: unevaluatedItems with anyOf
/* Schema: 
{
            "type": "array",
            "items": [
                { "const": "foo" }
            ],
            "anyOf": [
                {
                    "items": [
                        true,
                        { "const": "bar" }
                    ]
                },
                {
                    "items": [
                        true,
                        true,
                        { "const": "baz" }
                    ]
                }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | when one schema matches and has no unevaluated items                             |
        | #/009/tests/001/data | false | when one schema matches and has unevaluated items                                |
        | #/009/tests/002/data | true  | when two schemas match and has no unevaluated items                              |
        | #/009/tests/003/data | false | when two schemas match and has unevaluated items                                 |

Scenario Outline: unevaluatedItems with oneOf
/* Schema: 
{
            "type": "array",
            "items": [
                { "const": "foo" }
            ],
            "oneOf": [
                {
                    "items": [
                        true,
                        { "const": "bar" }
                    ]
                },
                {
                    "items": [
                        true,
                        { "const": "baz" }
                    ]
                }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | true  | with no unevaluated items                                                        |
        | #/010/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with not
/* Schema: 
{
            "type": "array",
            "items": [
                { "const": "foo" }
            ],
            "not": {
                "not": {
                    "items": [
                        true,
                        { "const": "bar" }
                    ]
                }
            },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/011/tests/000/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with if/then/else
/* Schema: 
{
            "type": "array",
            "items": [
                { "const": "foo" }
            ],
            "if": {
                "items": [
                    true,
                    { "const": "bar" }
                ]
            },
            "then": {
                "items": [
                    true,
                    true,
                    { "const": "then" }
                ]
            },
            "else": {
                "items": [
                    true,
                    true,
                    true,
                    { "const": "else" }
                ]
            },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/012/tests/000/data | true  | when if matches and it has no unevaluated items                                  |
        | #/012/tests/001/data | false | when if matches and it has unevaluated items                                     |
        | #/012/tests/002/data | true  | when if doesn't match and it has no unevaluated items                            |
        | #/012/tests/003/data | false | when if doesn't match and it has unevaluated items                               |

Scenario Outline: unevaluatedItems with boolean schemas
/* Schema: 
{
            "type": "array",
            "allOf": [true],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/013/tests/000/data | true  | with no unevaluated items                                                        |
        | #/013/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with $ref
/* Schema: 
{
            "type": "array",
            "$ref": "#/$defs/bar",
            "items": [
                { "type": "string" }
            ],
            "unevaluatedItems": false,
            "$defs": {
              "bar": {
                  "items": [
                      true,
                      { "type": "string" }
                  ]
              }
            }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/014/tests/000/data | true  | with no unevaluated items                                                        |
        | #/014/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems can't see inside cousins
/* Schema: 
{
            "allOf": [
                {
                    "items": [ true ]
                },
                {
                    "unevaluatedItems": false
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/015/tests/000/data | false | always fails                                                                     |

Scenario Outline: item is evaluated in an uncle schema to unevaluatedItems
/* Schema: 
{
            "type": "object",
            "properties": {
                "foo": {
                    "type": "array",
                    "items": [
                        {
                            "type": "string"
                        }
                    ],
                    "unevaluatedItems": false
                  }
            },
            "anyOf": [
                {
                    "properties": {
                        "foo": {
                            "items": [
                                true,
                                {
                                    "type": "string"
                                }
                            ]
                        }
                    }
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/016/tests/000/data | true  | no extra items                                                                   |
        | #/016/tests/001/data | false | uncle keyword evaluation is not significant                                      |

Scenario Outline: non-array instances are valid
/* Schema: 
{"unevaluatedItems": false}
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/017/tests/000/data | true  | ignores booleans                                                                 |
        | #/017/tests/001/data | true  | ignores integers                                                                 |
        | #/017/tests/002/data | true  | ignores floats                                                                   |
        | #/017/tests/003/data | true  | ignores objects                                                                  |
        | #/017/tests/004/data | true  | ignores strings                                                                  |
        | #/017/tests/005/data | true  | ignores null                                                                     |
