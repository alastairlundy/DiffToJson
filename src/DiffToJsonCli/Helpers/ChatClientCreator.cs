using System.ClientModel;
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace DiffToJsonCli.Helpers;

internal static class ChatClientCreator
{
    internal static IChatClient CreateClient(string provider, string apiKey, string endpoint, string model)
    {
        IChatClient client;
        
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(model);
        
        if(string.IsNullOrEmpty(provider))
            ArgumentException.ThrowIfNullOrEmpty(apiKey);
        
        switch (provider.ToLower())
        {
            case "anthropic":
            {
                client = new AnthropicClient(new ClientOptions
                    {
                        ApiKey = apiKey,
                        BaseUrl = endpoint
                    })
                    .AsIChatClient(model);
                break;
            }
            case "ollama":
            {
                OllamaApiClient.Configuration configuration = new OllamaApiClient.Configuration
                {
                    JsonSerializerContext = CustomOllamaJsonContext.Default,
                    Model = model,
                    Uri = new Uri(endpoint)
                };
                client = new OllamaApiClient(configuration);

                break;
            }
            default:
            {
                client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                    {
                        Endpoint = new  Uri(endpoint)
                    }) .GetChatClient(model)
                    .AsIChatClient();
                
                break;
            }
        }

        return client;
    }
}