// <copyright file="ArazzoExpressionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

[TestClass]
public class ArazzoExpressionTests
{
    [TestMethod]
    [DataRow("$url", ArazzoExpressionSource.Url)]
    [DataRow("$method", ArazzoExpressionSource.Method)]
    [DataRow("$statusCode", ArazzoExpressionSource.StatusCode)]
    [DataRow("$self", ArazzoExpressionSource.Self)]
    public void Parse_keyword_sources(string expression, ArazzoExpressionSource expected)
    {
        ArazzoExpression result = ArazzoExpression.Parse(expression);

        result.Source.ShouldBe(expected);
        result.Name.ShouldBeNull();
        result.HasJsonPointer.ShouldBeFalse();
    }

    [TestMethod]
    public void Parse_inputs_with_name()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$inputs.username");

        result.Source.ShouldBe(ArazzoExpressionSource.Inputs);
        result.Name.ShouldBe("username");
        result.HasJsonPointer.ShouldBeFalse();
    }

    [TestMethod]
    public void Parse_inputs_with_dotted_name_and_pointer()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$inputs.user.profile#/email");

        result.Source.ShouldBe(ArazzoExpressionSource.Inputs);
        result.Name.ShouldBe("user.profile");
        result.JsonPointer.ShouldBe("/email");
        result.HasJsonPointer.ShouldBeTrue();
    }

    [TestMethod]
    public void Parse_outputs_with_name()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$outputs.result");

        result.Source.ShouldBe(ArazzoExpressionSource.Outputs);
        result.Name.ShouldBe("result");
    }

    [TestMethod]
    public void Parse_step_output()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$steps.loginStep.outputs.token");

        result.Source.ShouldBe(ArazzoExpressionSource.Steps);
        result.ContainerId.ShouldBe("loginStep");
        result.Qualifier.ShouldBe("outputs");
        result.Name.ShouldBe("token");
    }

    [TestMethod]
    public void Parse_step_output_with_pointer()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$steps.create.outputs.data#/items/0");

        result.Source.ShouldBe(ArazzoExpressionSource.Steps);
        result.ContainerId.ShouldBe("create");
        result.Name.ShouldBe("data");
        result.JsonPointer.ShouldBe("/items/0");
    }

    [TestMethod]
    public void Parse_workflow_output()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$workflows.auth.outputs.token");

        result.Source.ShouldBe(ArazzoExpressionSource.Workflows);
        result.ContainerId.ShouldBe("auth");
        result.Qualifier.ShouldBe("outputs");
        result.Name.ShouldBe("token");
    }

    [TestMethod]
    public void Parse_workflow_input()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$workflows.auth.inputs.password");

        result.Source.ShouldBe(ArazzoExpressionSource.Workflows);
        result.ContainerId.ShouldBe("auth");
        result.Qualifier.ShouldBe("inputs");
        result.Name.ShouldBe("password");
    }

    [TestMethod]
    public void Parse_response_body_whole()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$response.body");

        result.Source.ShouldBe(ArazzoExpressionSource.ResponseBody);
        result.HasJsonPointer.ShouldBeFalse();
    }

    [TestMethod]
    public void Parse_response_body_with_pointer()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$response.body#/accessToken");

        result.Source.ShouldBe(ArazzoExpressionSource.ResponseBody);
        result.JsonPointer.ShouldBe("/accessToken");
    }

    [TestMethod]
    public void Parse_response_body_bare_hash_is_whole_value()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$response.body#");

        result.Source.ShouldBe(ArazzoExpressionSource.ResponseBody);
        result.HasJsonPointer.ShouldBeTrue();
        result.JsonPointer.ShouldBe(string.Empty);
    }

    [TestMethod]
    public void Parse_response_header()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$response.header.X-Expires-After");

        result.Source.ShouldBe(ArazzoExpressionSource.ResponseHeader);
        result.Name.ShouldBe("X-Expires-After");
    }

    [TestMethod]
    [DataRow("$request.path.id", ArazzoExpressionSource.RequestPath, "id")]
    [DataRow("$request.query.filter", ArazzoExpressionSource.RequestQuery, "filter")]
    [DataRow("$request.header.Authorization", ArazzoExpressionSource.RequestHeader, "Authorization")]
    public void Parse_request_named_sources(string expression, ArazzoExpressionSource expected, string name)
    {
        ArazzoExpression result = ArazzoExpression.Parse(expression);

        result.Source.ShouldBe(expected);
        result.Name.ShouldBe(name);
    }

    [TestMethod]
    public void Parse_request_body_with_pointer()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$request.body#/user/id");

        result.Source.ShouldBe(ArazzoExpressionSource.RequestBody);
        result.JsonPointer.ShouldBe("/user/id");
    }

    [TestMethod]
    public void Parse_message_payload_with_pointer()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$message.payload#/orderId");

        result.Source.ShouldBe(ArazzoExpressionSource.MessagePayload);
        result.JsonPointer.ShouldBe("/orderId");
    }

    [TestMethod]
    public void Parse_message_header()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$message.header.X-Correlation-ID");

        result.Source.ShouldBe(ArazzoExpressionSource.MessageHeader);
        result.Name.ShouldBe("X-Correlation-ID");
    }

    [TestMethod]
    public void Parse_source_description_field()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$sourceDescriptions.petStore.url");

        result.Source.ShouldBe(ArazzoExpressionSource.SourceDescriptions);
        result.ContainerId.ShouldBe("petStore");
        result.Name.ShouldBe("url");
    }

    [TestMethod]
    public void Parse_component_reference_with_dotted_name()
    {
        ArazzoExpression result = ArazzoExpression.Parse("$components.parameters.my.apiKey");

        result.Source.ShouldBe(ArazzoExpressionSource.Components);
        result.Qualifier.ShouldBe("parameters");
        result.Name.ShouldBe("my.apiKey");
    }

    [TestMethod]
    [DataRow("Bearer abc123")]
    [DataRow("application/json")]
    [DataRow("")]
    public void Parse_non_dollar_values_are_literals(string expression)
    {
        ArazzoExpression result = ArazzoExpression.Parse(expression);

        result.Source.ShouldBe(ArazzoExpressionSource.Literal);
        result.LiteralValue.ShouldBe(expression);
    }

    [TestMethod]
    [DataRow("$bogus.thing")]
    [DataRow("$steps.onlyStepNoOutputs")]
    [DataRow("$request.unknown")]
    public void Parse_unrecognized_dollar_forms_fall_back_to_literal(string expression)
    {
        ArazzoExpression result = ArazzoExpression.Parse(expression);

        result.Source.ShouldBe(ArazzoExpressionSource.Literal);
        result.LiteralValue.ShouldBe(expression);
    }

    [TestMethod]
    public void Parse_null_throws()
    {
        Should.Throw<ArgumentNullException>(() => ArazzoExpression.Parse(null!));
    }
}