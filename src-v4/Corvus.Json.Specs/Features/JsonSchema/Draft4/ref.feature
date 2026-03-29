@draft4

Feature: ref draft4
    In order to use json-schema
    As a developer
    I want to support ref in draft4

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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": false}
        | #/000/tests/000/data | true  | match                                                                            |
        # {"foo": {"foo": false}}
        | #/000/tests/001/data | true  | recursive match                                                                  |
        # {"bar": false}
        | #/000/tests/002/data | false | mismatch                                                                         |
        # {"foo": {"bar": false}}
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"bar": 3}
        | #/001/tests/000/data | true  | match                                                                            |
        # {"bar": true}
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
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1, 2]
        | #/002/tests/000/data | true  | match array                                                                      |
        # [1, "foo"]
        | #/002/tests/001/data | false | mismatch array                                                                   |

Scenario Outline: escaped pointer ref
/* Schema: 
{
            "definitions": {
                "tilde~field": {"type": "integer"},
                "slash/field": {"type": "integer"},
                "percent%field": {"type": "integer"}
            },
            "properties": {
                "tilde": {"$ref": "#/definitions/tilde~0field"},
                "slash": {"$ref": "#/definitions/slash~1field"},
                "percent": {"$ref": "#/definitions/percent%25field"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"slash": "aoeu"}
        | #/003/tests/000/data | false | slash invalid                                                                    |
        # {"tilde": "aoeu"}
        | #/003/tests/001/data | false | tilde invalid                                                                    |
        # {"percent": "aoeu"}
        | #/003/tests/002/data | false | percent invalid                                                                  |
        # {"slash": 123}
        | #/003/tests/003/data | true  | slash valid                                                                      |
        # {"tilde": 123}
        | #/003/tests/004/data | true  | tilde valid                                                                      |
        # {"percent": 123}
        | #/003/tests/005/data | true  | percent valid                                                                    |

Scenario Outline: nested refs
/* Schema: 
{
            "definitions": {
                "a": {"type": "integer"},
                "b": {"$ref": "#/definitions/a"},
                "c": {"$ref": "#/definitions/b"}
            },
            "allOf": [{ "$ref": "#/definitions/c" }]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 5
        | #/004/tests/000/data | true  | nested ref valid                                                                 |
        # a
        | #/004/tests/001/data | false | nested ref invalid                                                               |

Scenario Outline: ref overrides any sibling keywords
/* Schema: 
{
            "definitions": {
                "reffed": {
                    "type": "array"
                }
            },
            "properties": {
                "foo": {
                    "$ref": "#/definitions/reffed",
                    "maxItems": 2
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": [] }
        | #/005/tests/000/data | true  | ref valid                                                                        |
        # { "foo": [ 1, 2, 3] }
        | #/005/tests/001/data | true  | ref valid, maxItems ignored                                                      |
        # { "foo": "string" }
        | #/005/tests/002/data | false | ref invalid                                                                      |

Scenario Outline: $ref prevents a sibling id from changing the base uri
/* Schema: 
{
            "id": "http://localhost:1234/sibling_id/base/",
            "definitions": {
                "foo": {
                    "id": "http://localhost:1234/sibling_id/foo.json",
                    "type": "string"
                },
                "base_foo": {
                    "$comment": "this canonical uri is http://localhost:1234/sibling_id/base/foo.json",
                    "id": "foo.json",
                    "type": "number"
                }
            },
            "allOf": [
                {
                    "$comment": "$ref resolves to http://localhost:1234/sibling_id/base/foo.json, not http://localhost:1234/sibling_id/foo.json",
                    "id": "http://localhost:1234/sibling_id/",
                    "$ref": "foo.json"
                }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # a
        | #/006/tests/000/data | false | $ref resolves to /definitions/base_foo, data does not validate                   |
        # 1
        | #/006/tests/001/data | true  | $ref resolves to /definitions/base_foo, data validates                           |

Scenario Outline: remote ref, containing refs itself
/* Schema: 
{"$ref": "http://json-schema.org/draft-04/schema#"}
*/
    Given the input JSON file "ref.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"minLength": 1}
        | #/007/tests/000/data | true  | remote ref valid                                                                 |
        # {"minLength": -1}
        | #/007/tests/001/data | false | remote ref invalid                                                               |

Scenario Outline: property named $ref that is not a reference
/* Schema: 
{
            "properties": {
                "$ref": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"$ref": "a"}
        | #/008/tests/000/data | true  | property named $ref valid                                                        |
        # {"$ref": 2}
        | #/008/tests/001/data | false | property named $ref invalid                                                      |

Scenario Outline: property named $ref, containing an actual $ref
/* Schema: 
{
            "properties": {
                "$ref": {"$ref": "#/definitions/is-string"}
            },
            "definitions": {
                "is-string": {
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"$ref": "a"}
        | #/009/tests/000/data | true  | property named $ref valid                                                        |
        # {"$ref": 2}
        | #/009/tests/001/data | false | property named $ref invalid                                                      |

Scenario Outline: Recursive references between schemas
/* Schema: 
{
            "id": "http://localhost:1234/tree",
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
            "definitions": {
                "node": {
                    "id": "http://localhost:1234/node",
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
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "meta": "root", "nodes": [ { "value": 1, "subtree": { "meta": "child", "nodes": [ {"value": 1.1}, {"value": 1.2} ] } }, { "value": 2, "subtree": { "meta": "child", "nodes": [ {"value": 2.1}, {"value": 2.2} ] } } ] }
        | #/010/tests/000/data | true  | valid tree                                                                       |
        # { "meta": "root", "nodes": [ { "value": 1, "subtree": { "meta": "child", "nodes": [ {"value": "string is invalid"}, {"value": 1.2} ] } }, { "value": 2, "subtree": { "meta": "child", "nodes": [ {"value": 2.1}, {"value": 2.2} ] } } ] }
        | #/010/tests/001/data | false | invalid tree                                                                     |

Scenario Outline: refs with quote
/* Schema: 
{
            "properties": {
                "foo\"bar": {"$ref": "#/definitions/foo%22bar"}
            },
            "definitions": {
                "foo\"bar": {"type": "number"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\"bar": 1 }
        | #/011/tests/000/data | true  | object with numbers is valid                                                     |
        # { "foo\"bar": "1" }
        | #/011/tests/001/data | false | object with strings is invalid                                                   |

Scenario Outline: Location-independent identifier
/* Schema: 
{
            "allOf": [{
                "$ref": "#foo"
            }],
            "definitions": {
                "A": {
                    "id": "#foo",
                    "type": "integer"
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/012/tests/000/data | true  | match                                                                            |
        # a
        | #/012/tests/001/data | false | mismatch                                                                         |

Scenario Outline: Location-independent identifier with base URI change in subschema
/* Schema: 
{
            "id": "http://localhost:1234/root",
            "allOf": [{
                "$ref": "http://localhost:1234/nested.json#foo"
            }],
            "definitions": {
                "A": {
                    "id": "nested.json",
                    "definitions": {
                        "B": {
                            "id": "#foo",
                            "type": "integer"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/013/tests/000/data | true  | match                                                                            |
        # a
        | #/013/tests/001/data | false | mismatch                                                                         |

Scenario Outline: naive replacement of $ref with its destination is not correct
/* Schema: 
{
            "definitions": {
                "a_string": { "type": "string" }
            },
            "enum": [
                { "$ref": "#/definitions/a_string" }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # this is a string
        | #/014/tests/000/data | false | do not evaluate the $ref inside the enum, matching any string                    |
        # { "$ref": "#/definitions/a_string" }
        | #/014/tests/001/data | true  | match the enum exactly                                                           |

Scenario Outline: id must be resolved against nearest parent, not just immediate parent
/* Schema: 
{
            "id": "http://example.com/a.json",
            "definitions": {
                "x": {
                    "id": "http://example.com/b/c.json",
                    "not": {
                        "definitions": {
                            "y": {
                                "id": "d.json",
                                "type": "number"
                            }
                        }
                    }
                }
            },
            "allOf": [
                {
                    "$ref": "http://example.com/b/d.json"
                }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/015/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/015/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: id with file URI still resolves pointers - *nix
/* Schema: 
{
            "id": "file:///folder/file.json",
            "definitions": {
                "foo": {
                    "type": "number"
                }
            },
            "allOf": [
                {
                    "$ref": "#/definitions/foo"
                }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/016/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/016/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: id with file URI still resolves pointers - windows
/* Schema: 
{
            "id": "file:///c:/folder/file.json",
            "definitions": {
                "foo": {
                    "type": "number"
                }
            },
            "allOf": [
                {
                    "$ref": "#/definitions/foo"
                }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/017/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/017/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: empty tokens in $ref json-pointer
/* Schema: 
{
            "definitions": {
                "": {
                    "definitions": {
                        "": { "type": "number" }
                    }
                } 
            },
            "allOf": [
                {
                    "$ref": "#/definitions//definitions/"
                }
            ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/018/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/018/tests/001/data | false | non-number is invalid                                                            |
