@draft2020-12

Feature: uri-template draft2020-12
    In order to use json-schema
    As a developer
    I want to support uri-template in draft2020-12

Scenario Outline: format: uri-template
/* Schema: 
{"format": "uri-template"}
*/
    Given the input JSON file "uri-template.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid uri-template                                                             |
        | #/000/tests/001/data | false | an invalid uri-template                                                          |
        | #/000/tests/002/data | true  | a valid uri-template without variables                                           |
        | #/000/tests/003/data | true  | a valid relative uri-template                                                    |
