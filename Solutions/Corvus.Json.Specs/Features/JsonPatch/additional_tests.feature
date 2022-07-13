Feature: additional_tests

Additional tests manually created to improve code coverage.

Scenario: add item to array with bad index leading zeroes should fail [672c1926-eb11-40be-8d10-3eff831a29b2]
Given the document ["foo","bar"]
	And the patch [{"op":"add","path":"/00","value":"foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Copy to same location has no effect [153f6d29-ddb7-4c56-9caf-967a75a88494]
	Given the document {"foo":1}
	And the patch [{"op":"copy","from":"/foo","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}