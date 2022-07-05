
Feature: builder_spec_tests

Scenario: 4.1. add with missing object [c4f2aa93-9a06-4f04-8019-a900c0830d6b]
	Given the document {"q":{"bar":2}}
	When I build the patch [{"op":"add","path":"/a/b","value":1}]
	Then a patch exception should be thrown

Scenario: A.1.  Adding an Object Member [4ae47fb6-d3c4-4bbb-b5ac-88f3641222b3]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux"}]
	Then the patch result should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [c2d5caf8-ca2a-4d95-9802-bd8f99421e80]
	Given the document {"foo":["bar","baz"]}
	When I build the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	Then the patch result should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [920e4c18-8f18-4303-91ec-95b46aace529]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then the patch result should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [420b14f1-ae8d-4d06-9ebf-c82c54aedc21]
	Given the document {"foo":["bar","qux","baz"]}
	When I build the patch [{"op":"remove","path":"/foo/1"}]
	Then the patch result should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [ace7790f-be35-4465-914c-7f5af537f843]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"replace","path":"/baz","value":"boo"}]
	Then the patch result should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [a2578fbf-ac27-4960-88c5-362b80903b07]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	When I build the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	Then the patch result should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [f887ad53-9112-4365-8cb8-06a88132602e]
	Given the document {"foo":["all","grass","cows","eat"]}
	When I build the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	Then the patch result should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [937e49ea-c480-47ec-894c-80d738ff3c56]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	When I build the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	Then the patch result should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [a500b0d2-b8aa-4705-abf4-cdc97753a600]
	Given the document {"baz":"qux"}
	When I build the patch [{"op":"test","path":"/baz","value":"bar"}]
	Then a patch exception should be thrown

Scenario: A.10.  Adding a nested Member Object [c4101fb0-9fd7-4a26-b47b-aee62b3067e3]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	Then the patch result should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [91c3df35-e3b1-4469-90e8-43bfa1d16d82]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	Then the patch result should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [0beb63ed-e6a8-4f3c-9849-5fcb02326e1b]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	Then a patch exception should be thrown

Scenario: A.14. ~ Escape Ordering [4982936c-e312-444f-b795-4b16cd9ca489]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":10}]
	Then the patch result should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [1a4108f5-f4eb-49c5-822f-2b029014835f]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":"10"}]
	Then a patch exception should be thrown

Scenario: A.16. Adding an Array Value [4be5e792-7a70-471f-9159-3104de64fae3]
	Given the document {"foo":["bar"]}
	When I build the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	Then the patch result should equal {"foo":["bar",["abc","def"]]}
