@draft2020-12

Feature: infinite-loop-detection draft2020-12
    In order to use json-schema
    As a developer
    I want to support infinite-loop-detection in draft2020-12

Scenario Outline: evaluating the same schema location against the same data location twice is not a sign of an infinite loop
/* Schema: 
{
            "$defs": {
                "int": { "type": "integer" }
            },
            "allOf": [
                {
                    "properties": {
                        "foo": {
                            "$ref": "#/$defs/int"
                        }
                    }
                },
                {
                    "additionalProperties": {
                        "$ref": "#/$defs/int"
                    }
                }
            ]
        }
*/
    Given the input JSON file "infinite-loop-detection.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | passing case                                                                     |
        | #/000/tests/001/data | false | failing case                                                                     |
