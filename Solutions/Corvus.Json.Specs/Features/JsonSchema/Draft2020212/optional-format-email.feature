@draft2020-12

Feature: optional-format-email draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-email in draft2020-12

Scenario Outline: validation of e-mail addresses
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "email"
        }
*/
    Given the input JSON file "optional/format/email.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        # joe.bloggs@example.com
        | #/000/tests/006/data | true  | a valid e-mail address                                                           |
        # 2962
        | #/000/tests/007/data | false | an invalid e-mail address                                                        |
        # te~st@example.com
        | #/000/tests/008/data | true  | tilde in local part is valid                                                     |
        # ~test@example.com
        | #/000/tests/009/data | true  | tilde before local part is valid                                                 |
        # test~@example.com
        | #/000/tests/010/data | true  | tilde after local part is valid                                                  |
        # "joe bloggs"@example.com
        | #/000/tests/011/data | true  | a quoted string with a space in the local part is valid                          |
        # "joe..bloggs"@example.com
        | #/000/tests/012/data | true  | a quoted string with a double dot in the local part is valid                     |
        # "joe@bloggs"@example.com
        | #/000/tests/013/data | true  | a quoted string with a @ in the local part is valid                              |
        # joe.bloggs@[127.0.0.1]
        | #/000/tests/014/data | true  | an IPv4-address-literal after the @ is valid                                     |
        # joe.bloggs@[IPv6:::1]
        | #/000/tests/015/data | true  | an IPv6-address-literal after the @ is valid                                     |
        # .test@example.com
        | #/000/tests/016/data | false | dot before local part is not valid                                               |
        # test.@example.com
        | #/000/tests/017/data | false | dot after local part is not valid                                                |
        # te.s.t@example.com
        | #/000/tests/018/data | true  | two separated dots inside local part are valid                                   |
        # te..st@example.com
        | #/000/tests/019/data | false | two subsequent dots inside local part are not valid                              |
        # joe.bloggs@invalid=domain.com
        | #/000/tests/020/data | false | an invalid domain                                                                |
        # joe.bloggs@[127.0.0.300]
        | #/000/tests/021/data | false | an invalid IPv4-address-literal                                                  |
