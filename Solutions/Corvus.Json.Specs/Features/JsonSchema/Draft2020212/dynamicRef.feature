@draft2020-12

Feature: dynamicRef draft2020-12
    In order to use json-schema
    As a developer
    I want to support dynamicRef in draft2020-12

Scenario Outline: A $dynamicRef to a $dynamicAnchor in the same schema resource should behave like a normal $ref to an $anchor
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamicRef-dynamicAnchor-same-schema/root",
            "type": "array",
            "items": { "$dynamicRef": "#items" },
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | An array of strings is valid                                                     |
        | #/000/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $dynamicRef to an $anchor in the same schema resource should behave like a normal $ref to an $anchor
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamicRef-anchor-same-schema/root",
            "type": "array",
            "items": { "$dynamicRef": "#items" },
            "$defs": {
                "foo": {
                    "$anchor": "items",
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | An array of strings is valid                                                     |
        | #/001/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $ref to a $dynamicAnchor in the same schema resource should behave like a normal $ref to an $anchor
/* Schema: 
{
            "$id": "https://test.json-schema.org/ref-dynamicAnchor-same-schema/root",
            "type": "array",
            "items": { "$ref": "#items" },
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | An array of strings is valid                                                     |
        | #/002/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $dynamicRef should resolve to the first $dynamicAnchor still in scope that is encountered when the schema is evaluated
/* Schema: 
{
            "$id": "https://test.json-schema.org/typical-dynamic-resolution/root",
            "$ref": "list",
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                },
                "list": {
                    "$id": "list",
                    "type": "array",
                    "items": { "$dynamicRef": "#items" },
                    "$defs": {
                      "items": {
                          "$comment": "This is only needed to satisfy the bookending requirement",
                          "$dynamicAnchor": "items"
                      }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | An array of strings is valid                                                     |
        | #/003/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $dynamicRef with intermediate scopes that don't include a matching $dynamicAnchor should not affect dynamic scope resolution
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamic-resolution-with-intermediate-scopes/root",
            "$ref": "intermediate-scope",
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                },
                "intermediate-scope": {
                    "$id": "intermediate-scope",
                    "$ref": "list"
                },
                "list": {
                    "$id": "list",
                    "type": "array",
                    "items": { "$dynamicRef": "#items" },
                    "$defs": {
                      "items": {
                          "$comment": "This is only needed to satisfy the bookending requirement",
                          "$dynamicAnchor": "items"
                      }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | An array of strings is valid                                                     |
        | #/004/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: An $anchor with the same name as a $dynamicAnchor should not be used for dynamic scope resolution
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamic-resolution-ignores-anchors/root",
            "$ref": "list",
            "$defs": {
                "foo": {
                    "$anchor": "items",
                    "type": "string"
                },
                "list": {
                    "$id": "list",
                    "type": "array",
                    "items": { "$dynamicRef": "#items" },
                    "$defs": {
                      "items": {
                          "$comment": "This is only needed to satisfy the bookending requirement",
                          "$dynamicAnchor": "items"
                      }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | Any array is valid                                                               |

Scenario Outline: A $dynamicRef without a matching $dynamicAnchor in the same schema resource should behave like a normal $ref to $anchor
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamic-resolution-without-bookend/root",
            "$ref": "list",
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                },
                "list": {
                    "$id": "list",
                    "type": "array",
                    "items": { "$dynamicRef": "#items" },
                    "$defs": {
                        "items": {
                            "$comment": "This is only needed to give the reference somewhere to resolve to when it behaves like $ref",
                            "$anchor": "items"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | true  | Any array is valid                                                               |

Scenario Outline: A $dynamicRef with a non-matching $dynamicAnchor in the same schema resource should behave like a normal $ref to $anchor
/* Schema: 
{
            "$id": "https://test.json-schema.org/unmatched-dynamic-anchor/root",
            "$ref": "list",
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                },
                "list": {
                    "$id": "list",
                    "type": "array",
                    "items": { "$dynamicRef": "#items" },
                    "$defs": {
                        "items": {
                            "$comment": "This is only needed to give the reference somewhere to resolve to when it behaves like $ref",
                            "$anchor": "items",
                            "$dynamicAnchor": "foo"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/007/tests/000/data | true  | Any array is valid                                                               |

Scenario Outline: A $dynamicRef that initially resolves to a schema with a matching $dynamicAnchor should resolve to the first $dynamicAnchor in the dynamic scope
/* Schema: 
{
            "$id": "https://test.json-schema.org/relative-dynamic-reference/root",
            "$dynamicAnchor": "meta",
            "type": "object",
            "properties": {
                "foo": { "const": "pass" }
            },
            "$ref": "extended",
            "$defs": {
                "extended": {
                    "$id": "extended",
                    "$dynamicAnchor": "meta",
                    "type": "object",
                    "properties": {
                        "bar": { "$ref": "bar" }
                    }
                },
                "bar": {
                    "$id": "bar",
                    "type": "object",
                    "properties": {
                        "baz": { "$dynamicRef": "extended#meta" }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/008/tests/000/data | true  | The recursive part is valid against the root                                     |
        | #/008/tests/001/data | false | The recursive part is not valid against the root                                 |

Scenario Outline: A $dynamicRef that initially resolves to a schema without a matching $dynamicAnchor should behave like a normal $ref to $anchor
/* Schema: 
{
            "$id": "https://test.json-schema.org/relative-dynamic-reference-without-bookend/root",
            "$dynamicAnchor": "meta",
            "type": "object",
            "properties": {
                "foo": { "const": "pass" }
            },
            "$ref": "extended",
            "$defs": {
                "extended": {
                    "$id": "extended",
                    "$anchor": "meta",
                    "type": "object",
                    "properties": {
                        "bar": { "$ref": "bar" }
                    }
                },
                "bar": {
                    "$id": "bar",
                    "type": "object",
                    "properties": {
                        "baz": { "$dynamicRef": "extended#meta" }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/009/tests/000/data | true  | The recursive part doesn't need to validate against the root                     |

Scenario Outline: multiple dynamic paths to the $dynamicRef keyword
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamic-ref-with-multiple-paths/main",
            "$defs": {
                "inner": {
                    "$id": "inner",
                    "$dynamicAnchor": "foo",
                    "title": "inner",
                    "additionalProperties": {
                        "$dynamicRef": "#foo"
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
                "$id": "anyLeafNode",
                "$dynamicAnchor": "foo",
                "$ref": "inner"
            },
            "else": {
                "title": "integer node",
                "$id": "integerNode",
                "$dynamicAnchor": "foo",
                "type": [ "object", "integer" ],
                "$ref": "inner"
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/010/tests/000/data | true  | recurse to anyLeafNode - floats are allowed                                      |
        | #/010/tests/001/data | false | recurse to integerNode - floats are not allowed                                  |

Scenario Outline: after leaving a dynamic scope, it should not be used by a $dynamicRef
/* Schema: 
{
            "$id": "https://test.json-schema.org/dynamic-ref-leaving-dynamic-scope/main",
            "if": {
                "$id": "first_scope",
                "$defs": {
                    "thingy": {
                        "$comment": "this is first_scope#thingy",
                        "$dynamicAnchor": "thingy",
                        "type": "number"
                    }
                }
            },
            "then": {
                "$id": "second_scope",
                "$ref": "start",
                "$defs": {
                    "thingy": {
                        "$comment": "this is second_scope#thingy, the final destination of the $dynamicRef",
                        "$dynamicAnchor": "thingy",
                        "type": "null"
                    }
                }
            },
            "$defs": {
                "start": {
                    "$comment": "this is the landing spot from $ref",
                    "$id": "start",
                    "$dynamicRef": "inner_scope#thingy"
                },
                "thingy": {
                    "$comment": "this is the first stop for the $dynamicRef",
                    "$id": "inner_scope",
                    "$dynamicAnchor": "thingy",
                    "type": "string"
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/11/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/011/tests/000/data | false | string matches /$defs/thingy, but the $dynamicRef does not stop here             |
        | #/011/tests/001/data | false | first_scope is not in dynamic scope for the $dynamicRef                          |
        | #/011/tests/002/data | true  | /then/$defs/thingy is the final stop for the $dynamicRef                         |

Scenario Outline: strict-tree schema, guards against misspelled properties
/* Schema: 
{
            "$id": "http://localhost:1234/strict-tree.json",
            "$dynamicAnchor": "node",

            "$ref": "tree.json",
            "unevaluatedProperties": false
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/012/tests/000/data | false | instance with misspelled field                                                   |
        | #/012/tests/001/data | true  | instance with correct field                                                      |

Scenario Outline: tests for implementation dynamic anchor and reference link
/* Schema: 
{
            "$id": "http://localhost:1234/strict-extendible.json",
            "$ref": "extendible-dynamic-ref.json",
            "$defs": {
                "elements": {
                    "$dynamicAnchor": "elements",
                    "properties": {
                        "a": true
                    },
                    "required": ["a"],
                    "additionalProperties": false
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/13/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/013/tests/000/data | false | incorrect parent schema                                                          |
        | #/013/tests/001/data | false | incorrect extended schema                                                        |
        | #/013/tests/002/data | true  | correct extended schema                                                          |

Scenario Outline: Tests for implementation dynamic anchor and reference link. Reference should be independent of any possible ordering.
/* Schema: 
{
            "$id": "http://localhost:1234/strict-extendible-allof-defs-first.json",
            "allOf": [
                {
                    "$ref": "extendible-dynamic-ref.json"
                },
                {
                    "$defs": {
                        "elements": {
                            "$dynamicAnchor": "elements",
                            "properties": {
                                "a": true
                            },
                            "required": ["a"],
                            "additionalProperties": false
                        }
                    }
                }
            ]
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/014/tests/000/data | false | incorrect parent schema                                                          |
        | #/014/tests/001/data | false | incorrect extended schema                                                        |
        | #/014/tests/002/data | true  | correct extended schema                                                          |

Scenario Outline: Tests for implementation dynamic anchor and reference link. Reference should be independent of any possible ordering 2.
/* Schema: 
{
            "$id": "http://localhost:1234/strict-extendible-allof-ref-first.json",
            "allOf": [
                {
                    "$defs": {
                        "elements": {
                            "$dynamicAnchor": "elements",
                            "properties": {
                                "a": true
                            },
                            "required": ["a"],
                            "additionalProperties": false
                        }
                    }
                },
                {
                    "$ref": "extendible-dynamic-ref.json"
                }
            ]
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/015/tests/000/data | false | incorrect parent schema                                                          |
        | #/015/tests/001/data | false | incorrect extended schema                                                        |
        | #/015/tests/002/data | true  | correct extended schema                                                          |
