@draft2019-09

Feature: vocabulary draft2019-09
    In order to use json-schema
    As a developer
    I want to support vocabulary in draft2019-09

Scenario Outline: schema that uses custom metaschema with with no validation vocabulary
/* Schema: 
{
            "$id": "https://schema/using/no/validation",
            "$schema": "http://localhost:1234/draft2019-09/metaschema-no-validation.json",
            "properties": {
                "badProperty": false,
                "numberProperty": {
                    "minimum": 10
                }
            }
        }
*/
    Given the input JSON file "vocabulary.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "badProperty": "this property should not exist" }
        | #/000/tests/000/data | false | applicator vocabulary still works                                                |
        # { "numberProperty": 20 }
        | #/000/tests/001/data | true  | no validation: valid number                                                      |
        # { "numberProperty": 1 }
        | #/000/tests/002/data | true  | no validation: invalid number, but it still validates                            |

Scenario Outline: ignore unrecognized optional vocabulary
/* Schema: 
{
             "$schema": "http://localhost:1234/draft2019-09/metaschema-optional-vocabulary.json",
             "type": "number"
         }
*/
    Given the input JSON file "vocabulary.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foobar
        | #/001/tests/000/data | false | string value                                                                     |
        # 20
        | #/001/tests/001/data | true  | number value                                                                     |
