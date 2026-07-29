# Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm

The [Azure Resource Manager](https://learn.microsoft.com/dotnet/api/overview/azure/resourcemanager-readme)
implementation of `IFunctionAppConfigurator` for the Arazzo serverless backend (ADR 0059, ADR 0061).
It is the real management-plane half of a run-from-package deploy: it points the target `dotnet-isolated`
Function App at the uploaded package (the `WEBSITE_RUN_FROM_PACKAGE` app setting) and sets the deployed
environment's source app settings, then returns the app's default host so the deployer can form the
invoke URL.

The `ArmClient` is injected. The runner constructs it with the environment's credential (a managed
identity or a least-privileged service principal), so this library holds no cloud identity of its own:
the runner is the secure boundary (ADR 0059). Settings are merged over the app's existing settings, so
the runtime settings the app was provisioned with (storage, worker runtime, functions version) are
preserved.

Azure has no management-plane emulator (Azurite emulates Storage only), so this half is verified against
real Azure, the analogue of the AWS Lambda deployer's `AWS_IAM` Function URL auth being real-AWS-only
(ADR 0060, ADR 0061). The deployer's storage half (the package upload and the run-from-package URL) is
verified locally against Azurite.
