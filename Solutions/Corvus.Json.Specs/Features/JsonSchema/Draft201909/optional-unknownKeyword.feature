@draft2019-09

Feature: optional-unknownKeyword draft2019-09
    In order to use json-schema
    As a developer
    I want to support optional-unknownKeyword in draft2019-09

Scenario Outline: $id inside an unknown keyword is not a real identifier
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "$defs": {
                "id_in_unknown0": {
                    "not": {
                        "array_of_schemas": [
                            {
                              "$id": "https://localhost:1234/draft2019-09/unknownKeyword/my_identifier.json",
                              "type": "null"
                            }
                        ]
                    }
                },
                "real_id_in_schema": {
                    "$id": "https://localhost:1234/draft2019-09/unknownKeyword/my_identifier.json",
                    "type": "string"
                },
                "id_in_unknown1": {
                    "not": {
                        "object_of_schemas": {
                            "foo": {
                              "$id": "https://localhost:1234/draft2019-09/unknownKeyword/my_identifier.json",
                              "type": "integer"
                            }
                        }
                    }
                }
            },
            "anyOf": [
                { "$ref": "#/$defs/id_in_unknown0" },
                { "$ref": "#/$defs/id_in_unknown1" },
                { "$ref": "https://localhost:1234/draft2019-09/unknownKeyword/my_identifier.json" }
            ]
        }
*/
    Given the input JSON file "optional/unknownKeyword.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # a string
        | #/000/tests/000/data | true  | type matches second anyOf, which has a real schema in it                         |
        # 
        | #/000/tests/001/data | false | type matches non-schema in first anyOf                                           |
        # 1
        | #/000/tests/002/data | false | type matches non-schema in third anyOf                                           |
