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
        # 1
        | #/000/tests/000/data | true  | match                                                                            |
        # a
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
        # 1
        | #/001/tests/000/data | true  | match                                                                            |
        # a
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
        # 1
        | #/002/tests/000/data | true  | match                                                                            |
        # a
        | #/002/tests/001/data | false | mismatch                                                                         |

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
    And the schema at "#/3/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # a
        | #/003/tests/000/data | true  | $ref resolves to /$defs/A/allOf/1                                                |
        # 1
        | #/003/tests/001/data | false | $ref does not resolve to /$defs/A/allOf/0                                        |
