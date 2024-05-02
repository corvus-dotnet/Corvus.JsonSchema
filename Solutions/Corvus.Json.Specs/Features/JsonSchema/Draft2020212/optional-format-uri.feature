@draft2020-12

Feature: optional-format-uri draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-uri in draft2020-12

Scenario Outline: validation of URIs
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "uri"
        }
*/
    Given the input JSON file "optional/format/uri.json"
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
        # http://foo.bar/?baz=qux#quux
        | #/000/tests/006/data | true  | a valid URL with anchor tag                                                      |
        # http://foo.com/blah_(wikipedia)_blah#cite-1
        | #/000/tests/007/data | true  | a valid URL with anchor tag and parentheses                                      |
        # http://foo.bar/?q=Test%20URL-encoded%20stuff
        | #/000/tests/008/data | true  | a valid URL with URL-encoded stuff                                               |
        # http://xn--nw2a.xn--j6w193g/
        | #/000/tests/009/data | true  | a valid puny-coded URL                                                           |
        # http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com
        | #/000/tests/010/data | true  | a valid URL with many special characters                                         |
        # http://223.255.255.254
        | #/000/tests/011/data | true  | a valid URL based on IPv4                                                        |
        # ftp://ftp.is.co.za/rfc/rfc1808.txt
        | #/000/tests/012/data | true  | a valid URL with ftp scheme                                                      |
        # http://www.ietf.org/rfc/rfc2396.txt
        | #/000/tests/013/data | true  | a valid URL for a simple text file                                               |
        # ldap://[2001:db8::7]/c=GB?objectClass?one
        | #/000/tests/014/data | true  | a valid URL                                                                      |
        # mailto:John.Doe@example.com
        | #/000/tests/015/data | true  | a valid mailto URI                                                               |
        # news:comp.infosystems.www.servers.unix
        | #/000/tests/016/data | true  | a valid newsgroup URI                                                            |
        # tel:+1-816-555-1212
        | #/000/tests/017/data | true  | a valid tel URI                                                                  |
        # urn:oasis:names:specification:docbook:dtd:xml:4.1.2
        | #/000/tests/018/data | true  | a valid URN                                                                      |
        # //foo.bar/?baz=qux#quux
        | #/000/tests/019/data | false | an invalid protocol-relative URI Reference                                       |
        # /abc
        | #/000/tests/020/data | false | an invalid relative URI Reference                                                |
        # \\WINDOWS\fileshare
        | #/000/tests/021/data | false | an invalid URI                                                                   |
        # abc
        | #/000/tests/022/data | false | an invalid URI though valid URI reference                                        |
        # http:// shouldfail.com
        | #/000/tests/023/data | false | an invalid URI with spaces                                                       |
        # :// should fail
        | #/000/tests/024/data | false | an invalid URI with spaces and missing scheme                                    |
        # bar,baz:foo
        | #/000/tests/025/data | false | an invalid URI with comma in scheme                                              |
