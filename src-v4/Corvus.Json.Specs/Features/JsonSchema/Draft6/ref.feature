@draft6

Feature: ref draft6
    In order to use json-schema
    As a developer
    I want to support ref in draft6

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

Scenario Outline: $ref prevents a sibling $id from changing the base uri
/* Schema: 
{
            "$id": "http://localhost:1234/sibling_id/base/",
            "definitions": {
                "foo": {
                    "$id": "http://localhost:1234/sibling_id/foo.json",
                    "type": "string"
                },
                "base_foo": {
                    "$comment": "this canonical uri is http://localhost:1234/sibling_id/base/foo.json",
                    "$id": "foo.json",
                    "type": "number"
                }
            },
            "allOf": [
                {
                    "$comment": "$ref resolves to http://localhost:1234/sibling_id/base/foo.json, not http://localhost:1234/sibling_id/foo.json",
                    "$id": "http://localhost:1234/sibling_id/",
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
{"$ref": "http://json-schema.org/draft-06/schema#"}
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

Scenario Outline: $ref to boolean schema true
/* Schema: 
{
            "allOf": [{ "$ref": "#/definitions/bool" }],
            "definitions": {
                "bool": true
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
        # foo
        | #/010/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: $ref to boolean schema false
/* Schema: 
{
            "allOf": [{ "$ref": "#/definitions/bool" }],
            "definitions": {
                "bool": false
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
        # foo
        | #/011/tests/000/data | false | any value is invalid                                                             |

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
            "definitions": {
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
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "meta": "root", "nodes": [ { "value": 1, "subtree": { "meta": "child", "nodes": [ {"value": 1.1}, {"value": 1.2} ] } }, { "value": 2, "subtree": { "meta": "child", "nodes": [ {"value": 2.1}, {"value": 2.2} ] } } ] }
        | #/012/tests/000/data | true  | valid tree                                                                       |
        # { "meta": "root", "nodes": [ { "value": 1, "subtree": { "meta": "child", "nodes": [ {"value": "string is invalid"}, {"value": 1.2} ] } }, { "value": 2, "subtree": { "meta": "child", "nodes": [ {"value": 2.1}, {"value": 2.2} ] } } ] }
        | #/012/tests/001/data | false | invalid tree                                                                     |

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
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo\"bar": 1 }
        | #/013/tests/000/data | true  | object with numbers is valid                                                     |
        # { "foo\"bar": "1" }
        | #/013/tests/001/data | false | object with strings is invalid                                                   |

Scenario Outline: Location-independent identifier
/* Schema: 
{
            "allOf": [{
                "$ref": "#foo"
            }],
            "definitions": {
                "A": {
                    "$id": "#foo",
                    "type": "integer"
                }
            }
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
        # 1
        | #/014/tests/000/data | true  | match                                                                            |
        # a
        | #/014/tests/001/data | false | mismatch                                                                         |

Scenario Outline: Reference an anchor with a non-relative URI
/* Schema: 
{
            "$id": "https://example.com/schema-with-anchor",
            "allOf": [{
                "$ref": "https://example.com/schema-with-anchor#foo"
            }],
            "definitions": {
                "A": {
                    "$id": "#foo",
                    "type": "integer"
                }
            }
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
        | #/015/tests/000/data | true  | match                                                                            |
        # a
        | #/015/tests/001/data | false | mismatch                                                                         |

Scenario Outline: Location-independent identifier with base URI change in subschema
/* Schema: 
{
            "$id": "http://localhost:1234/root",
            "allOf": [{
                "$ref": "http://localhost:1234/nested.json#foo"
            }],
            "definitions": {
                "A": {
                    "$id": "nested.json",
                    "definitions": {
                        "B": {
                            "$id": "#foo",
                            "type": "integer"
                        }
                    }
                }
            }
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
        | #/016/tests/000/data | true  | match                                                                            |
        # a
        | #/016/tests/001/data | false | mismatch                                                                         |

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
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # this is a string
        | #/017/tests/000/data | false | do not evaluate the $ref inside the enum, matching any string                    |
        # { "type": "string" }
        | #/017/tests/001/data | false | do not evaluate the $ref inside the enum, definition exact match                 |
        # { "$ref": "#/definitions/a_string" }
        | #/017/tests/002/data | true  | match the enum exactly                                                           |

Scenario Outline: refs with relative uris and defs
/* Schema: 
{
            "$id": "http://example.com/schema-relative-uri-defs1.json",
            "properties": {
                "foo": {
                    "$id": "schema-relative-uri-defs2.json",
                    "definitions": {
                        "inner": {
                            "properties": {
                                "bar": { "type": "string" }
                            }
                        }
                    },
                    "allOf": [ { "$ref": "#/definitions/inner" } ]
                }
            },
            "allOf": [ { "$ref": "schema-relative-uri-defs2.json" } ]
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
        # { "foo": { "bar": 1 }, "bar": "a" }
        | #/018/tests/000/data | false | invalid on inner field                                                           |
        # { "foo": { "bar": "a" }, "bar": 1 }
        | #/018/tests/001/data | false | invalid on outer field                                                           |
        # { "foo": { "bar": "a" }, "bar": "a" }
        | #/018/tests/002/data | true  | valid on both fields                                                             |

Scenario Outline: relative refs with absolute uris and defs
/* Schema: 
{
            "$id": "http://example.com/schema-refs-absolute-uris-defs1.json",
            "properties": {
                "foo": {
                    "$id": "http://example.com/schema-refs-absolute-uris-defs2.json",
                    "definitions": {
                        "inner": {
                            "properties": {
                                "bar": { "type": "string" }
                            }
                        }
                    },
                    "allOf": [ { "$ref": "#/definitions/inner" } ]
                }
            },
            "allOf": [ { "$ref": "schema-refs-absolute-uris-defs2.json" } ]
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": { "bar": 1 }, "bar": "a" }
        | #/019/tests/000/data | false | invalid on inner field                                                           |
        # { "foo": { "bar": "a" }, "bar": 1 }
        | #/019/tests/001/data | false | invalid on outer field                                                           |
        # { "foo": { "bar": "a" }, "bar": "a" }
        | #/019/tests/002/data | true  | valid on both fields                                                             |

Scenario Outline: simple URN base URI with $ref via the URN
/* Schema: 
{
            "$comment": "URIs do not have to have HTTP(s) schemes",
            "$id": "urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed",
            "minimum": 30,
            "properties": {
                "foo": {"$ref": "urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/20/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": 37}
        | #/020/tests/000/data | true  | valid under the URN IDed schema                                                  |
        # {"foo": 12}
        | #/020/tests/001/data | false | invalid under the URN IDed schema                                                |

Scenario Outline: simple URN base URI with JSON pointer
/* Schema: 
{
            "$comment": "URIs do not have to have HTTP(s) schemes",
            "$id": "urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed",
            "properties": {
                "foo": {"$ref": "#/definitions/bar"}
            },
            "definitions": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/21/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "bar"}
        | #/021/tests/000/data | true  | a string is valid                                                                |
        # {"foo": 12}
        | #/021/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: URN base URI with NSS
/* Schema: 
{
            "$comment": "RFC 8141 §2.2",
            "$id": "urn:example:1/406/47452/2",
            "properties": {
                "foo": {"$ref": "#/definitions/bar"}
            },
            "definitions": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/22/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "bar"}
        | #/022/tests/000/data | true  | a string is valid                                                                |
        # {"foo": 12}
        | #/022/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: URN base URI with r-component
/* Schema: 
{
            "$comment": "RFC 8141 §2.3.1",
            "$id": "urn:example:foo-bar-baz-qux?+CCResolve:cc=uk",
            "properties": {
                "foo": {"$ref": "#/definitions/bar"}
            },
            "definitions": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/23/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "bar"}
        | #/023/tests/000/data | true  | a string is valid                                                                |
        # {"foo": 12}
        | #/023/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: URN base URI with q-component
/* Schema: 
{
            "$comment": "RFC 8141 §2.3.2",
            "$id": "urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z",
            "properties": {
                "foo": {"$ref": "#/definitions/bar"}
            },
            "definitions": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/24/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "bar"}
        | #/024/tests/000/data | true  | a string is valid                                                                |
        # {"foo": 12}
        | #/024/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: URN base URI with URN and JSON pointer ref
/* Schema: 
{
            "$id": "urn:uuid:deadbeef-1234-0000-0000-4321feebdaed",
            "properties": {
                "foo": {"$ref": "urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/definitions/bar"}
            },
            "definitions": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/25/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "bar"}
        | #/025/tests/000/data | true  | a string is valid                                                                |
        # {"foo": 12}
        | #/025/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: URN base URI with URN and anchor ref
/* Schema: 
{
            "$id": "urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed",
            "properties": {
                "foo": {"$ref": "urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something"}
            },
            "definitions": {
                "bar": {
                    "$id": "#something",
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/26/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "bar"}
        | #/026/tests/000/data | true  | a string is valid                                                                |
        # {"foo": 12}
        | #/026/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: ref with absolute-path-reference
/* Schema: 
{
             "$id": "http://example.com/ref/absref.json",
             "definitions": {
                 "a": {
                     "$id": "http://example.com/ref/absref/foobar.json",
                     "type": "number"
                  },
                  "b": {
                      "$id": "http://example.com/absref/foobar.json",
                      "type": "string"
                  }
             },
             "allOf": [
                 { "$ref": "/absref/foobar.json" }
             ]
         }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/27/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/027/tests/000/data | true  | a string is valid                                                                |
        # 12
        | #/027/tests/001/data | false | an integer is invalid                                                            |

Scenario Outline: $id with file URI still resolves pointers - *nix
/* Schema: 
{
             "$id": "file:///folder/file.json",
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
    And the schema at "#/28/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/028/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/028/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: $id with file URI still resolves pointers - windows
/* Schema: 
{
             "$id": "file:///c:/folder/file.json",
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
    And the schema at "#/29/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/029/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/029/tests/001/data | false | non-number is invalid                                                            |

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
    And the schema at "#/30/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/030/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/030/tests/001/data | false | non-number is invalid                                                            |
