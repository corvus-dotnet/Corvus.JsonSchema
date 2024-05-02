@draft2019-09

Feature: id draft2019-09
    In order to use json-schema
    As a developer
    I want to support id in draft2019-09

Scenario Outline: $id inside an enum is not a real identifier
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$defs": {
                "id_in_enum": {
                    "enum": [
                        {
                          "$id": "https://localhost:1234/draft2019-09/id/my_identifier.json",
                          "type": "null"
                        }
                    ]
                },
                "real_id_in_schema": {
                    "$id": "https://localhost:1234/draft2019-09/id/my_identifier.json",
                    "type": "string"
                },
                "zzz_id_in_const": {
                    "const": {
                        "$id": "https://localhost:1234/draft2019-09/id/my_identifier.json",
                        "type": "null"
                    }
                }
            },
            "anyOf": [
                { "$ref": "#/$defs/id_in_enum" },
                { "$ref": "https://localhost:1234/draft2019-09/id/my_identifier.json" }
            ]
        }
*/
    Given the input JSON file "optional/id.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "$id": "https://localhost:1234/draft2019-09/id/my_identifier.json", "type": "null" }
        | #/000/tests/000/data | true  | exact match to enum, and type matches                                            |
        # a string to match #/$defs/id_in_enum
        | #/000/tests/001/data | true  | match $ref to $id                                                                |
        # 1
        | #/000/tests/002/data | false | no match on enum or $ref to $id                                                  |
