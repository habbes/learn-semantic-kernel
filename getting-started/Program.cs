using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.Json.Serialization;

DotNetEnv.Env.TraversePath().Load();

var modelId = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_ID") ?? "gpt-5-mini";
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new ArgumentNullException("AZURE_OPENAI_ENDPOINT required");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? throw new ArgumentNullException("AZURE_OPENAI_API_KEY required");

// See AI services supported by semantic kernel:
//   - https://learn.microsoft.com/en-us/semantic-kernel/get-started/supported-languages?pivots=programming-language-csharp
var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
Kernel kernel = builder.Build();

// To learn more about the kernel:https://learn.microsoft.com/en-us/semantic-kernel/concepts/kernel
// To learn more about semantic kernel components: https://learn.microsoft.com/en-us/semantic-kernel/concepts/semantic-kernel-components

// Retrieve the chat completion service registerd above
var chatCompletionService =  kernel.GetRequiredService<IChatCompletionService>();

// Add a plugin that provide additional functionality (e.g. tool calling, fetching data from external sources, etc.)
// The LightsPlugin provides the agent the list of available bulbs and their state
//  It also allows the agent to change the state of a light
// To learn more about RAG: https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/using-data-retrieval-functions-for-rag
// To learn more about task automation: https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/using-task-automation-functions
kernel.Plugins.AddFromType<LightsPlugin>("Lights");

// Enable planning
// Semantic Kernel leverages the "native" function calling features of the API
// To learn more: https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/function-calling/
// This configuration tells SK to automatically invoke functions in the kernel
// when the agent requests them (an alternative could be None, in which
// case the functions won't be called automatically, allowing the user
// to validate them first).
OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Create history to store the conversation
var history = new ChatHistory();

string? userInput;
do
{
    Console.Write("User > ");
    userInput = Console.ReadLine();

    history.AddUserMessage(userInput!);

    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: openAIPromptExecutionSettings,
        kernel: kernel);

    Console.WriteLine("Assistant > " + result);

    // Add message from agent to chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
} while (userInput is not null);

class LightsPlugin
{
    // Mock data for the lights
    private readonly List<LightModel> lights = new()
    {
        new LightModel { Id = 1, Name = "Table Lamp", IsOn = false },
        new LightModel { Id = 2, Name = "Porch light", IsOn = false },
        new LightModel { Id = 3, Name = "Chandelier", IsOn = true }
    };

    [KernelFunction("get_lights")]
    [Description("Get a list of lights and their current state")]
    public Task<List<LightModel>> GetLights()
    {
        return Task.FromResult(lights);
    }

    [KernelFunction("change_state")]
    [Description("Changes the state of the light")]
    public Task<LightModel?> ChangeStateAsync(int id, bool isOn)
    {
        var light = lights.FirstOrDefault(l => l.Id == id);

        if (light == null)
        {
            return Task.FromResult<LightModel?>(null);
        }

        light.IsOn = isOn;
        return Task.FromResult<LightModel?>(light);
    }
}

public class LightModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("is_on")]
    public bool? IsOn { get; set; }
}