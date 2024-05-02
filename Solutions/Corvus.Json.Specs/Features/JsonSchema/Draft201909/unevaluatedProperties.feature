@draft2019-09

Feature: unevaluatedProperties draft2019-09
    In order to use json-schema
    As a developer
    I want to support unevaluatedProperties in draft2019-09

Scenario Outline: unevaluatedProperties true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "unevaluatedProperties": true
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/000/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo" }
        | #/000/tests/001/data | true  | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "unevaluatedProperties": {
                "type": "string",
                "minLength": 3
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/001/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo" }
        | #/001/tests/001/data | true  | with valid unevaluated properties                                                |
        # { "foo": "fo" }
        | #/001/tests/002/data | false | with invalid unevaluated properties                                              |

Scenario Outline: unevaluatedProperties false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/002/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo" }
        | #/002/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with adjacent properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/003/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo", "bar": "bar" }
        | #/003/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with adjacent patternProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "patternProperties": {
                "^foo": { "type": "string" }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/004/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo", "bar": "bar" }
        | #/004/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with adjacent additionalProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "additionalProperties": true,
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/005/tests/000/data | true  | with no additional properties                                                    |
        # { "foo": "foo", "bar": "bar" }
        | #/005/tests/001/data | true  | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [
                {
                    "properties": {
                        "bar": { "type": "string" }
                    }
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/006/tests/000/data | true  | with no additional properties                                                    |
        # { "foo": "foo", "bar": "bar", "baz": "baz" }
        | #/006/tests/001/data | false | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested patternProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [
              {
                  "patternProperties": {
                      "^bar": { "type": "string" }
                  }
              }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/007/tests/000/data | true  | with no additional properties                                                    |
        # { "foo": "foo", "bar": "bar", "baz": "baz" }
        | #/007/tests/001/data | false | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested additionalProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [
                {
                    "additionalProperties": true
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/008/tests/000/data | true  | with no additional properties                                                    |
        # { "foo": "foo", "bar": "bar" }
        | #/008/tests/001/data | true  | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested unevaluatedProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [
                {
                    "unevaluatedProperties": true
                }
            ],
            "unevaluatedProperties": {
                "type": "string",
                "maxLength": 2
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/009/tests/000/data | true  | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/009/tests/001/data | true  | with nested unevaluated properties                                               |

Scenario Outline: unevaluatedProperties with anyOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "anyOf": [
                {
                    "properties": {
                        "bar": { "const": "bar" }
                    },
                    "required": ["bar"]
                },
                {
                    "properties": {
                        "baz": { "const": "baz" }
                    },
                    "required": ["baz"]
                },
                {
                    "properties": {
                        "quux": { "const": "quux" }
                    },
                    "required": ["quux"]
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/010/tests/000/data | true  | when one matches and has no unevaluated properties                               |
        # { "foo": "foo", "bar": "bar", "baz": "not-baz" }
        | #/010/tests/001/data | false | when one matches and has unevaluated properties                                  |
        # { "foo": "foo", "bar": "bar", "baz": "baz" }
        | #/010/tests/002/data | true  | when two match and has no unevaluated properties                                 |
        # { "foo": "foo", "bar": "bar", "baz": "baz", "quux": "not-quux" }
        | #/010/tests/003/data | false | when two match and has unevaluated properties                                    |

Scenario Outline: unevaluatedProperties with oneOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "oneOf": [
                {
                    "properties": {
                        "bar": { "const": "bar" }
                    },
                    "required": ["bar"]
                },
                {
                    "properties": {
                        "baz": { "const": "baz" }
                    },
                    "required": ["baz"]
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/011/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo", "bar": "bar", "quux": "quux" }
        | #/011/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with not
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "not": {
                "not": {
                    "properties": {
                        "bar": { "const": "bar" }
                    },
                    "required": ["bar"]
                }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/012/tests/000/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with if/then/else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "if": {
                "properties": {
                    "foo": { "const": "then" }
                },
                "required": ["foo"]
            },
            "then": {
                "properties": {
                    "bar": { "type": "string" }
                },
                "required": ["bar"]
            },
            "else": {
                "properties": {
                    "baz": { "type": "string" }
                },
                "required": ["baz"]
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "then", "bar": "bar" }
        | #/013/tests/000/data | true  | when if is true and has no unevaluated properties                                |
        # { "foo": "then", "bar": "bar", "baz": "baz" }
        | #/013/tests/001/data | false | when if is true and has unevaluated properties                                   |
        # { "baz": "baz" }
        | #/013/tests/002/data | true  | when if is false and has no unevaluated properties                               |
        # { "foo": "else", "baz": "baz" }
        | #/013/tests/003/data | false | when if is false and has unevaluated properties                                  |

Scenario Outline: unevaluatedProperties with if/then/else, then not defined
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "if": {
                "properties": {
                    "foo": { "const": "then" }
                },
                "required": ["foo"]
            },
            "else": {
                "properties": {
                    "baz": { "type": "string" }
                },
                "required": ["baz"]
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "then", "bar": "bar" }
        | #/014/tests/000/data | false | when if is true and has no unevaluated properties                                |
        # { "foo": "then", "bar": "bar", "baz": "baz" }
        | #/014/tests/001/data | false | when if is true and has unevaluated properties                                   |
        # { "baz": "baz" }
        | #/014/tests/002/data | true  | when if is false and has no unevaluated properties                               |
        # { "foo": "else", "baz": "baz" }
        | #/014/tests/003/data | false | when if is false and has unevaluated properties                                  |

Scenario Outline: unevaluatedProperties with if/then/else, else not defined
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "if": {
                "properties": {
                    "foo": { "const": "then" }
                },
                "required": ["foo"]
            },
            "then": {
                "properties": {
                    "bar": { "type": "string" }
                },
                "required": ["bar"]
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "then", "bar": "bar" }
        | #/015/tests/000/data | true  | when if is true and has no unevaluated properties                                |
        # { "foo": "then", "bar": "bar", "baz": "baz" }
        | #/015/tests/001/data | false | when if is true and has unevaluated properties                                   |
        # { "baz": "baz" }
        | #/015/tests/002/data | false | when if is false and has no unevaluated properties                               |
        # { "foo": "else", "baz": "baz" }
        | #/015/tests/003/data | false | when if is false and has unevaluated properties                                  |

Scenario Outline: unevaluatedProperties with dependentSchemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "dependentSchemas": {
                "foo": {
                    "properties": {
                        "bar": { "const": "bar" }
                    },
                    "required": ["bar"]
                }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/016/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "bar": "bar" }
        | #/016/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with boolean schemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [true],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/017/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "bar": "bar" }
        | #/017/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "$ref": "#/$defs/bar",
            "properties": {
                "foo": { "type": "string" }
            },
            "unevaluatedProperties": false,
            "$defs": {
                "bar": {
                    "properties": {
                        "bar": { "type": "string" }
                    }
                }
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/018/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo", "bar": "bar", "baz": "baz" }
        | #/018/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties before $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "unevaluatedProperties": false,
            "properties": {
                "foo": { "type": "string" }
            },
            "$ref": "#/$defs/bar",
            "$defs": {
                "bar": {
                    "properties": {
                        "bar": { "type": "string" }
                    }
                }
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo", "bar": "bar" }
        | #/019/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "foo": "foo", "bar": "bar", "baz": "baz" }
        | #/019/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with $recursiveRef
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "https://example.com/unevaluated-properties-with-recursive-ref/extended-tree",

            "$recursiveAnchor": true,

            "$ref": "./tree",
            "properties": {
                "name": { "type": "string" }
            },

            "$defs": {
                "tree": {
                    "$id": "./tree",
                    "$recursiveAnchor": true,

                    "type": "object",
                    "properties": {
                        "node": true,
                        "branches": {
                            "$comment": "unevaluatedProperties comes first so it's more likely to bugs errors with implementations that are sensitive to keyword ordering",
                            "unevaluatedProperties": false,
                            "$recursiveRef": "#"
                        }
                    },
                    "required": ["node"]
                }
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/20/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "name": "a", "node": 1, "branches": { "name": "b", "node": 2 } }
        | #/020/tests/000/data | true  | with no unevaluated properties                                                   |
        # { "name": "a", "node": 1, "branches": { "foo": "b", "node": 2 } }
        | #/020/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties can't see inside cousins
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "allOf": [
                {
                    "properties": {
                        "foo": true
                    }
                },
                {
                    "unevaluatedProperties": false
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/21/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": 1 }
        | #/021/tests/000/data | false | always fails                                                                     |

Scenario Outline: unevaluatedProperties can't see inside cousins (reverse order)
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "allOf": [
                {
                    "unevaluatedProperties": false
                },
                {
                    "properties": {
                        "foo": true
                    }
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/22/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": 1 }
        | #/022/tests/000/data | false | always fails                                                                     |

Scenario Outline: nested unevaluatedProperties, outer false, inner true, properties outside
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [
                {
                    "unevaluatedProperties": true
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/23/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/023/tests/000/data | true  | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/023/tests/001/data | true  | with nested unevaluated properties                                               |

Scenario Outline: nested unevaluatedProperties, outer false, inner true, properties inside
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "allOf": [
                {
                    "properties": {
                        "foo": { "type": "string" }
                    },
                    "unevaluatedProperties": true
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/24/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/024/tests/000/data | true  | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/024/tests/001/data | true  | with nested unevaluated properties                                               |

Scenario Outline: nested unevaluatedProperties, outer true, inner false, properties outside
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": { "type": "string" }
            },
            "allOf": [
                {
                    "unevaluatedProperties": false
                }
            ],
            "unevaluatedProperties": true
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/25/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/025/tests/000/data | false | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/025/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: nested unevaluatedProperties, outer true, inner false, properties inside
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "allOf": [
                {
                    "properties": {
                        "foo": { "type": "string" }
                    },
                    "unevaluatedProperties": false
                }
            ],
            "unevaluatedProperties": true
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/26/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/026/tests/000/data | true  | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/026/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: cousin unevaluatedProperties, true and false, true with properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "allOf": [
                {
                    "properties": {
                        "foo": { "type": "string" }
                    },
                    "unevaluatedProperties": true
                },
                {
                    "unevaluatedProperties": false
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/27/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/027/tests/000/data | false | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/027/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: cousin unevaluatedProperties, true and false, false with properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "allOf": [
                {
                    "unevaluatedProperties": true
                },
                {
                    "properties": {
                        "foo": { "type": "string" }
                    },
                    "unevaluatedProperties": false
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/28/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "foo" }
        | #/028/tests/000/data | true  | with no nested unevaluated properties                                            |
        # { "foo": "foo", "bar": "bar" }
        | #/028/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: property is evaluated in an uncle schema to unevaluatedProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "foo": {
                    "type": "object",
                    "properties": {
                        "bar": {
                            "type": "string"
                        }
                    },
                    "unevaluatedProperties": false
                  }
            },
            "anyOf": [
                {
                    "properties": {
                        "foo": {
                            "properties": {
                                "faz": {
                                    "type": "string"
                                }
                            }
                        }
                    }
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/29/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": { "bar": "test" } }
        | #/029/tests/000/data | true  | no extra properties                                                              |
        # { "foo": { "bar": "test", "faz": "test" } }
        | #/029/tests/001/data | false | uncle keyword evaluation is not significant                                      |

Scenario Outline: in-place applicator siblings, allOf has unevaluated
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "allOf": [
                {
                    "properties": {
                        "foo": true
                    },
                    "unevaluatedProperties": false
                }
            ],
            "anyOf": [
                {
                    "properties": {
                        "bar": true
                    }
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/30/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": 1, "bar": 1 }
        | #/030/tests/000/data | false | base case: both properties present                                               |
        # { "foo": 1 }
        | #/030/tests/001/data | true  | in place applicator siblings, bar is missing                                     |
        # { "bar": 1 }
        | #/030/tests/002/data | false | in place applicator siblings, foo is missing                                     |

Scenario Outline: in-place applicator siblings, anyOf has unevaluated
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "allOf": [
                {
                    "properties": {
                        "foo": true
                    }
                }
            ],
            "anyOf": [
                {
                    "properties": {
                        "bar": true
                    },
                    "unevaluatedProperties": false
                }
            ]
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/31/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": 1, "bar": 1 }
        | #/031/tests/000/data | false | base case: both properties present                                               |
        # { "foo": 1 }
        | #/031/tests/001/data | false | in place applicator siblings, bar is missing                                     |
        # { "bar": 1 }
        | #/031/tests/002/data | true  | in place applicator siblings, foo is missing                                     |

Scenario Outline: unevaluatedProperties + single cyclic ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "object",
            "properties": {
                "x": { "$ref": "#" }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/32/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/032/tests/000/data | true  | Empty is valid                                                                   |
        # { "x": {} }
        | #/032/tests/001/data | true  | Single is valid                                                                  |
        # { "x": {}, "y": {} }
        | #/032/tests/002/data | false | Unevaluated on 1st level is invalid                                              |
        # { "x": { "x": {} } }
        | #/032/tests/003/data | true  | Nested is valid                                                                  |
        # { "x": { "x": {}, "y": {} } }
        | #/032/tests/004/data | false | Unevaluated on 2nd level is invalid                                              |
        # { "x": { "x": { "x": {} } } }
        | #/032/tests/005/data | true  | Deep nested is valid                                                             |
        # { "x": { "x": { "x": {}, "y": {} } } }
        | #/032/tests/006/data | false | Unevaluated on 3rd level is invalid                                              |

Scenario Outline: unevaluatedProperties + ref inside allOf / oneOf
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$defs": {
                "one": {
                    "properties": { "a": true }
                },
                "two": {
                    "required": ["x"],
                    "properties": { "x": true }
                }
            },
            "allOf": [
                { "$ref": "#/$defs/one" },
                { "properties": { "b": true } },
                {
                    "oneOf": [
                        { "$ref": "#/$defs/two" },
                        {
                            "required": ["y"],
                            "properties": { "y": true }
                        }
                    ]
                }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/33/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/033/tests/000/data | false | Empty is invalid (no x or y)                                                     |
        # { "a": 1, "b": 1 }
        | #/033/tests/001/data | false | a and b are invalid (no x or y)                                                  |
        # { "x": 1, "y": 1 }
        | #/033/tests/002/data | false | x and y are invalid                                                              |
        # { "a": 1, "x": 1 }
        | #/033/tests/003/data | true  | a and x are valid                                                                |
        # { "a": 1, "y": 1 }
        | #/033/tests/004/data | true  | a and y are valid                                                                |
        # { "a": 1, "b": 1, "x": 1 }
        | #/033/tests/005/data | true  | a and b and x are valid                                                          |
        # { "a": 1, "b": 1, "y": 1 }
        | #/033/tests/006/data | true  | a and b and y are valid                                                          |
        # { "a": 1, "b": 1, "x": 1, "y": 1 }
        | #/033/tests/007/data | false | a and b and x and y are invalid                                                  |

Scenario Outline: dynamic evalation inside nested refs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$defs": {
                "one": {
                    "oneOf": [
                        { "$ref": "#/$defs/two" },
                        { "required": ["b"], "properties": { "b": true } },
                        { "required": ["xx"], "patternProperties": { "x": true } },
                        { "required": ["all"], "unevaluatedProperties": true }
                    ]
                },
                "two": {
                    "oneOf": [
                        { "required": ["c"], "properties": { "c": true } },
                        { "required": ["d"], "properties": { "d": true } }
                    ]
                }
            },
            "oneOf": [
                { "$ref": "#/$defs/one" },
                { "required": ["a"], "properties": { "a": true } }
            ],
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/34/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {}
        | #/034/tests/000/data | false | Empty is invalid                                                                 |
        # { "a": 1 }
        | #/034/tests/001/data | true  | a is valid                                                                       |
        # { "b": 1 }
        | #/034/tests/002/data | true  | b is valid                                                                       |
        # { "c": 1 }
        | #/034/tests/003/data | true  | c is valid                                                                       |
        # { "d": 1 }
        | #/034/tests/004/data | true  | d is valid                                                                       |
        # { "a": 1, "b": 1 }
        | #/034/tests/005/data | false | a + b is invalid                                                                 |
        # { "a": 1, "c": 1 }
        | #/034/tests/006/data | false | a + c is invalid                                                                 |
        # { "a": 1, "d": 1 }
        | #/034/tests/007/data | false | a + d is invalid                                                                 |
        # { "b": 1, "c": 1 }
        | #/034/tests/008/data | false | b + c is invalid                                                                 |
        # { "b": 1, "d": 1 }
        | #/034/tests/009/data | false | b + d is invalid                                                                 |
        # { "c": 1, "d": 1 }
        | #/034/tests/010/data | false | c + d is invalid                                                                 |
        # { "xx": 1 }
        | #/034/tests/011/data | true  | xx is valid                                                                      |
        # { "xx": 1, "foox": 1 }
        | #/034/tests/012/data | true  | xx + foox is valid                                                               |
        # { "xx": 1, "foo": 1 }
        | #/034/tests/013/data | false | xx + foo is invalid                                                              |
        # { "xx": 1, "a": 1 }
        | #/034/tests/014/data | false | xx + a is invalid                                                                |
        # { "xx": 1, "b": 1 }
        | #/034/tests/015/data | false | xx + b is invalid                                                                |
        # { "xx": 1, "c": 1 }
        | #/034/tests/016/data | false | xx + c is invalid                                                                |
        # { "xx": 1, "d": 1 }
        | #/034/tests/017/data | false | xx + d is invalid                                                                |
        # { "all": 1 }
        | #/034/tests/018/data | true  | all is valid                                                                     |
        # { "all": 1, "foo": 1 }
        | #/034/tests/019/data | true  | all + foo is valid                                                               |
        # { "all": 1, "a": 1 }
        | #/034/tests/020/data | false | all + a is invalid                                                               |

Scenario Outline: non-object instances are valid
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/35/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # True
        | #/035/tests/000/data | true  | ignores booleans                                                                 |
        # 123
        | #/035/tests/001/data | true  | ignores integers                                                                 |
        # 1.0
        | #/035/tests/002/data | true  | ignores floats                                                                   |
        # []
        | #/035/tests/003/data | true  | ignores arrays                                                                   |
        # foo
        | #/035/tests/004/data | true  | ignores strings                                                                  |
        # 
        | #/035/tests/005/data | true  | ignores null                                                                     |

Scenario Outline: unevaluatedProperties with null valued instance properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "unevaluatedProperties": {
                "type": "null"
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/36/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": null}
        | #/036/tests/000/data | true  | allows null valued properties                                                    |

Scenario Outline: unevaluatedProperties not affected by propertyNames
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "propertyNames": {"maxLength": 1},
            "unevaluatedProperties": {
                "type": "number"
            }
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/37/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"a": 1}
        | #/037/tests/000/data | true  | allows only number properties                                                    |
        # {"a": "b"}
        | #/037/tests/001/data | false | string property is invalid                                                       |

Scenario Outline: unevaluatedProperties can see annotations from if without then and else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "if": {
                "patternProperties": {
                    "foo": {
                        "type": "string"
                    }
                }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/38/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "a" }
        | #/038/tests/000/data | true  | valid in case if is evaluated                                                    |
        # { "bar": "a" }
        | #/038/tests/001/data | false | invalid in case if is evaluated                                                  |

Scenario Outline: dependentSchemas with unevaluatedProperties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "properties": {"foo2": {}},
            "dependentSchemas": {
                "foo" : {},
                "foo2": {
                    "properties": {
                        "bar":{}
                    }
                }
            },
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "unevaluatedProperties.json"
    And the schema at "#/39/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": ""}
        | #/039/tests/000/data | false | unevaluatedProperties doesn't consider dependentSchemas                          |
        # {"bar": ""}
        | #/039/tests/001/data | false | unevaluatedProperties doesn't see bar when foo2 is absent                        |
        # { "foo2": "", "bar": ""}
        | #/039/tests/002/data | true  | unevaluatedProperties sees bar when foo2 is present                              |
