@draft7

Feature: id draft7
    In order to use json-schema
    As a developer
    I want to support id in draft7

Scenario Outline: id inside an enum is not a real identifier
/* Schema: 
{
            "definitions": {
                "id_in_enum": {
                    "enum": [
                        {
                          "$id": "https://localhost:1234/id/my_identifier.json",
                          "type": "null"
                        }
                    ]
                },
                "real_id_in_schema": {
                    "$id": "https://localhost:1234/id/my_identifier.json",
                    "type": "string"
                },
                "zzz_id_in_const": {
                    "const": {
                        "$id": "https://localhost:1234/id/my_identifier.json",
                        "type": "null"
                    }
                }
            },
            "anyOf": [
                { "$ref": "#/definitions/id_in_enum" },
                { "$ref": "https://localhost:1234/id/my_identifier.json" }
            ]
        }
*/
    Given the input JSON file "id.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | exact match to enum, and type matches                                            |
        | #/000/tests/001/data | true  | match $ref to id                                                                 |
        | #/000/tests/002/data | false | no match on enum or $ref to id                                                   |
