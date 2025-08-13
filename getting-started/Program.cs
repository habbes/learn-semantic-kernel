using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.Json.Serialization;

DotNetEnv.Env.TraversePath().Load();

var modelId = Environment.GetEnvironmentVariable("MODEL_ID") ?? "gpt-4.1";
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new ArgumentNullException("AZURE_OPENAI_ENDPOINT required");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? throw new ArgumentNullException("AZURE_OPENAI_API_KEY required");

var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
Kernel kernel = builder.Build();
var chatCompletionService =  kernel.GetRequiredService<IChatCompletionService>();

// Add a plugin
kernel.Plugins.AddFromType<LightsPlugin>("Lights");

// Enable planning
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