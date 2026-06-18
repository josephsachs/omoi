using System;
using System.Threading.Tasks;
using ReactiveUI;
using Omoi.Services;

namespace Omoi.ViewModels;

public class ChatSettingsViewModel : ViewModelBase
{
    private readonly ConfigService _configService;

    private string _personalityPrompt = "You are Omoi (思), a social chatbot. You use precise descriptions and intellectual terminology; you do not use metaphor, and you avoid flowery speech. You do not use formatting.";
    private string _maxContextMessages = "42";
    private string _memoryStoreInterval = "4";
    private string _topKMemories = "5";
    private string _similarityThreshold = "0.12";

    public string PersonalityPrompt
    {
        get => _personalityPrompt;
        set => this.RaiseAndSetIfChanged(ref _personalityPrompt, value);
    }

    public string MaxContextMessages
    {
        get => _maxContextMessages;
        set => this.RaiseAndSetIfChanged(ref _maxContextMessages, value);
    }

    public string MemoryStoreInterval
    {
        get => _memoryStoreInterval;
        set => this.RaiseAndSetIfChanged(ref _memoryStoreInterval, value);
    }

    public string TopKMemories
    {
        get => _topKMemories;
        set => this.RaiseAndSetIfChanged(ref _topKMemories, value);
    }

    public string SimilarityThreshold
    {
        get => _similarityThreshold;
        set => this.RaiseAndSetIfChanged(ref _similarityThreshold, value);
    }

    public ChatSettingsViewModel(ConfigService configService)
    {
        _configService = configService;
    }

    public async Task LoadSettingsAsync()
    {
        var config = await _configService.LoadConfigAsync();
        
        PersonalityPrompt = config.PersonalityPrompt;
        MaxContextMessages = config.MaxContextMessages.ToString();
        MemoryStoreInterval = config.MemoryStoreInterval.ToString();
        TopKMemories = config.TopKMemories.ToString();
        SimilarityThreshold = config.SimilarityThreshold.ToString("F5");
    }

    public async Task SaveSettingsAsync()
    {
        var config = await _configService.LoadConfigAsync();

        config.PersonalityPrompt = string.IsNullOrWhiteSpace(PersonalityPrompt)
            ? "You are Omoi (思), a social chatbot. You use precise descriptions and intellectual terminology; you do not use metaphor, and you avoid flowery speech. You do not use formatting."
            : PersonalityPrompt;

        config.MaxContextMessages = int.TryParse(MaxContextMessages, out var maxContext) ? maxContext : 42;
        config.MemoryStoreInterval = int.TryParse(MemoryStoreInterval, out var memInterval) ? memInterval : 4;
        config.TopKMemories = int.TryParse(TopKMemories, out var topK) ? topK : 5;
        config.SimilarityThreshold = float.TryParse(SimilarityThreshold, out var threshold) ? threshold : 0.12f;

        await _configService.SaveConfigAsync(config);
    }
}