@draft2019-09

Feature: email draft2019-09
    In order to use json-schema
    As a developer
    I want to support email in draft2019-09

Scenario Outline: validation of e-mail addresses
/* Schema: 
{"format": "email"}
*/
    Given the input JSON file "email.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid e-mail address                                                           |
        | #/000/tests/001/data | false | an invalid e-mail address                                                        |
        | #/000/tests/002/data | true  | tilde in local part is valid                                                     |
        | #/000/tests/003/data | true  | tilde before local part is valid                                                 |
        | #/000/tests/004/data | true  | tilde after local part is valid                                                  |
        | #/000/tests/005/data | false | dot before local part is not valid                                               |
        | #/000/tests/006/data | false | dot after local part is not valid                                                |
        | #/000/tests/007/data | true  | two separated dots inside local part are valid                                   |
        | #/000/tests/008/data | false | two subsequent dots inside local part are not valid                              |
