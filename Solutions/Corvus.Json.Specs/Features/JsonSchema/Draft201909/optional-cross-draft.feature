@draft2019-09

Feature: optional-cross-draft draft2019-09
    In order to use json-schema
    As a developer
    I want to support optional-cross-draft in draft2019-09

Scenario Outline: refs to future drafts are processed as future drafts
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "type": "array",
            "$ref": "http://localhost:1234/draft2020-12/prefixItems.json"
        }
*/
    Given the input JSON file "optional/cross-draft.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1, 2, 3]
        | #/000/tests/000/data | false | first item not a string is invalid                                               |
        # ["a string", 1, 2, 3]
        | #/000/tests/001/data | true  | first item is a string is valid                                                  |

Scenario Outline: refs to historic drafts are processed as historic drafts
/* Schema: 
{
            "type": "object",
            "allOf": [
                { "properties": { "foo": true } },
                { "$ref": "http://localhost:1234/draft7/ignore-dependentRequired.json" }
            ]
        }
*/
    Given the input JSON file "optional/cross-draft.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "any value"}
        | #/001/tests/000/data | true  | missing bar is valid                                                             |
