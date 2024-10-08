@draft7

Feature: optional-cross-draft draft7
    In order to use json-schema
    As a developer
    I want to support optional-cross-draft in draft7

Scenario Outline: refs to future drafts are processed as future drafts
/* Schema: 
{
            "type": "object",
            "allOf": [
                { "properties": { "foo": true } },
                { "$ref": "http://localhost:1234/draft2019-09/dependentRequired.json" }
            ]
        }
*/
    Given the input JSON file "optional/cross-draft.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # {"foo": "any value"}
        | #/000/tests/000/data | false | missing bar is invalid                                                           |
        # {"foo": "any value", "bar": "also any value"}
        | #/000/tests/001/data | true  | present bar is valid                                                             |
