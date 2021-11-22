@draft2020-12

Feature: defs draft2020-12
    In order to use json-schema
    As a developer
    I want to support defs in draft2020-12

Scenario Outline: validate definition against metaschema
/* Schema: 
{
            "$ref": "https://json-schema.org/draft/2020-12/schema"
        }
*/
    Given the input JSON file "defs.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | valid definition schema                                                          |
        | #/000/tests/001/data | false | invalid definition schema                                                        |
