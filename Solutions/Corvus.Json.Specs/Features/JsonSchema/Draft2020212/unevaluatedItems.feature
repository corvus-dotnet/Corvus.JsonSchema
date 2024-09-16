@draft2020-12

Feature: unevaluatedItems draft2020-12
    In order to use json-schema
    As a developer
    I want to support unevaluatedItems in draft2020-12

Scenario Outline: unevaluatedItems true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # []
        | #/000/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo"]
        | #/000/tests/001/data | true  | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # []
        | #/001/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo"]
        | #/001/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems as schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # []
        | #/002/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo"]
        | #/002/tests/001/data | true  | with valid unevaluated items                                                     |
        # [42]
        | #/002/tests/002/data | false | with invalid unevaluated items                                                   |

Scenario Outline: unevaluatedItems with uniform items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # ["foo", "bar"]
        | #/003/tests/000/data | true  | unevaluatedItems doesn't apply                                                   |

Scenario Outline: unevaluatedItems with tuple
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
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
        # ["foo"]
        | #/004/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", "bar"]
        | #/004/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with items and prefixItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                { "type": "string" }
            ],
            "items": true,
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
        # ["foo", 42]
        | #/005/tests/000/data | true  | unevaluatedItems doesn't apply                                                   |

Scenario Outline: unevaluatedItems with items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "items": {"type": "number"},
            "unevaluatedItems": {"type": "string"}
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
        # [5, 6, 7, 8]
        | #/006/tests/000/data | true  | valid under items                                                                |
        # ["foo", "bar", "baz"]
        | #/006/tests/001/data | false | invalid under items                                                              |

Scenario Outline: unevaluatedItems with nested tuple
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                { "type": "string" }
            ],
            "allOf": [
                {
                    "prefixItems": [
                        true,
                        { "type": "number" }
                    ]
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
        # ["foo", 42]
        | #/007/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", 42, true]
        | #/007/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with nested items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "unevaluatedItems": {"type": "boolean"},
            "anyOf": [
                { "items": {"type": "string"} },
                true
            ]
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
        # [true, false]
        | #/008/tests/000/data | true  | with only (valid) additional items                                               |
        # ["yes", "no"]
        | #/008/tests/001/data | true  | with no additional items                                                         |
        # ["yes", false]
        | #/008/tests/002/data | false | with invalid additional item                                                     |

Scenario Outline: unevaluatedItems with nested prefixItems and items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                {
                    "prefixItems": [
                        { "type": "string" }
                    ],
                    "items": true
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
        # ["foo"]
        | #/009/tests/000/data | true  | with no additional items                                                         |
        # ["foo", 42, true]
        | #/009/tests/001/data | true  | with additional items                                                            |

Scenario Outline: unevaluatedItems with nested unevaluatedItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                {
                    "prefixItems": [
                        { "type": "string" }
                    ]
                },
                { "unevaluatedItems": true }
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
        # ["foo"]
        | #/010/tests/000/data | true  | with no additional items                                                         |
        # ["foo", 42, true]
        | #/010/tests/001/data | true  | with additional items                                                            |

Scenario Outline: unevaluatedItems with anyOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                { "const": "foo" }
            ],
            "anyOf": [
                {
                    "prefixItems": [
                        true,
                        { "const": "bar" }
                    ]
                },
                {
                    "prefixItems": [
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
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar"]
        | #/011/tests/000/data | true  | when one schema matches and has no unevaluated items                             |
        # ["foo", "bar", 42]
        | #/011/tests/001/data | false | when one schema matches and has unevaluated items                                |
        # ["foo", "bar", "baz"]
        | #/011/tests/002/data | true  | when two schemas match and has no unevaluated items                              |
        # ["foo", "bar", "baz", 42]
        | #/011/tests/003/data | false | when two schemas match and has unevaluated items                                 |

Scenario Outline: unevaluatedItems with oneOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                { "const": "foo" }
            ],
            "oneOf": [
                {
                    "prefixItems": [
                        true,
                        { "const": "bar" }
                    ]
                },
                {
                    "prefixItems": [
                        true,
                        { "const": "baz" }
                    ]
                }
            ],
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
        # ["foo", "bar"]
        | #/012/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", "bar", 42]
        | #/012/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with not
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                { "const": "foo" }
            ],
            "not": {
                "not": {
                    "prefixItems": [
                        true,
                        { "const": "bar" }
                    ]
                }
            },
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
        # ["foo", "bar"]
        | #/013/tests/000/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with if/then/else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [
                { "const": "foo" }
            ],
            "if": {
                "prefixItems": [
                    true,
                    { "const": "bar" }
                ]
            },
            "then": {
                "prefixItems": [
                    true,
                    true,
                    { "const": "then" }
                ]
            },
            "else": {
                "prefixItems": [
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
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar", "then"]
        | #/014/tests/000/data | true  | when if matches and it has no unevaluated items                                  |
        # ["foo", "bar", "then", "else"]
        | #/014/tests/001/data | false | when if matches and it has unevaluated items                                     |
        # ["foo", 42, 42, "else"]
        | #/014/tests/002/data | true  | when if doesn't match and it has no unevaluated items                            |
        # ["foo", 42, 42, "else", 42]
        | #/014/tests/003/data | false | when if doesn't match and it has unevaluated items                               |

Scenario Outline: unevaluatedItems with boolean schemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [true],
            "unevaluatedItems": false
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
        # []
        | #/015/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo"]
        | #/015/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "#/$defs/bar",
            "prefixItems": [
                { "type": "string" }
            ],
            "unevaluatedItems": false,
            "$defs": {
              "bar": {
                  "prefixItems": [
                      true,
                      { "type": "string" }
                  ]
              }
            }
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
        # ["foo", "bar"]
        | #/016/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", "bar", "baz"]
        | #/016/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems before $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "unevaluatedItems": false,
            "prefixItems": [
                { "type": "string" }
            ],
            "$ref": "#/$defs/bar",
            "$defs": {
              "bar": {
                  "prefixItems": [
                      true,
                      { "type": "string" }
                  ]
              }
            }
        }
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
        # ["foo", "bar"]
        | #/017/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", "bar", "baz"]
        | #/017/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with $dynamicRef
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "https://example.com/unevaluated-items-with-dynamic-ref/derived",

            "$ref": "./baseSchema",

            "$defs": {
                "derived": {
                    "$dynamicAnchor": "addons",
                    "prefixItems": [
                        true,
                        { "type": "string" }
                    ]
                },
                "baseSchema": {
                    "$id": "./baseSchema",

                    "$comment": "unevaluatedItems comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering",
                    "unevaluatedItems": false,
                    "type": "array",
                    "prefixItems": [
                        { "type": "string" }
                    ],
                    "$dynamicRef": "#addons",

                    "$defs": {
                        "defaultAddons": {
                            "$comment": "Needed to satisfy the bookending requirement",
                            "$dynamicAnchor": "addons"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar"]
        | #/018/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", "bar", "baz"]
        | #/018/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems can't see inside cousins
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                {
                    "prefixItems": [ true ]
                },
                { "unevaluatedItems": false }
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 1 ]
        | #/019/tests/000/data | false | always fails                                                                     |

Scenario Outline: item is evaluated in an uncle schema to unevaluatedItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": {
                "foo": {
                    "prefixItems": [
                        { "type": "string" }
                    ],
                    "unevaluatedItems": false
                  }
            },
            "anyOf": [
                {
                    "properties": {
                        "foo": {
                            "prefixItems": [
                                true,
                                { "type": "string" }
                            ]
                        }
                    }
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/20/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": [ "test" ] }
        | #/020/tests/000/data | true  | no extra items                                                                   |
        # { "foo": [ "test", "test" ] }
        | #/020/tests/001/data | false | uncle keyword evaluation is not significant                                      |

Scenario Outline: unevaluatedItems depends on adjacent contains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "prefixItems": [true],
            "contains": {"type": "string"},
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/21/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 1, "foo" ]
        | #/021/tests/000/data | true  | second item is evaluated by contains                                             |
        # [ 1, 2 ]
        | #/021/tests/001/data | false | contains fails, second item is not evaluated                                     |
        # [ 1, 2, "foo" ]
        | #/021/tests/002/data | false | contains passes, second item is not evaluated                                    |

Scenario Outline: unevaluatedItems depends on multiple nested contains
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                { "contains": { "multipleOf": 2 } },
                { "contains": { "multipleOf": 3 } }
            ],
            "unevaluatedItems": { "multipleOf": 5 }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/22/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 2, 3, 4, 5, 6 ]
        | #/022/tests/000/data | true  | 5 not evaluated, passes unevaluatedItems                                         |
        # [ 2, 3, 4, 7, 8 ]
        | #/022/tests/001/data | false | 7 not evaluated, fails unevaluatedItems                                          |

Scenario Outline: unevaluatedItems and contains interact to control item dependency relationship
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "if": {
                "contains": {"const": "a"}
            },
            "then": {
                "if": {
                    "contains": {"const": "b"}
                },
                "then": {
                    "if": {
                        "contains": {"const": "c"}
                    }
                }
            },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/23/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # []
        | #/023/tests/000/data | true  | empty array is valid                                                             |
        # [ "a", "a" ]
        | #/023/tests/001/data | true  | only a's are valid                                                               |
        # [ "a", "b", "a", "b", "a" ]
        | #/023/tests/002/data | true  | a's and b's are valid                                                            |
        # [ "c", "a", "c", "c", "b", "a" ]
        | #/023/tests/003/data | true  | a's, b's and c's are valid                                                       |
        # [ "b", "b" ]
        | #/023/tests/004/data | false | only b's are invalid                                                             |
        # [ "c", "c" ]
        | #/023/tests/005/data | false | only c's are invalid                                                             |
        # [ "c", "b", "c", "b", "c" ]
        | #/023/tests/006/data | false | only b's and c's are invalid                                                     |
        # [ "c", "a", "c", "a", "c" ]
        | #/023/tests/007/data | false | only a's and c's are invalid                                                     |

Scenario Outline: non-array instances are valid
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/24/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # True
        | #/024/tests/000/data | true  | ignores booleans                                                                 |
        # 123
        | #/024/tests/001/data | true  | ignores integers                                                                 |
        # 1.0
        | #/024/tests/002/data | true  | ignores floats                                                                   |
        # {}
        | #/024/tests/003/data | true  | ignores objects                                                                  |
        # foo
        | #/024/tests/004/data | true  | ignores strings                                                                  |
        # 
        | #/024/tests/005/data | true  | ignores null                                                                     |

Scenario Outline: unevaluatedItems with null instance elements
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "unevaluatedItems": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/25/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/025/tests/000/data | true  | allows null elements                                                             |

Scenario Outline: unevaluatedItems can see annotations from if without then and else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "if": {
                "prefixItems": [{"const": "a"}]
            },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/26/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ "a" ]
        | #/026/tests/000/data | true  | valid in case if is evaluated                                                    |
        # [ "b" ]
        | #/026/tests/001/data | false | invalid in case if is evaluated                                                  |
