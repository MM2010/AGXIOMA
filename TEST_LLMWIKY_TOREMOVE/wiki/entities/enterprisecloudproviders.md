---
title: Enterprise Cloud Providers
category: entity
tags: [enterprise, cloud, provider, LLM]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Enterprise Cloud Providers

## Summary
The `EnterpriseCloudProviders` classes (AWSBedrock, AzureAIFoundry, IBMWatsonX, SambaNova) implement `ILLMProvider` to access large language models hosted on major cloud platforms. Each wraps a standard provider (OpenAIProvider or AzureOpenAIProvider) with a tailored endpoint and supported model list. They offer text completion with streaming and function calling, but embeddings require separate dedicated endpoints.

## Key Details
- **AWS Bedrock** (`AWSBedrockProvider`): Routes Anthropic, Meta, Mistral, and Amazon Titan models. Supports streaming, function calling, vision, up to 200K context tokens. Embeddings not supported.
- **Azure AI Foundry** (`AzureAIFoundryProvider`): Hosts Phi-4, Phi-3.5, Meta Llama, Mistral models. Supports streaming and function calling, no vision, up to 131K context. No embeddings.
- **IBM WatsonX** (`IBMWatsonXProvider`): Granite and Llama models via IBM Cloud. Supports streaming and function calling, no vision, up to 128K context. No embeddings.
- **SambaNova** (`SambaNovaProvider`): Optimized hardware for Llama 3 and Samba-1 models. Uses `api.sambanova.ai/v1` endpoint. Supports streaming and function calling, no vision, 131K context. No embeddings.
- All four providers delegate completion calls to an inner `OpenAIProvider` or `AzureOpenAIProvider`, injecting platform-specific configuration.
- Embedding methods throw `NotSupportedException`; users should configure a separate embedding deployment for each cloud.

## Cross-References
- [[entities/illmprovider]] — Interface implemented by all enterprise cloud providers.
- [[entities/llmwikiconfig]] — Configuration object used to set endpoint and API key per provider.
- [[entities/llmprovidercapabilities]] — Structure defining streaming, function calling, vision, and model support flags.
- [[entities/openai-provider]] — Inner provider used by AWS Bedrock, IBM WatsonX, and SambaNova.
- [[entities/azure-openai-provider]] — Inner provider used by Azure AI Foundry.

## Source Files
- `AGXIOME.LLMWIKY/Providers/EnterpriseCloudProviders.cs` (ingested 2026-05-14)