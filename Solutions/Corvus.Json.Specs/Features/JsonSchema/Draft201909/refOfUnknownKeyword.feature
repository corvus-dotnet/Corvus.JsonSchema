@draft2019-09

Feature: refOfUnknownKeyword draft2019-09
    In order to use json-schema
    As a developer
    I want to support refOfUnknownKeyword in draft2019-09

Scenario Outline: reference of a root arbitrary keyword 
/* Schema: 
{
            "unknown-keyword": {"type": "integer"},
            "properties": {
                "bar": {"$ref": "#/unknown-keyword"}
            }
        }
*/
    Given the input JSON file "optional\refOfUnknownKeyword.json"
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

Scenario Outline: reference of an arbitrary keyword of a sub-schema
/* Schema: 
{
            "properties": {
                "foo": {"unknown-keyword": {"type": "integer"}},
                "bar": {"$ref": "#/properties/foo/unknown-keyword"}
            }
        }
*/
    Given the input JSON file "optional\refOfUnknownKeyword.json"
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
