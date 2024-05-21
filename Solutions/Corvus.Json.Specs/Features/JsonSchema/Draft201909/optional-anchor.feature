@draft2019-09

Feature: optional-anchor draft2019-09
    In order to use json-schema
    As a developer
    I want to support optional-anchor in draft2019-09

Scenario Outline: $anchor inside an enum is not a real identifier
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
    Given the input JSON file "optional/anchor.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "$anchor": "my_anchor", "type": "null" }
        | #/000/tests/000/data | true  | exact match to enum, and type matches                                            |
        # { "type": "null" }
        | #/000/tests/001/data | false | in implementations that strip $anchor, this may match either $def                |
        # a string to match #/$defs/anchor_in_enum
        | #/000/tests/002/data | true  | match $ref to $anchor                                                            |
        # 1
        | #/000/tests/003/data | false | no match on enum or $ref to $anchor                                              |
