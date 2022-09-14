@draft2020-12

Feature: cross-draft draft2020-12
    In order to use json-schema
    As a developer
    I want to support cross-draft in draft2020-12

Scenario Outline: refs to historic drafts are processed as historic drafts
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "$ref": "http://localhost:1234/draft2019-09/ignore-prefixItems.json"
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
        | #/000/tests/000/data | true  | first item not a string is valid                                                 |
