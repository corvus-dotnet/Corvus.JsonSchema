@draft2019-09

Feature: anchor draft2019-09
    In order to use json-schema
    As a developer
    I want to support anchor in draft2019-09

Scenario Outline: Location-independent identifier
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$ref": "http://localhost:1234/draft2019-09/bar#foo",
            "$defs": {
                "A": {
                    "$id": "http://localhost:1234/draft2019-09/bar",
                    "$anchor": "foo",
                    "type": "integer"
                }
            }
        }
*/
    Given the input JSON file "anchor.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/root",
            "$ref": "http://localhost:1234/draft2019-09/nested.json#foo",
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
    And I assert format
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
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$id": "http://localhost:1234/draft2019-09/foobar",
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
    And I assert format
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
