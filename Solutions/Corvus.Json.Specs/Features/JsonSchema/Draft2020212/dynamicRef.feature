@draft2020-12

Feature: dynamicRef draft2020-12
    In order to use json-schema
    As a developer
    I want to support dynamicRef in draft2020-12

Scenario Outline: A $dynamicRef to a $dynamicAnchor in the same schema resource behaves like a normal $ref to an $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # ["foo", "bar"]
        | #/000/tests/000/data | true  | An array of strings is valid                                                     |
        # ["foo", 42]
        | #/000/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $dynamicRef to an $anchor in the same schema resource behaves like a normal $ref to an $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # ["foo", "bar"]
        | #/001/tests/000/data | true  | An array of strings is valid                                                     |
        # ["foo", 42]
        | #/001/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $ref to a $dynamicAnchor in the same schema resource behaves like a normal $ref to an $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # ["foo", "bar"]
        | #/002/tests/000/data | true  | An array of strings is valid                                                     |
        # ["foo", 42]
        | #/002/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $dynamicRef resolves to the first $dynamicAnchor still in scope that is encountered when the schema is evaluated
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
        # ["foo", "bar"]
        | #/003/tests/000/data | true  | An array of strings is valid                                                     |
        # ["foo", 42]
        | #/003/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: A $dynamicRef without anchor in fragment behaves identical to $ref
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "https://test.json-schema.org/dynamicRef-without-anchor/root",
            "$ref": "list",
            "$defs": {
                "foo": {
                    "$dynamicAnchor": "items",
                    "type": "string"
                },
                "list": {
                    "$id": "list",
                    "type": "array",
                    "items": { "$dynamicRef": "#/$defs/items" },
                    "$defs": {
                      "items": {
                          "$comment": "This is only needed to satisfy the bookending requirement",
                          "$dynamicAnchor": "items",
                          "type": "number"
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
        # ["foo", "bar"]
        | #/004/tests/000/data | false | An array of strings is invalid                                                   |
        # [24, 42]
        | #/004/tests/001/data | true  | An array of numbers is valid                                                     |

Scenario Outline: A $dynamicRef with intermediate scopes that don't include a matching $dynamicAnchor does not affect dynamic scope resolution
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", "bar"]
        | #/005/tests/000/data | true  | An array of strings is valid                                                     |
        # ["foo", 42]
        | #/005/tests/001/data | false | An array containing non-strings is invalid                                       |

Scenario Outline: An $anchor with the same name as a $dynamicAnchor is not used for dynamic scope resolution
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 42]
        | #/006/tests/000/data | true  | Any array is valid                                                               |

Scenario Outline: A $dynamicRef without a matching $dynamicAnchor in the same schema resource behaves like a normal $ref to $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/7/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 42]
        | #/007/tests/000/data | true  | Any array is valid                                                               |

Scenario Outline: A $dynamicRef with a non-matching $dynamicAnchor in the same schema resource behaves like a normal $ref to $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/8/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # ["foo", 42]
        | #/008/tests/000/data | true  | Any array is valid                                                               |

Scenario Outline: A $dynamicRef that initially resolves to a schema with a matching $dynamicAnchor resolves to the first $dynamicAnchor in the dynamic scope
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/9/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "pass", "bar": { "baz": { "foo": "pass" } } }
        | #/009/tests/000/data | true  | The recursive part is valid against the root                                     |
        # { "foo": "pass", "bar": { "baz": { "foo": "fail" } } }
        | #/009/tests/001/data | false | The recursive part is not valid against the root                                 |

Scenario Outline: A $dynamicRef that initially resolves to a schema without a matching $dynamicAnchor behaves like a normal $ref to $anchor
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/10/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "foo": "pass", "bar": { "baz": { "foo": "fail" } } }
        | #/010/tests/000/data | true  | The recursive part doesn't need to validate against the root                     |

Scenario Outline: multiple dynamic paths to the $dynamicRef keyword
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "https://test.json-schema.org/dynamic-ref-with-multiple-paths/main",
            "if": {
                "properties": {
                    "kindOfList": { "const": "numbers" }
                },
                "required": ["kindOfList"]
            },
            "then": { "$ref": "numberList" },
            "else": { "$ref": "stringList" },

            "$defs": {
                "genericList": {
                    "$id": "genericList",
                    "properties": {
                        "list": {
                            "items": { "$dynamicRef": "#itemType" }
                        }
                    },
                    "$defs": {
                        "defaultItemType": {
                            "$comment": "Only needed to satisfy bookending requirement",
                            "$dynamicAnchor": "itemType"
                        }
                    }
                },
                "numberList": {
                    "$id": "numberList",
                    "$defs": {
                        "itemType": {
                            "$dynamicAnchor": "itemType",
                            "type": "number"
                        }
                    },
                    "$ref": "genericList"
                },
                "stringList": {
                    "$id": "stringList",
                    "$defs": {
                        "itemType": {
                            "$dynamicAnchor": "itemType",
                            "type": "string"
                        }
                    },
                    "$ref": "genericList"
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
        # { "kindOfList": "numbers", "list": [1.1] }
        | #/011/tests/000/data | true  | number list with number values                                                   |
        # { "kindOfList": "numbers", "list": ["foo"] }
        | #/011/tests/001/data | false | number list with string values                                                   |
        # { "kindOfList": "strings", "list": [1.1] }
        | #/011/tests/002/data | false | string list with number values                                                   |
        # { "kindOfList": "strings", "list": ["foo"] }
        | #/011/tests/003/data | true  | string list with string values                                                   |

Scenario Outline: after leaving a dynamic scope, it is not used by a $dynamicRef
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
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
    And the schema at "#/12/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # a string
        | #/012/tests/000/data | false | string matches /$defs/thingy, but the $dynamicRef does not stop here             |
        # 42
        | #/012/tests/001/data | false | first_scope is not in dynamic scope for the $dynamicRef                          |
        # 
        | #/012/tests/002/data | true  | /then/$defs/thingy is the final stop for the $dynamicRef                         |

Scenario Outline: strict-tree schema, guards against misspelled properties
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "http://localhost:1234/draft2020-12/strict-tree.json",
            "$dynamicAnchor": "node",

            "$ref": "tree.json",
            "unevaluatedProperties": false
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
        # { "children": [{ "daat": 1 }] }
        | #/013/tests/000/data | false | instance with misspelled field                                                   |
        # { "children": [{ "data": 1 }] }
        | #/013/tests/001/data | true  | instance with correct field                                                      |

Scenario Outline: tests for implementation dynamic anchor and reference link
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "http://localhost:1234/draft2020-12/strict-extendible.json",
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
    And the schema at "#/14/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "a": true }
        | #/014/tests/000/data | false | incorrect parent schema                                                          |
        # { "elements": [ { "b": 1 } ] }
        | #/014/tests/001/data | false | incorrect extended schema                                                        |
        # { "elements": [ { "a": 1 } ] }
        | #/014/tests/002/data | true  | correct extended schema                                                          |

Scenario Outline: $ref and $dynamicAnchor are independent of order - $defs first
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "http://localhost:1234/draft2020-12/strict-extendible-allof-defs-first.json",
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
    And the schema at "#/15/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "a": true }
        | #/015/tests/000/data | false | incorrect parent schema                                                          |
        # { "elements": [ { "b": 1 } ] }
        | #/015/tests/001/data | false | incorrect extended schema                                                        |
        # { "elements": [ { "a": 1 } ] }
        | #/015/tests/002/data | true  | correct extended schema                                                          |

Scenario Outline: $ref and $dynamicAnchor are independent of order - $ref first
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "http://localhost:1234/draft2020-12/strict-extendible-allof-ref-first.json",
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
    And the schema at "#/16/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "a": true }
        | #/016/tests/000/data | false | incorrect parent schema                                                          |
        # { "elements": [ { "b": 1 } ] }
        | #/016/tests/001/data | false | incorrect extended schema                                                        |
        # { "elements": [ { "a": 1 } ] }
        | #/016/tests/002/data | true  | correct extended schema                                                          |

Scenario Outline: $ref to $dynamicRef finds detached $dynamicAnchor
/* Schema: 
{
            "$ref": "http://localhost:1234/draft2020-12/detached-dynamicref.json#/$defs/foo"
        }
*/
    Given the input JSON file "dynamicRef.json"
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

Scenario Outline: $dynamicRef points to a boolean schema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$defs": {
                "true": true,
                "false": false
            },
            "properties": {
                "true": {
                    "$dynamicRef": "#/$defs/true"
                },
                "false": {
                    "$dynamicRef": "#/$defs/false"
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/18/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "true": 1 }
        | #/018/tests/000/data | true  | follow $dynamicRef to a true schema                                              |
        # { "false": 1 }
        | #/018/tests/001/data | false | follow $dynamicRef to a false schema                                             |

Scenario Outline: $dynamicRef skips over intermediate resources - direct reference
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "https://test.json-schema.org/dynamic-ref-skips-intermediate-resource/main",
            "type": "object",
            "properties": {
                "bar-item": {
                    "$ref": "item"
                }
            },
            "$defs": {
                "bar": {
                    "$id": "bar",
                    "type": "array",
                    "items": {
                        "$ref": "item"
                    },
                    "$defs": {
                        "item": {
                            "$id": "item",
                            "type": "object",
                            "properties": {
                                "content": {
                                    "$dynamicRef": "#content"
                                }
                            },
                            "$defs": {
                                "defaultContent": {
                                    "$dynamicAnchor": "content",
                                    "type": "integer"
                                }
                            }
                        },
                        "content": {
                            "$dynamicAnchor": "content",
                            "type": "string"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "dynamicRef.json"
    And the schema at "#/19/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "bar-item": { "content": 42 } }
        | #/019/tests/000/data | true  | integer property passes                                                          |
        # { "bar-item": { "content": "value" } }
        | #/019/tests/001/data | false | string property fails                                                            |
