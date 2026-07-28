# Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy

The [AWS Lambda](https://aws.amazon.com/lambda/) `IServerlessDeployer` for the Arazzo serverless
backend (ADR 0055, ADR 0059, ADR 0060). The runner packages a version's verified native binary into
a `provided.al2023` custom-runtime deployment zip (a single executable `bootstrap` entry at the zip
root) and creates or updates a Lambda function with an `AWS_IAM` Function URL, then returns that URL.

The `IAmazonLambda` client is injected. The runner wires it to [LocalStack](https://localstack.cloud/)
for local development and to real AWS in production, and only the client's endpoint differs, so the
one deployer serves both (ADR 0060). This library never constructs a client and never handles
credentials: the runner is the secure boundary that holds the environment's cloud identity
(ADR 0059). An AWS failure is returned as a failed `ServerlessDeployResult`, not thrown.
