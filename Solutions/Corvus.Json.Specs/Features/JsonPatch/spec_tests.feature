
Feature: spec_tests

Scenario: 4.1. add with missing object [59af5a73-ea62-4458-b15f-88a786e4ae02]
	Given the document {"q":{"bar":2}}
	And the patch [{"op":"add","path":"/a/b","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.1.  Adding an Object Member [12a01685-b13f-4ae8-9fdf-940517283805]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [6d1ec1f6-3771-4e83-8213-21c88b98c1db]
	Given the document {"foo":["bar","baz"]}
	And the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [8c205e94-4fe5-42c4-9659-b6d8dd7e0835]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [4e597fd0-87f2-481b-b4c3-8ad519d89c7f]
	Given the document {"foo":["bar","qux","baz"]}
	And the patch [{"op":"remove","path":"/foo/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [be5b69cc-4967-4204-887f-af25c9038136]
	Given the document {"baz":"qux","foo":"bar"}
	And the patch [{"op":"replace","path":"/baz","value":"boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [e752e345-36f9-4122-a97a-1a18355c5a11]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	And the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [d4622da1-5163-44be-9e0b-43c77b8a56af]
	Given the document {"foo":["all","grass","cows","eat"]}
	And the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [d39fd2db-749f-4db5-bf29-065fdbdee99f]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	And the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [24523091-d1e5-48ab-a167-2cafd9b6f55c]
	Given the document {"baz":"qux"}
	And the patch [{"op":"test","path":"/baz","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.10.  Adding a nested Member Object [069edb80-5218-486a-b769-b44ee46dff7c]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [6a9b70dd-a15e-4f75-8046-e92a47403333]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [5c782101-84ba-4dee-a486-66141b53b390]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.14. ~ Escape Ordering [edbf8603-daff-455c-aba6-8b0f2a9fa9bb]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":10}]
	When I apply the patch to the document
	Then the transformed document should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [4f4ecf1e-a3db-41c5-9d70-95bebb7a3ab3]
	Given the document {"/":9,"~1":10}
	And the patch [{"op":"test","path":"/~01","value":"10"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: A.16. Adding an Array Value [25c2b2cd-1e80-49ed-8c50-fb521383b225]
	Given the document {"foo":["bar"]}
	And the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":["bar",["abc","def"]]}
