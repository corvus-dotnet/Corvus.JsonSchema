@draft2019-09

Feature: unevaluatedItems draft2019-09
    In order to use json-schema
    As a developer
    I want to support unevaluatedItems in draft2019-09

Scenario Outline: unevaluatedItems true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": true
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": { "type": "string" }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "items": { "type": "string" },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "items": [
                { "type": "string" }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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

Scenario Outline: unevaluatedItems with items and additionalItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 42]
        | #/005/tests/000/data | true  | unevaluatedItems doesn't apply                                                   |

Scenario Outline: unevaluatedItems with ignored additionalItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "additionalItems": {"type": "number"},
            "unevaluatedItems": {"type": "string"}
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 1]
        | #/006/tests/000/data | false | invalid under unevaluatedItems                                                   |
        # ["foo", "bar", "baz"]
        | #/006/tests/001/data | true  | all valid under unevaluatedItems                                                 |

Scenario Outline: unevaluatedItems with ignored applicator additionalItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "allOf": [ { "additionalItems": { "type": "number" } } ],
            "unevaluatedItems": {"type": "string"}
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 1]
        | #/007/tests/000/data | false | invalid under unevaluatedItems                                                   |
        # ["foo", "bar", "baz"]
        | #/007/tests/001/data | true  | all valid under unevaluatedItems                                                 |

Scenario Outline: unevaluatedItems with nested tuple
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 42]
        | #/008/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", 42, true]
        | #/008/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with nested items
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": {"type": "boolean"},
            "anyOf": [
                { "items": {"type": "string"} },
                true
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [true, false]
        | #/009/tests/000/data | true  | with only (valid) additional items                                               |
        # ["yes", "no"]
        | #/009/tests/001/data | true  | with no additional items                                                         |
        # ["yes", false]
        | #/009/tests/002/data | false | with invalid additional item                                                     |

Scenario Outline: unevaluatedItems with nested items and additionalItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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

Scenario Outline: unevaluatedItems with nested unevaluatedItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "allOf": [
                {
                    "items": [
                        { "type": "string" }
                    ]
                },
                { "unevaluatedItems": true }
            ],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo"]
        | #/011/tests/000/data | true  | with no additional items                                                         |
        # ["foo", 42, true]
        | #/011/tests/001/data | true  | with additional items                                                            |

Scenario Outline: unevaluatedItems with anyOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar"]
        | #/012/tests/000/data | true  | when one schema matches and has no unevaluated items                             |
        # ["foo", "bar", 42]
        | #/012/tests/001/data | false | when one schema matches and has unevaluated items                                |
        # ["foo", "bar", "baz"]
        | #/012/tests/002/data | true  | when two schemas match and has no unevaluated items                              |
        # ["foo", "bar", "baz", 42]
        | #/012/tests/003/data | false | when two schemas match and has unevaluated items                                 |

Scenario Outline: unevaluatedItems with oneOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar"]
        | #/013/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo", "bar", 42]
        | #/013/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with not
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar"]
        | #/014/tests/000/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with if/then/else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "items": [ { "const": "foo" } ],
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
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar", "then"]
        | #/015/tests/000/data | true  | when if matches and it has no unevaluated items                                  |
        # ["foo", "bar", "then", "else"]
        | #/015/tests/001/data | false | when if matches and it has unevaluated items                                     |
        # ["foo", 42, 42, "else"]
        | #/015/tests/002/data | true  | when if doesn't match and it has no unevaluated items                            |
        # ["foo", 42, 42, "else", 42]
        | #/015/tests/003/data | false | when if doesn't match and it has unevaluated items                               |

Scenario Outline: unevaluatedItems with boolean schemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "allOf": [true],
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # []
        | #/016/tests/000/data | true  | with no unevaluated items                                                        |
        # ["foo"]
        | #/016/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems with $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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

Scenario Outline: unevaluatedItems before $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": false,
            "items": [
                { "type": "string" }
            ],
            "$ref": "#/$defs/bar",
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
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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

Scenario Outline: unevaluatedItems with $recursiveRef
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "https://example.com/unevaluated-items-with-recursive-ref/extended-tree",

            "$recursiveAnchor": true,

            "$ref": "./tree",
            "items": [
                true,
                true,
                { "type": "string" }
            ],

            "$defs": {
                "tree": {
                    "$id": "./tree",
                    "$recursiveAnchor": true,

                    "type": "array",
                    "items": [
                        { "type": "number" },
                        {
                            "$comment": "unevaluatedItems comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering",
                            "unevaluatedItems": false,
                            "$recursiveRef": "#"
                        }
                    ]
                }
            }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1, [2, [], "b"], "a"]
        | #/019/tests/000/data | true  | with no unevaluated items                                                        |
        # [1, [2, [], "b", "too many"], "a"]
        | #/019/tests/001/data | false | with unevaluated items                                                           |

Scenario Outline: unevaluatedItems can't see inside cousins
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "allOf": [
                {
                    "items": [ true ]
                },
                { "unevaluatedItems": false }
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/20/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ 1 ]
        | #/020/tests/000/data | false | always fails                                                                     |

Scenario Outline: item is evaluated in an uncle schema to unevaluatedItems
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {
                "foo": {
                    "items": [
                        { "type": "string" }
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
                                { "type": "string" }
                            ]
                        }
                    }
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/21/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": [ "test" ] }
        | #/021/tests/000/data | true  | no extra items                                                                   |
        # { "foo": [ "test", "test" ] }
        | #/021/tests/001/data | false | uncle keyword evaluation is not significant                                      |

Scenario Outline: non-array instances are valid
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/22/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # True
        | #/022/tests/000/data | true  | ignores booleans                                                                 |
        # 123
        | #/022/tests/001/data | true  | ignores integers                                                                 |
        # 1.0
        | #/022/tests/002/data | true  | ignores floats                                                                   |
        # {}
        | #/022/tests/003/data | true  | ignores objects                                                                  |
        # foo
        | #/022/tests/004/data | true  | ignores strings                                                                  |
        # 
        | #/022/tests/005/data | true  | ignores null                                                                     |

Scenario Outline: unevaluatedItems with null instance elements
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedItems": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/23/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ null ]
        | #/023/tests/000/data | true  | allows null elements                                                             |

Scenario Outline: unevaluatedItems can see annotations from if without then and else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "if": {
                "items": [{"const": "a"}]
            },
            "unevaluatedItems": false
        }
*/
    Given the input JSON file "unevaluatedItems.json"
    And the schema at "#/24/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [ "a" ]
        | #/024/tests/000/data | true  | valid in case if is evaluated                                                    |
        # [ "b" ]
        | #/024/tests/001/data | false | invalid in case if is evaluated                                                  |
