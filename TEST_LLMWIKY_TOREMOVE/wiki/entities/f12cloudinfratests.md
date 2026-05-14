---
title: F12CloudInfraTests
category: entity
tags: [test, cloud-infrastructure, aws, azure, gcp, f12]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12CloudInfraTests

## Summary
The `F12CloudInfraTests` file contains unit tests for the cloud infrastructure toolkits provided by AGXIOMA. It validates the identity (Id, Name, Category), unconfigured behavior (returns `Skipped`), and configured execution (returns `Ok`) for eight toolkits covering AWS (S3, Lambda, DynamoDB, EC2), Azure (Blob, Functions), and GCP (Storage, Cloud Run). These tests ensure that each toolkit adheres to the expected contract and can be safely skipped when not configured.

## Key Details
- **Test classes** (1 class per toolkit): `AwsS3ToolkitTests`, `AwsLambdaToolkitTests`, `AwsDynamoDbToolkitTests`, `AwsEc2ToolkitTests`, `AzureBlobToolkitTests`, `AzureFunctionsToolkitTests`, `GcpStorageToolkitTests`, `GcpCloudRunToolkitTests`.
- Each class contains four test methods:
  - `Id_IsCorrect` – verifies `Id` equals the expected string (e.g., `"aws_s3"`).
  - `Name_IsCorrect` – verifies `Name` (e.g., `"AWS S3"`).
  - `Category_IsCorrect` – verifies `Category` is `ToolkitCategory.CloudInfra`.
  - `NotConfigured_ReturnsSkipped` – calls `ExecuteAsync` with no config, expects `ToolkitResultStatus.Skipped`.
  - `Execute_WithConfig_ReturnsOk` – calls `ExecuteAsync` with a `ToolkitConfig` containing an `api_key`, expects `ToolkitResultStatus.Ok`.
- The `TelemetryToolkitTests` class (partially shown) also appears but is not fully included in the source excerpt.
- All tests are marked as `[Fact]` and return `Task` (async).

## Cross-References
- [[agxioma-toolkits]] — Base class for all toolkits, whose behavior is verified here.
- [[cloudinfra-toolkits]] — Conceptual page for the `ToolkitCategory.CloudInfra` grouping.
- [[f12-test-suite]] — Overview of the F12 test namespace and its structure.

## Source Files
- `AGXIOME.Tests/F12/F12CloudInfraTests.cs` (ingested 2026-05-14)