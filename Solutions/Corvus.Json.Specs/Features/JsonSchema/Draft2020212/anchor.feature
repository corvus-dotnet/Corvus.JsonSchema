@draft2020-12

Feature: anchor draft2020-12
    In order to use json-schema
    As a developer
    I want to support anchor in draft2020-12

Scenario Outline: Location-independent identifier
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "#foo",
            "$defs": {
                "A": {
                    "$anchor": "foo",
                    "type": "integer"
                }
            }
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | match                                                                            |
        | #/000/tests/001/data | false | mismatch                                                                         |

Scenario Outline: Location-independent identifier with absolute URI
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "http://localhost:1234/draft2020-12/bar#foo",
            "$defs": {
                "A": {
                    "$id": "http://localhost:1234/draft2020-12/bar",
                    "$anchor": "foo",
                    "type": "integer"
                }
            }
        }
*/
    Given the input JSON file "anchor.json"
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

Scenario Outline: Location-independent identifier with base URI change in subschema
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "http://localhost:1234/draft2020-12/root",
            "$ref": "http://localhost:1234/draft2020-12/nested.json#foo",
            "$defs": {
                "A": {
                    "$id": "nested.json",
                    "$defs": {
                        "B": {
                            "$anchor": "foo",
                            "type": "integer"
                        }
                    }
                }
            }
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/2/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/002/tests/000/data | true  | match                                                                            |
        | #/002/tests/001/data | false | mismatch                                                                         |

Scenario Outline: $anchor inside an enum is not a real identifier
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$defs": {
                "anchor_in_enum": {
                    "enum": [
                        {
                            "$anchor": "my_anchor",
                            "type": "null"
                        }
                    ]
                },
                "real_identifier_in_schema": {
                    "$anchor": "my_anchor",
                    "type": "string"
                },
                "zzz_anchor_in_const": {
                    "const": {
                        "$anchor": "my_anchor",
                        "type": "null"
                    }
                }
            },
            "anyOf": [
                { "$ref": "#/$defs/anchor_in_enum" },
                { "$ref": "#my_anchor" }
            ]
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/003/tests/000/data | true  | exact match to enum, and type matches                                            |
        | #/003/tests/001/data | false | in implementations that strip $anchor, this may match either $def                |
        | #/003/tests/002/data | true  | match $ref to $anchor                                                            |
        | #/003/tests/003/data | false | no match on enum or $ref to $anchor                                              |

Scenario Outline: same $anchor with different base uri
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$id": "http://localhost:1234/draft2020-12/foobar",
            "$defs": {
                "A": {
                    "$id": "child1",
                    "allOf": [
                        {
                            "$id": "child2",
                            "$anchor": "my_anchor",
                            "type": "number"
                        },
                        {
                            "$anchor": "my_anchor",
                            "type": "string"
                        }
                    ]
                }
            },
            "$ref": "child1#my_anchor"
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/4/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/004/tests/000/data | true  | $ref resolves to /$defs/A/allOf/1                                                |
        | #/004/tests/001/data | false | $ref does not resolve to /$defs/A/allOf/0                                        |

Scenario Outline: non-schema object containing an $anchor property
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$defs": {
                "const_not_anchor": {
                    "const": {
                        "$anchor": "not_a_real_anchor"
                    }
                }
            },
            "if": {
                "const": "skip not_a_real_anchor"
            },
            "then": true,
            "else" : {
                "$ref": "#/$defs/const_not_anchor"
            }
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/5/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/005/tests/000/data | true  | skip traversing definition for a valid result                                    |
        | #/005/tests/001/data | false | const at const_not_anchor does not match                                         |

Scenario Outline: invalid anchors
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "https://json-schema.org/draft/2020-12/schema"
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/6/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/006/tests/000/data | false | MUST start with a letter (and not #)                                             |
        | #/006/tests/001/data | false | JSON pointers are not valid                                                      |
        | #/006/tests/002/data | false | invalid with valid beginning                                                     |
