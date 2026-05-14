# dotnet user-secrets — store API keys outside source control
# Run once after cloning:

dotnet user-secrets init
dotnet user-secrets set "LLM:ApiKey" "sk-your-key-here"

# For Anthropic:
# dotnet user-secrets set "LLM:ApiKey" "sk-ant-your-key-here"

# For Ollama (no key needed — just make sure Ollama is running):
# ollama pull llama3.2
