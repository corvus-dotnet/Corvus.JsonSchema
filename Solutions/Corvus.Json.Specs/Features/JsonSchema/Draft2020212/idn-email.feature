@draft2020-12

Feature: idn-email draft2020-12
    In order to use json-schema
    As a developer
    I want to support idn-email in draft2020-12

Scenario Outline: validation of an internationalized e-mail addresses
/* Schema: 
{"format": "idn-email"}
*/
    Given the input JSON file "idn-email.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid idn e-mail (example@example.test in Hangul)                              |
        | #/000/tests/001/data | false | an invalid idn e-mail address                                                    |
        | #/000/tests/002/data | true  | a valid e-mail address                                                           |
        | #/000/tests/003/data | false | an invalid e-mail address                                                        |
