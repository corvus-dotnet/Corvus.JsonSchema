@draft2019-09

Feature: ref draft2019-09
    In order to use json-schema
    As a developer
    I want to support ref in draft2019-09

Scenario Outline: root pointer ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # {"bar": 3}
        | #/001/tests/000/data | true  | match                                                                            |
        # {"bar": true}
        | #/001/tests/001/data | false | mismatch                                                                         |

Scenario Outline: relative pointer ref to array
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # [1, 2]
        | #/002/tests/000/data | true  | match array                                                                      |
        # [1, "foo"]
        | #/002/tests/001/data | false | mismatch array                                                                   |

Scenario Outline: escaped pointer ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # 5
        | #/004/tests/000/data | true  | nested ref valid                                                                 |
        # a
        | #/004/tests/001/data | false | nested ref invalid                                                               |

Scenario Outline: ref applies alongside sibling keywords
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # { "foo": [] }
        | #/005/tests/000/data | true  | ref valid, maxItems valid                                                        |
        # { "foo": [1, 2, 3] }
        | #/005/tests/001/data | false | ref valid, maxItems invalid                                                      |
        # { "foo": "string" }
        | #/005/tests/002/data | false | ref invalid                                                                      |

Scenario Outline: remote ref, containing refs itself
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "https://json-schema.org/draft/2019-09/schema"
        }
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
        # {"minLength": 1}
        | #/006/tests/000/data | true  | remote ref valid                                                                 |
        # {"minLength": -1}
        | #/006/tests/001/data | false | remote ref invalid                                                               |

Scenario Outline: property named $ref that is not a reference
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # {"$ref": "a"}
        | #/007/tests/000/data | true  | property named $ref valid                                                        |
        # {"$ref": 2}
        | #/007/tests/001/data | false | property named $ref invalid                                                      |

Scenario Outline: property named $ref, containing an actual $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # {"$ref": "a"}
        | #/008/tests/000/data | true  | property named $ref valid                                                        |
        # {"$ref": 2}
        | #/008/tests/001/data | false | property named $ref invalid                                                      |

Scenario Outline: $ref to boolean schema true
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # foo
        | #/009/tests/000/data | true  | any value is valid                                                               |

Scenario Outline: $ref to boolean schema false
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # foo
        | #/010/tests/000/data | false | any value is invalid                                                             |

Scenario Outline: Recursive references between schemas
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/tree",
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
                    "$id": "http://localhost:1234/draft2019-09/node",
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
        # { "meta": "root", "nodes": [ { "value": 1, "subtree": { "meta": "child", "nodes": [ {"value": 1.1}, {"value": 1.2} ] } }, { "value": 2, "subtree": { "meta": "child", "nodes": [ {"value": 2.1}, {"value": 2.2} ] } } ] }
        | #/011/tests/000/data | true  | valid tree                                                                       |
        # { "meta": "root", "nodes": [ { "value": 1, "subtree": { "meta": "child", "nodes": [ {"value": "string is invalid"}, {"value": 1.2} ] } }, { "value": 2, "subtree": { "meta": "child", "nodes": [ {"value": 2.1}, {"value": 2.2} ] } } ] }
        | #/011/tests/001/data | false | invalid tree                                                                     |

Scenario Outline: refs with quote
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # { "foo\"bar": 1 }
        | #/012/tests/000/data | true  | object with numbers is valid                                                     |
        # { "foo\"bar": "1" }
        | #/012/tests/001/data | false | object with strings is invalid                                                   |

Scenario Outline: ref creates new scope when adjacent to keywords
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # { "prop1": "match" }
        | #/013/tests/000/data | false | referenced subschema doesn't see annotations from properties                     |

Scenario Outline: naive replacement of $ref with its destination is not correct
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # this is a string
        | #/014/tests/000/data | false | do not evaluate the $ref inside the enum, matching any string                    |
        # { "type": "string" }
        | #/014/tests/001/data | false | do not evaluate the $ref inside the enum, definition exact match                 |
        # { "$ref": "#/$defs/a_string" }
        | #/014/tests/002/data | true  | match the enum exactly                                                           |

Scenario Outline: refs with relative uris and defs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://example.com/schema-relative-uri-defs1.json",
            "properties": {
                "foo": {
                    "$id": "schema-relative-uri-defs2.json",
                    "$defs": {
                        "inner": {
                            "properties": {
                                "bar": { "type": "string" }
                            }
                        }
                    },
                    "$ref": "#/$defs/inner"
                }
            },
            "$ref": "schema-relative-uri-defs2.json"
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": { "bar": 1 }, "bar": "a" }
        | #/015/tests/000/data | false | invalid on inner field                                                           |
        # { "foo": { "bar": "a" }, "bar": 1 }
        | #/015/tests/001/data | false | invalid on outer field                                                           |
        # { "foo": { "bar": "a" }, "bar": "a" }
        | #/015/tests/002/data | true  | valid on both fields                                                             |

Scenario Outline: relative refs with absolute uris and defs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://example.com/schema-refs-absolute-uris-defs1.json",
            "properties": {
                "foo": {
                    "$id": "http://example.com/schema-refs-absolute-uris-defs2.json",
                    "$defs": {
                        "inner": {
                            "properties": {
                                "bar": { "type": "string" }
                            }
                        }
                    },
                    "$ref": "#/$defs/inner"
                }
            },
            "$ref": "schema-refs-absolute-uris-defs2.json"
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": { "bar": 1 }, "bar": "a" }
        | #/016/tests/000/data | false | invalid on inner field                                                           |
        # { "foo": { "bar": "a" }, "bar": 1 }
        | #/016/tests/001/data | false | invalid on outer field                                                           |
        # { "foo": { "bar": "a" }, "bar": "a" }
        | #/016/tests/002/data | true  | valid on both fields                                                             |

Scenario Outline: $id must be resolved against nearest parent, not just immediate parent
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://example.com/a.json",
            "$defs": {
                "x": {
                    "$id": "http://example.com/b/c.json",
                    "not": {
                        "$defs": {
                            "y": {
                                "$id": "d.json",
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
    And the schema at "#/17/schema"
    And the input data at "<inputDataReference>"
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

Scenario Outline: order of evaluation: $id and $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$comment": "$id must be evaluated before $ref to get the proper $ref destination",
            "$id": "https://example.com/draft2019-09/ref-and-id1/base.json",
            "$ref": "int.json",
            "$defs": {
                "bigint": {
                    "$comment": "canonical uri: https://example.com/draft2019-09/ref-and-id1/int.json",
                    "$id": "int.json",
                    "maximum": 10
                },
                "smallint": {
                    "$comment": "canonical uri: https://example.com/draft2019-09/ref-and-id1-int.json",
                    "$id": "/draft2019-09/ref-and-id1-int.json",
                    "maximum": 2
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 5
        | #/018/tests/000/data | true  | data is valid against first definition                                           |
        # 50
        | #/018/tests/001/data | false | data is invalid against first definition                                         |

Scenario Outline: order of evaluation: $id and $anchor and $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$comment": "$id must be evaluated before $ref to get the proper $ref destination",
            "$id": "https://example.com/draft2019-09/ref-and-id2/base.json",
            "$ref": "#bigint",
            "$defs": {
                "bigint": {
                    "$comment": "canonical uri: https://example.com/draft2019-09/ref-and-id2/base.json#/$defs/bigint; another valid uri for this location: https://example.com/ref-and-id2/base.json#bigint",
                    "$anchor": "bigint",
                    "maximum": 10
                },
                "smallint": {
                    "$comment": "canonical uri: https://example.com/draft2019-09/ref-and-id2#/$defs/smallint; another valid uri for this location: https://example.com/ref-and-id2/#bigint",
                    "$id": "/draft2019-09/ref-and-id2/",
                    "$anchor": "bigint",
                    "maximum": 2
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 5
        | #/019/tests/000/data | true  | data is valid against first definition                                           |
        # 50
        | #/019/tests/001/data | false | data is invalid against first definition                                         |

Scenario Outline: simple URN base URI with $ref via the URN
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$comment": "URIs do not have to have HTTP(s) schemes",
            "$id": "urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed",
            "properties": {
                "foo": {"$ref": "#/$defs/bar"}
            },
            "$defs": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/21/schema"
    And the input data at "<inputDataReference>"
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$comment": "RFC 8141 §2.2",
            "$id": "urn:example:1/406/47452/2",
            "properties": {
                "foo": {"$ref": "#/$defs/bar"}
            },
            "$defs": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/22/schema"
    And the input data at "<inputDataReference>"
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$comment": "RFC 8141 §2.3.1",
            "$id": "urn:example:foo-bar-baz-qux?+CCResolve:cc=uk",
            "properties": {
                "foo": {"$ref": "#/$defs/bar"}
            },
            "$defs": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/23/schema"
    And the input data at "<inputDataReference>"
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$comment": "RFC 8141 §2.3.2",
            "$id": "urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z",
            "properties": {
                "foo": {"$ref": "#/$defs/bar"}
            },
            "$defs": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/24/schema"
    And the input data at "<inputDataReference>"
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "urn:uuid:deadbeef-1234-0000-0000-4321feebdaed",
            "properties": {
                "foo": {"$ref": "urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/$defs/bar"}
            },
            "$defs": {
                "bar": {"type": "string"}
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/25/schema"
    And the input data at "<inputDataReference>"
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed",
            "properties": {
                "foo": {"$ref": "urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something"}
            },
            "$defs": {
                "bar": {
                    "$anchor": "something",
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/26/schema"
    And the input data at "<inputDataReference>"
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

Scenario Outline: URN ref with nested pointer ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed",
            "$defs": {
                "foo": {
                    "$id": "urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed",
                    "$defs": {"bar": {"type": "string"}},
                    "$ref": "#/$defs/bar"
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/27/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # bar
        | #/027/tests/000/data | true  | a string is valid                                                                |
        # 12
        | #/027/tests/001/data | false | a non-string is invalid                                                          |

Scenario Outline: ref to if
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://example.com/ref/if",
            "if": {
                "$id": "http://example.com/ref/if",
                "type": "integer"
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/28/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/028/tests/000/data | false | a non-integer is invalid due to the $ref                                         |
        # 12
        | #/028/tests/001/data | true  | an integer is valid                                                              |

Scenario Outline: ref to then
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://example.com/ref/then",
            "then": {
                "$id": "http://example.com/ref/then",
                "type": "integer"
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/29/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/029/tests/000/data | false | a non-integer is invalid due to the $ref                                         |
        # 12
        | #/029/tests/001/data | true  | an integer is valid                                                              |

Scenario Outline: ref to else
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://example.com/ref/else",
            "else": {
                "$id": "http://example.com/ref/else",
                "type": "integer"
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/30/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/030/tests/000/data | false | a non-integer is invalid due to the $ref                                         |
        # 12
        | #/030/tests/001/data | true  | an integer is valid                                                              |

Scenario Outline: ref with absolute-path-reference
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
             "$id": "http://example.com/ref/absref.json",
             "$defs": {
                 "a": {
                     "$id": "http://example.com/ref/absref/foobar.json",
                     "type": "number"
                 },
                 "b": {
                     "$id": "http://example.com/absref/foobar.json",
                     "type": "string"
                 }
             },
             "$ref": "/absref/foobar.json"
         }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/31/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/031/tests/000/data | true  | a string is valid                                                                |
        # 12
        | #/031/tests/001/data | false | an integer is invalid                                                            |

Scenario Outline: $id with file URI still resolves pointers - *nix
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
             "$id": "file:///folder/file.json",
             "$defs": {
                 "foo": {
                     "type": "number"
                 }
             },
             "$ref": "#/$defs/foo"
         }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/32/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/032/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/032/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: $id with file URI still resolves pointers - windows
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
             "$id": "file:///c:/folder/file.json",
             "$defs": {
                 "foo": {
                     "type": "number"
                 }
             },
             "$ref": "#/$defs/foo"
         }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/33/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/033/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/033/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: empty tokens in $ref json-pointer
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
             "$defs": {
                 "": {
                     "$defs": {
                         "": { "type": "number" }
                     }
                 } 
             },
             "allOf": [
                 {
                     "$ref": "#/$defs//$defs/"
                 }
             ]
         }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/34/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1
        | #/034/tests/000/data | true  | number is valid                                                                  |
        # a
        | #/034/tests/001/data | false | non-number is invalid                                                            |

Scenario Outline: $ref with $recursiveAnchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "https://example.com/schemas/unevaluated-items-are-disallowed",
            "$ref": "/schemas/unevaluated-items-are-allowed",
            "$recursiveAnchor": true,
            "unevaluatedItems": false,
            "$defs": {
                "/schemas/unevaluated-items-are-allowed": {
                    "$schema": "https://json-schema.org/draft/2019-09/schema",
                    "$id": "/schemas/unevaluated-items-are-allowed",
                    "$recursiveAnchor": true,
                    "type": "array",
                    "items": [
                        {
                            "type": "string"
                        },
                        {
                            "$ref": "#"
                        }
                    ]
                }
            }
        }
*/
    Given the input JSON file "ref.json"
    And the schema at "#/35/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo",["bar" , [] , 8]]
        | #/035/tests/000/data | true  | extra items allowed for inner arrays                                             |
        # ["foo",["bar" , [] , 8], 8]
        | #/035/tests/001/data | false | extra items disallowed for root                                                  |
