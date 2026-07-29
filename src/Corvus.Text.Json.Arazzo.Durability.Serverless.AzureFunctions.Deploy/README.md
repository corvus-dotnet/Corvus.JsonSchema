# Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy

The [Azure Functions](https://learn.microsoft.com/azure/azure-functions/) `IServerlessDeployer` for
the Arazzo serverless backend (ADR 0055, ADR 0059, ADR 0061). The runner deploys a version's verified
ReadyToRun isolated-worker app package by *run-from-package*: it uploads the app zip to a blob container
and points the target `dotnet-isolated` Function App at it (the `WEBSITE_RUN_FROM_PACKAGE` app setting),
then returns the app's HTTP-trigger invoke URL.

The package `BlobContainerClient` is injected. The runner wires it to
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for local development
and to the environment's real Azure Storage in production, and only the client's endpoint differs, so
the one deployer serves both — the same pattern the Lambda deployer uses over `IAmazonLambda` (ADR 0060).

The management-plane configuration (setting `WEBSITE_RUN_FROM_PACKAGE` and the source app settings, over
Azure Resource Manager) has no local emulator, so it is a separate injected seam,
`IFunctionAppConfigurator`: real ARM in production, a recording fake in tests. This mirrors the Azure gate
being asymmetric with Lambda's — the storage half is proven locally against Azurite, and the ARM half is
proven against real Azure, the analogue of Lambda's `AWS_IAM` Function URL auth being real-AWS-only
(ADR 0061).

This library never constructs a client and never handles credentials: the runner is the secure boundary
that holds the environment's cloud identity (ADR 0059). An Azure failure is returned as a failed
`ServerlessDeployResult`, not thrown.
