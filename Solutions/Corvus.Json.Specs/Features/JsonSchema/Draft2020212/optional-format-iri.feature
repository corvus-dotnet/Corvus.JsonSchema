@draft2020-12

Feature: optional-format-iri draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-iri in draft2020-12

Scenario Outline: validation of IRIs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "iri"
        }
*/
    Given the input JSON file "optional/format/iri.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
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
        # http://ƒøø.ßår/?∂éœ=πîx#πîüx
        | #/000/tests/006/data | true  | a valid IRI with anchor tag                                                      |
        # http://ƒøø.com/blah_(wîkïpédiå)_blah#ßité-1
        | #/000/tests/007/data | true  | a valid IRI with anchor tag and parentheses                                      |
        # http://ƒøø.ßår/?q=Test%20URL-encoded%20stuff
        | #/000/tests/008/data | true  | a valid IRI with URL-encoded stuff                                               |
        # http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com
        | #/000/tests/009/data | true  | a valid IRI with many special characters                                         |
        # http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]
        | #/000/tests/010/data | true  | a valid IRI based on IPv6                                                        |
        # http://2001:0db8:85a3:0000:0000:8a2e:0370:7334
        | #/000/tests/011/data | false | an invalid IRI based on IPv6                                                     |
        # /abc
        | #/000/tests/012/data | false | an invalid relative IRI Reference                                                |
        # \\WINDOWS\filëßåré
        | #/000/tests/013/data | false | an invalid IRI                                                                   |
        # âππ
        | #/000/tests/014/data | false | an invalid IRI though valid IRI reference                                        |
