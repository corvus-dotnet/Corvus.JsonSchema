
Feature: builder_spec_tests

Scenario: 4.1. add with missing object [e9311282-4ed5-4580-85bc-e9b3e3d09814]
	Given the document {"q":{"bar":2}}
	When I build the patch [{"op":"add","path":"/a/b","value":1}]
	Then a patch exception should be thrown

Scenario: A.1.  Adding an Object Member [c048fa20-b3d8-4aa3-bd44-077db15733e4]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux"}]
	Then the patch result should equal {"baz":"qux","foo":"bar"}

Scenario: A.2.  Adding an Array Element [c4026a32-8b55-42b4-a466-81719b076125]
	Given the document {"foo":["bar","baz"]}
	When I build the patch [{"op":"add","path":"/foo/1","value":"qux"}]
	Then the patch result should equal {"foo":["bar","qux","baz"]}

Scenario: A.3.  Removing an Object Member [61493012-a08e-4dab-9831-8cc64e37e571]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then the patch result should equal {"foo":"bar"}

Scenario: A.4.  Removing an Array Element [ab4e5a07-0a4c-4027-ad49-71513a886102]
	Given the document {"foo":["bar","qux","baz"]}
	When I build the patch [{"op":"remove","path":"/foo/1"}]
	Then the patch result should equal {"foo":["bar","baz"]}

Scenario: A.5.  Replacing a Value [32231887-dcc1-4ea3-9fbf-90e2801b6c71]
	Given the document {"baz":"qux","foo":"bar"}
	When I build the patch [{"op":"replace","path":"/baz","value":"boo"}]
	Then the patch result should equal {"baz":"boo","foo":"bar"}

Scenario: A.6.  Moving a Value [5a770e0f-1221-4d08-af07-94f57ae60e31]
	Given the document {"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}
	When I build the patch [{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]
	Then the patch result should equal {"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}

Scenario: A.7.  Moving an Array Element [e905c322-0663-4d90-a304-d7304fa542c3]
	Given the document {"foo":["all","grass","cows","eat"]}
	When I build the patch [{"op":"move","from":"/foo/1","path":"/foo/3"}]
	Then the patch result should equal {"foo":["all","cows","eat","grass"]}

Scenario: A.8.  Testing a Value: Success [bf9eb3c7-a173-450f-ae90-fec054e12672]
	Given the document {"baz":"qux","foo":["a",2,"c"]}
	When I build the patch [{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]
	Then the patch result should equal {"baz":"qux","foo":["a",2,"c"]}

Scenario: A.9.  Testing a Value: Error [618332cc-204d-4be4-8aef-4bf953f163b0]
	Given the document {"baz":"qux"}
	When I build the patch [{"op":"test","path":"/baz","value":"bar"}]
	Then a patch exception should be thrown

Scenario: A.10.  Adding a nested Member Object [79442047-997b-4072-934e-170210f4562d]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/child","value":{"grandchild":{}}}]
	Then the patch result should equal {"foo":"bar","child":{"grandchild":{}}}

Scenario: A.11.  Ignoring Unrecognized Elements [436ca681-15f4-4dec-b15e-410f257d33c9]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz","value":"qux","xyz":123}]
	Then the patch result should equal {"foo":"bar","baz":"qux"}

Scenario: A.12.  Adding to a Non-existent Target [22a24524-0c28-4c67-a7dd-70867650b80c]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/baz/bat","value":"qux"}]
	Then a patch exception should be thrown

Scenario: A.14. ~ Escape Ordering [7f165fed-3833-4adb-9377-2edfd0ac462c]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":10}]
	Then the patch result should equal {"/":9,"~1":10}

Scenario: A.15. Comparing Strings and Numbers [17a617c3-c274-415a-842f-f56de1641bf1]
	Given the document {"/":9,"~1":10}
	When I build the patch [{"op":"test","path":"/~01","value":"10"}]
	Then a patch exception should be thrown

Scenario: A.16. Adding an Array Value [1b31dcd6-0b18-427e-8aa3-d56a503ff01a]
	Given the document {"foo":["bar"]}
	When I build the patch [{"op":"add","path":"/foo/-","value":["abc","def"]}]
	Then the patch result should equal {"foo":["bar",["abc","def"]]}
