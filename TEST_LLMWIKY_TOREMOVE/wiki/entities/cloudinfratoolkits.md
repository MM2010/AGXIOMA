---
title: Cloudinfratoolkits
category: entity
tags: [cloud, infrastructure, aws, azure, toolkit, cli]
source_count: 6
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Cloudinfratoolkits

## Summary
A collection of lightweight toolkits that provide access to AWS and Azure cloud services by delegating to their respective CLI tools (`aws`, `az`) rather than bundling heavy SDKs. For operations not easily covered by the CLI (e.g., DynamoDB, EC2, Azure Blob), they return informative stub messages. These toolkits enable agents to perform common cloud operations (list buckets, invoke functions, manage storage) via a unified `ToolkitBase` interface.

## Key Details
- **AWS S3** (`aws_s3`): list buckets, list objects, upload/download/delete, presign URLs. Uses `aws s3` CLI.
- **AWS Lambda** (`aws_lambda`): invoke functions, list/get functions, list versions. Uses `aws lambda` CLI.
- **AWS DynamoDB** (`aws_dynamodb`): stub only – advises using the `aws dynamodb` CLI directly or the AWSSDK.DynamoDBv2 package.
- **AWS EC2** (`aws_ec2`): stub only – advises using the `aws ec2` CLI directly or the AWSSDK.EC2 package.
- **Azure Blob** (`azure_blob`): list containers, list blobs; delegates to `az storage blob` CLI when `connection_string` or `account_name` is configured.
- All toolkits accept shared settings (`access_key_id`, `secret_access_key`, `region` for AWS; `connection_string`, `account_name` for Azure) and fall back to environment variables.
- Execution uses `System.Diagnostics.Process` to launch the CLI; results are returned as success/failure with command output.

## Cross-References
- [[ToolkitBase]] — base class for all toolkits, providing parameter handling and configuration.
- [[ToolkitConfig]] — configuration object used when constructing toolkits.
- [[ToolkitResult]] — result type returned by all toolkit executions.

## Source Files
- `AGXIOME.Toolkits/CloudInfra/CloudInfraToolkits.cs` (ingested 2026-05-14)