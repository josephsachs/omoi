using System;
using System.Threading.Tasks;
using ReactiveUI;
using Omoi.Services;
using Omoi.Models;

namespace Omoi.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private readonly CredentialsService _credentialsService;
    private readonly OpenRouterClient _openRouterClient;
    private readonly ConfigService _configService;

    private string _openRouterKeyDisplay = "(not set)";
    private string _anlatanKeyDisplay = "(not set)";
    private string _supabaseKeyDisplay = "(not set)";
    private string _qdrantKeyDisplay = "(not set)";

    private bool _hasOpenRouterKey;
    private bool _hasAnlatanKey;
    private bool _hasSupabaseKey;
    private bool _hasQdrantKey;

    private string _openRouterKeyInput = string.Empty;
    private string _anlatanKeyInput = string.Empty;
    private string _supabaseKeyInput = string.Empty;
    private string _qdrantKeyInput = string.Empty;

    private string _supabaseUrlInput = string.Empty;
    private string _qdrantUrlInput = string.Empty;

    private bool _isSupabaseSelected;
    private bool _isQdrantSelected;

    public string OpenRouterKeyDisplay
    {
        get => _openRouterKeyDisplay;
        set => this.RaiseAndSetIfChanged(ref _openRouterKeyDisplay, value);
    }

    public string AnlatanKeyDisplay
    {
        get => _anlatanKeyDisplay;
        set => this.RaiseAndSetIfChanged(ref _anlatanKeyDisplay, value);
    }

    public string SupabaseKeyDisplay
    {
        get => _supabaseKeyDisplay;
        set => this.RaiseAndSetIfChanged(ref _supabaseKeyDisplay, value);
    }

    public string QdrantKeyDisplay
    {
        get => _qdrantKeyDisplay;
        set => this.RaiseAndSetIfChanged(ref _qdrantKeyDisplay, value);
    }

    public bool HasOpenRouterKey
    {
        get => _hasOpenRouterKey;
        set => this.RaiseAndSetIfChanged(ref _hasOpenRouterKey, value);
    }

    public bool HasAnlatanKey
    {
        get => _hasAnlatanKey;
        set => this.RaiseAndSetIfChanged(ref _hasAnlatanKey, value);
    }

    public bool HasSupabaseKey
    {
        get => _hasSupabaseKey;
        set => this.RaiseAndSetIfChanged(ref _hasSupabaseKey, value);
    }

    public bool HasQdrantKey
    {
        get => _hasQdrantKey;
        set => this.RaiseAndSetIfChanged(ref _hasQdrantKey, value);
    }

    public string OpenRouterKeyInput
    {
        get => _openRouterKeyInput;
        set => this.RaiseAndSetIfChanged(ref _openRouterKeyInput, value);
    }

    public string AnlatanKeyInput
    {
        get => _anlatanKeyInput;
        set => this.RaiseAndSetIfChanged(ref _anlatanKeyInput, value);
    }

    public string SupabaseKeyInput
    {
        get => _supabaseKeyInput;
        set => this.RaiseAndSetIfChanged(ref _supabaseKeyInput, value);
    }

    public string QdrantKeyInput
    {
        get => _qdrantKeyInput;
        set => this.RaiseAndSetIfChanged(ref _qdrantKeyInput, value);
    }

    public string SupabaseUrlInput
    {
        get => _supabaseUrlInput;
        set => this.RaiseAndSetIfChanged(ref _supabaseUrlInput, value);
    }

    public string QdrantUrlInput
    {
        get => _qdrantUrlInput;
        set => this.RaiseAndSetIfChanged(ref _qdrantUrlInput, value);
    }

    public bool IsSupabaseSelected
    {
        get => _isSupabaseSelected;
        set => this.RaiseAndSetIfChanged(ref _isSupabaseSelected, value);
    }

    public bool IsQdrantSelected
    {
        get => _isQdrantSelected;
        set => this.RaiseAndSetIfChanged(ref _isQdrantSelected, value);
    }

    public SettingsWindowViewModel(CredentialsService credentialsService, OpenRouterClient openRouterClient, ConfigService configService)
    {
        _credentialsService = credentialsService;
        _openRouterClient = openRouterClient;
        _configService = configService;
    }

    public async Task LoadKeysAsync()
    {
        await UpdateKeyDisplayAsync(CredentialsService.OPENROUTER);
        await UpdateKeyDisplayAsync(CredentialsService.ANLATAN);
        await UpdateKeyDisplayAsync(CredentialsService.SUPABASE);
        await UpdateKeyDisplayAsync(CredentialsService.QDRANT);
        await LoadUrlsAsync();
        await LoadStorageProviderAsync();
    }

    private async Task LoadUrlsAsync()
    {
        var config = await _configService.LoadConfigAsync();
        SupabaseUrlInput = config.SupabaseUrl;
        QdrantUrlInput = config.QdrantUrl;
    }

    public async Task AddOrReplaceKeyAsync(string provider)
    {
        string keyInput = provider switch
        {
            CredentialsService.OPENROUTER => OpenRouterKeyInput,
            CredentialsService.ANLATAN => AnlatanKeyInput,
            CredentialsService.SUPABASE => SupabaseKeyInput,
            CredentialsService.QDRANT => QdrantKeyInput,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(keyInput))
            return;

        await _credentialsService.SetApiKeyAsync(provider, keyInput);

        if (provider == CredentialsService.OPENROUTER)
        {
            _openRouterClient.SetApiKey(keyInput);
        }

        ClearInput(provider);
        await UpdateKeyDisplayAsync(provider);
    }

    public async Task DeleteKeyAsync(string provider)
    {
        await _credentialsService.DeleteApiKeyAsync(provider);
        await UpdateKeyDisplayAsync(provider);
    }

    private async Task UpdateKeyDisplayAsync(string provider)
    {
        var key = await _credentialsService.GetApiKeyAsync(provider);
        var hasKey = !string.IsNullOrWhiteSpace(key);
        var display = hasKey && key?.Length >= 4 ? $"****{key[^4..]}" : "(not set)";

        switch (provider)
        {
            case CredentialsService.OPENROUTER:
                HasOpenRouterKey = hasKey;
                OpenRouterKeyDisplay = display;
                break;
            case CredentialsService.ANLATAN:
                HasAnlatanKey = hasKey;
                AnlatanKeyDisplay = display;
                break;
            case CredentialsService.SUPABASE:
                HasSupabaseKey = hasKey;
                SupabaseKeyDisplay = display;
                break;
            case CredentialsService.QDRANT:
                HasQdrantKey = hasKey;
                QdrantKeyDisplay = display;
                break;
        }
    }

    private void ClearInput(string provider)
    {
        switch (provider)
        {
            case CredentialsService.OPENROUTER:
                OpenRouterKeyInput = string.Empty;
                break;
            case CredentialsService.ANLATAN:
                AnlatanKeyInput = string.Empty;
                break;
            case CredentialsService.SUPABASE:
                SupabaseKeyInput = string.Empty;
                break;
            case CredentialsService.QDRANT:
                QdrantKeyInput = string.Empty;
                break;
        }
    }

    public async Task SaveSupabaseUrlAsync()
    {
        var config = await _configService.LoadConfigAsync();
        config.SupabaseUrl = SupabaseUrlInput;
        await _configService.SaveConfigAsync(config);
    }

    public async Task SaveQdrantUrlAsync()
    {
        var config = await _configService.LoadConfigAsync();
        config.QdrantUrl = QdrantUrlInput;
        await _configService.SaveConfigAsync(config);
    }

    public async Task SelectStorageProviderAsync(VectorStorageProvider provider)
    {
        var config = await _configService.LoadConfigAsync();
        config.SelectedVectorStorage = provider;
        await _configService.SaveConfigAsync(config);
        await LoadStorageProviderAsync();
    }

    private async Task LoadStorageProviderAsync()
    {
        var config = await _configService.LoadConfigAsync();
        IsSupabaseSelected = config.SelectedVectorStorage == VectorStorageProvider.Supabase;
        IsQdrantSelected = config.SelectedVectorStorage == VectorStorageProvider.Qdrant;
    }
}
