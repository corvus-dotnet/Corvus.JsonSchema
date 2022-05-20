@draft2019-09

Feature: unevaluatedProperties draft2019-09
    In order to use json-schema
    As a developer
    I want to support unevaluatedProperties in draft2019-09

Scenario Outline: unevaluatedProperties true
/* Schema: 
{
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
        | #/000/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/000/tests/001/data | true  | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties schema
/* Schema: 
{
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
        | #/001/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/001/tests/001/data | true  | with valid unevaluated properties                                                |
        | #/001/tests/002/data | false | with invalid unevaluated properties                                              |

Scenario Outline: unevaluatedProperties false
/* Schema: 
{
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
        | #/002/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/002/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with adjacent properties
/* Schema: 
{
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
        | #/003/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/003/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with adjacent patternProperties
/* Schema: 
{
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
        | #/004/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/004/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with adjacent additionalProperties
/* Schema: 
{
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
        | #/005/tests/000/data | true  | with no additional properties                                                    |
        | #/005/tests/001/data | true  | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested properties
/* Schema: 
{
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
        | #/006/tests/000/data | true  | with no additional properties                                                    |
        | #/006/tests/001/data | false | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested patternProperties
/* Schema: 
{
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
        | #/007/tests/000/data | true  | with no additional properties                                                    |
        | #/007/tests/001/data | false | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested additionalProperties
/* Schema: 
{
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
        | #/008/tests/000/data | true  | with no additional properties                                                    |
        | #/008/tests/001/data | true  | with additional properties                                                       |

Scenario Outline: unevaluatedProperties with nested unevaluatedProperties
/* Schema: 
{
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
        | #/009/tests/000/data | true  | with no nested unevaluated properties                                            |
        | #/009/tests/001/data | true  | with nested unevaluated properties                                               |

Scenario Outline: unevaluatedProperties with anyOf
/* Schema: 
{
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
        | #/010/tests/000/data | true  | when one matches and has no unevaluated properties                               |
        | #/010/tests/001/data | false | when one matches and has unevaluated properties                                  |
        | #/010/tests/002/data | true  | when two match and has no unevaluated properties                                 |
        | #/010/tests/003/data | false | when two match and has unevaluated properties                                    |

Scenario Outline: unevaluatedProperties with oneOf
/* Schema: 
{
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
        | #/011/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/011/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with not
/* Schema: 
{
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
        | #/012/tests/000/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with if/then/else
/* Schema: 
{
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
        | #/013/tests/000/data | true  | when if is true and has no unevaluated properties                                |
        | #/013/tests/001/data | false | when if is true and has unevaluated properties                                   |
        | #/013/tests/002/data | true  | when if is false and has no unevaluated properties                               |
        | #/013/tests/003/data | false | when if is false and has unevaluated properties                                  |

Scenario Outline: unevaluatedProperties with if/then/else, then not defined
/* Schema: 
{
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
        | #/014/tests/000/data | false | when if is true and has no unevaluated properties                                |
        | #/014/tests/001/data | false | when if is true and has unevaluated properties                                   |
        | #/014/tests/002/data | true  | when if is false and has no unevaluated properties                               |
        | #/014/tests/003/data | false | when if is false and has unevaluated properties                                  |

Scenario Outline: unevaluatedProperties with if/then/else, else not defined
/* Schema: 
{
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
        | #/015/tests/000/data | true  | when if is true and has no unevaluated properties                                |
        | #/015/tests/001/data | false | when if is true and has unevaluated properties                                   |
        | #/015/tests/002/data | false | when if is false and has no unevaluated properties                               |
        | #/015/tests/003/data | false | when if is false and has unevaluated properties                                  |

Scenario Outline: unevaluatedProperties with dependentSchemas
/* Schema: 
{
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
        | #/016/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/016/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with boolean schemas
/* Schema: 
{
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
        | #/017/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/017/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties with $ref
/* Schema: 
{
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
        | #/018/tests/000/data | true  | with no unevaluated properties                                                   |
        | #/018/tests/001/data | false | with unevaluated properties                                                      |

Scenario Outline: unevaluatedProperties can't see inside cousins
/* Schema: 
{
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
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/019/tests/000/data | false | always fails                                                                     |

Scenario Outline: nested unevaluatedProperties, outer false, inner true, properties outside
/* Schema: 
{
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
    And the schema at "#/20/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/020/tests/000/data | true  | with no nested unevaluated properties                                            |
        | #/020/tests/001/data | true  | with nested unevaluated properties                                               |

Scenario Outline: nested unevaluatedProperties, outer false, inner true, properties inside
/* Schema: 
{
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
    And the schema at "#/21/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/021/tests/000/data | true  | with no nested unevaluated properties                                            |
        | #/021/tests/001/data | true  | with nested unevaluated properties                                               |

Scenario Outline: nested unevaluatedProperties, outer true, inner false, properties outside
/* Schema: 
{
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
    And the schema at "#/22/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/022/tests/000/data | false | with no nested unevaluated properties                                            |
        | #/022/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: nested unevaluatedProperties, outer true, inner false, properties inside
/* Schema: 
{
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
    And the schema at "#/23/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/023/tests/000/data | true  | with no nested unevaluated properties                                            |
        | #/023/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: cousin unevaluatedProperties, true and false, true with properties
/* Schema: 
{
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
    And the schema at "#/24/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/024/tests/000/data | false | with no nested unevaluated properties                                            |
        | #/024/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: cousin unevaluatedProperties, true and false, false with properties
/* Schema: 
{
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
    And the schema at "#/25/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/025/tests/000/data | true  | with no nested unevaluated properties                                            |
        | #/025/tests/001/data | false | with nested unevaluated properties                                               |

Scenario Outline: property is evaluated in an uncle schema to unevaluatedProperties
/* Schema: 
{
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
    And the schema at "#/26/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/026/tests/000/data | true  | no extra properties                                                              |
        | #/026/tests/001/data | false | uncle keyword evaluation is not significant                                      |

Scenario Outline: in-place applicator siblings, allOf has unevaluated
/* Schema: 
{
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
    And the schema at "#/27/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/027/tests/000/data | false | base case: both properties present                                               |
        | #/027/tests/001/data | true  | in place applicator siblings, bar is missing                                     |
        | #/027/tests/002/data | false | in place applicator siblings, foo is missing                                     |

Scenario Outline: in-place applicator siblings, anyOf has unevaluated
/* Schema: 
{
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
    And the schema at "#/28/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/028/tests/000/data | false | base case: both properties present                                               |
        | #/028/tests/001/data | false | in place applicator siblings, bar is missing                                     |
        | #/028/tests/002/data | true  | in place applicator siblings, foo is missing                                     |

Scenario Outline: unevaluatedProperties + single cyclic ref
/* Schema: 
{
            "type": "object",
            "properties": {
                "x": { "$ref": "#" }
            },
            "unevaluatedProperties": false
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
        | #/029/tests/000/data | true  | Empty is valid                                                                   |
        | #/029/tests/001/data | true  | Single is valid                                                                  |
        | #/029/tests/002/data | false | Unevaluated on 1st level is invalid                                              |
        | #/029/tests/003/data | true  | Nested is valid                                                                  |
        | #/029/tests/004/data | false | Unevaluated on 2nd level is invalid                                              |
        | #/029/tests/005/data | true  | Deep nested is valid                                                             |
        | #/029/tests/006/data | false | Unevaluated on 3rd level is invalid                                              |

Scenario Outline: unevaluatedProperties + ref inside allOf / oneOf
/* Schema: 
{
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
    And the schema at "#/30/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/030/tests/000/data | false | Empty is invalid (no x or y)                                                     |
        | #/030/tests/001/data | false | a and b are invalid (no x or y)                                                  |
        | #/030/tests/002/data | false | x and y are invalid                                                              |
        | #/030/tests/003/data | true  | a and x are valid                                                                |
        | #/030/tests/004/data | true  | a and y are valid                                                                |
        | #/030/tests/005/data | true  | a and b and x are valid                                                          |
        | #/030/tests/006/data | true  | a and b and y are valid                                                          |
        | #/030/tests/007/data | false | a and b and x and y are invalid                                                  |

Scenario Outline: dynamic evalation inside nested refs
/* Schema: 
{
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
    And the schema at "#/31/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/031/tests/000/data | false | Empty is invalid                                                                 |
        | #/031/tests/001/data | true  | a is valid                                                                       |
        | #/031/tests/002/data | true  | b is valid                                                                       |
        | #/031/tests/003/data | true  | c is valid                                                                       |
        | #/031/tests/004/data | true  | d is valid                                                                       |
        | #/031/tests/005/data | false | a + b is invalid                                                                 |
        | #/031/tests/006/data | false | a + c is invalid                                                                 |
        | #/031/tests/007/data | false | a + d is invalid                                                                 |
        | #/031/tests/008/data | false | b + c is invalid                                                                 |
        | #/031/tests/009/data | false | b + d is invalid                                                                 |
        | #/031/tests/010/data | false | c + d is invalid                                                                 |
        | #/031/tests/011/data | true  | xx is valid                                                                      |
        | #/031/tests/012/data | true  | xx + foox is valid                                                               |
        | #/031/tests/013/data | false | xx + foo is invalid                                                              |
        | #/031/tests/014/data | false | xx + a is invalid                                                                |
        | #/031/tests/015/data | false | xx + b is invalid                                                                |
        | #/031/tests/016/data | false | xx + c is invalid                                                                |
        | #/031/tests/017/data | false | xx + d is invalid                                                                |
        | #/031/tests/018/data | true  | all is valid                                                                     |
        | #/031/tests/019/data | true  | all + foo is valid                                                               |
        | #/031/tests/020/data | false | all + a is invalid                                                               |

Scenario Outline: non-object instances are valid
/* Schema: 
{"unevaluatedProperties": false}
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
        | #/032/tests/000/data | true  | ignores booleans                                                                 |
        | #/032/tests/001/data | true  | ignores integers                                                                 |
        | #/032/tests/002/data | true  | ignores floats                                                                   |
        | #/032/tests/003/data | true  | ignores arrays                                                                   |
        | #/032/tests/004/data | true  | ignores strings                                                                  |
        | #/032/tests/005/data | true  | ignores null                                                                     |
