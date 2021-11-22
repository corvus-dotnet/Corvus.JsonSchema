@draft2019-09

Feature: ref draft2019-09
    In order to use json-schema
    As a developer
    I want to support ref in draft2019-09

Scenario Outline: root pointer ref
/* Schema: 
{
            "properties": {
                "foo": {"$ref": "#"}
            },
            "additionalProperties": false
        }
*/
    Given the input JSON file "ref.json"
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

Scenario Outline: relative pointer ref to object
/* Schema: 
{
            "properties": {
                "foo": {"type": "integer"},
                "bar": {"$ref": "#/properties/foo"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | match                                                                            |
        | #/001/tests/001/data | false | mismatch                                                                         |

Scenario Outline: relative pointer ref to array
/* Schema: 
{
            "items": [
                {"type": "integer"},
                {"$ref": "#/items/0"}
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | match array                                                                      |
        | #/002/tests/001/data | false | mismatch array                                                                   |

Scenario Outline: escaped pointer ref
/* Schema: 
{
            "$defs": {
                "tilde~field": {"type": "integer"},
                "slash/field": {"type": "integer"},
                "percent%field": {"type": "integer"}
            },
            "properties": {
                "tilde": {"$ref": "#/$defs/tilde~0field"},
                "slash": {"$ref": "#/$defs/slash~1field"},
                "percent": {"$ref": "#/$defs/percent%25field"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | false | slash invalid                                                                    |
        | #/003/tests/001/data | false | tilde invalid                                                                    |
        | #/003/tests/002/data | false | percent invalid                                                                  |
        | #/003/tests/003/data | true  | slash valid                                                                      |
        | #/003/tests/004/data | true  | tilde valid                                                                      |
        | #/003/tests/005/data | true  | percent valid                                                                    |

Scenario Outline: nested refs
/* Schema: 
{
            "$defs": {
                "a": {"type": "integer"},
                "b": {"$ref": "#/$defs/a"},
                "c": {"$ref": "#/$defs/b"}
            },
            "$ref": "#/$defs/c"
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | nested ref valid                                                                 |
        | #/004/tests/001/data | false | nested ref invalid                                                               |

Scenario Outline: ref applies alongside sibling keywords
/* Schema: 
{
            "$defs": {
                "reffed": {
                    "type": "array"
                }
            },
            "properties": {
                "foo": {
                    "$ref": "#/$defs/reffed",
                    "maxItems": 2
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | ref valid, maxItems valid                                                        |
        | #/005/tests/001/data | false | ref valid, maxItems invalid                                                      |
        | #/005/tests/002/data | false | ref invalid                                                                      |

Scenario Outline: remote ref, containing refs itself
/* Schema: 
{"$ref": "https://json-schema.org/draft/2019-09/schema"}
*/
    Given the input JSON file "ref.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | remote ref valid                                                                 |
        | #/006/tests/001/data | false | remote ref invalid                                                               |

Scenario Outline: property named $ref that is not a reference
/* Schema: 
{
            "properties": {
                "$ref": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | property named $ref valid                                                        |
        | #/007/tests/001/data | false | property named $ref invalid                                                      |

Scenario Outline: property named $ref, containing an actual $ref
/* Schema: 
{
            "properties": {
                "$ref": {"$ref": "#/$defs/is-string"}
            },
            "$defs": {
                "is-string": {
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | property named $ref valid                                                        |
        | #/008/tests/001/data | false | property named $ref invalid                                                      |

Scenario Outline: $ref to boolean schema true
/* Schema: 
{
            "$ref": "#/$defs/bool",
            "$defs": {
                "bool": true
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: $ref to boolean schema false
/* Schema: 
{
            "$ref": "#/$defs/bool",
            "$defs": {
                "bool": false
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: Recursive references between schemas
/* Schema: 
{
            "$id": "http://localhost:1234/tree",
            "description": "tree of nodes",
            "type": "object",
            "properties": {
                "meta": {"type": "string"},
                "nodes": {
                    "type": "array",
                    "items": {"$ref": "node"}
                }
            },
            "required": ["meta", "nodes"],
            "$defs": {
                "node": {
                    "$id": "http://localhost:1234/node",
                    "description": "node",
                    "type": "object",
                    "properties": {
                        "value": {"type": "number"},
                        "subtree": {"$ref": "tree"}
                    },
                    "required": ["value"]
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/011/tests/000/data | true  | valid tree                                                                       |
        | #/011/tests/001/data | false | invalid tree                                                                     |

Scenario Outline: refs with quote
/* Schema: 
{
            "properties": {
                "foo\"bar": {"$ref": "#/$defs/foo%22bar"}
            },
            "$defs": {
                "foo\"bar": {"type": "number"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/012/tests/000/data | true  | object with numbers is valid                                                     |
        | #/012/tests/001/data | false | object with strings is invalid                                                   |

Scenario Outline: ref creates new scope when adjacent to keywords
/* Schema: 
{
            "$defs": {
                "A": {
                    "unevaluatedProperties": false
                }
            },
            "properties": {
                "prop1": {
                    "type": "string"
                }
            },
            "$ref": "#/$defs/A"
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/013/tests/000/data | false | referenced subschema doesn't see annotations from properties                     |

Scenario Outline: naive replacement of $ref with its destination is not correct
/* Schema: 
{
            "$defs": {
                "a_string": { "type": "string" }
            },
            "enum": [
                { "$ref": "#/$defs/a_string" }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/014/tests/000/data | false | do not evaluate the $ref inside the enum, matching any string                    |
        | #/014/tests/001/data | false | do not evaluate the $ref inside the enum, definition exact match                 |
        | #/014/tests/002/data | true  | match the enum exactly                                                           |
