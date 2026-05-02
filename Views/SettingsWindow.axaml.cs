using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Omoi.Services;
using Omoi.ViewModels;
using Omoi.Models;

namespace Omoi.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.LoadKeysAsync();
        }
    }

    private async void AddOpenRouterKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.AddOrReplaceKeyAsync(CredentialsService.OPENROUTER);
        }
    }

    private async void DeleteOpenRouterKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.DeleteKeyAsync(CredentialsService.OPENROUTER);
        }
    }

    private async void AddAnlatanKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.AddOrReplaceKeyAsync(CredentialsService.ANLATAN);
        }
    }

    private async void DeleteAnlatanKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.DeleteKeyAsync(CredentialsService.ANLATAN);
        }
    }

    private async void AddSupabaseKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.AddOrReplaceKeyAsync(CredentialsService.SUPABASE);
        }
    }

    private async void DeleteSupabaseKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.DeleteKeyAsync(CredentialsService.SUPABASE);
        }
    }

    private async void AddQdrantKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.AddOrReplaceKeyAsync(CredentialsService.QDRANT);
        }
    }

    private async void DeleteQdrantKey_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.DeleteKeyAsync(CredentialsService.QDRANT);
        }
    }

    private async void SaveSupabaseUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.SaveSupabaseUrlAsync();
        }
    }

    private async void SaveQdrantUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.SaveQdrantUrlAsync();
        }
    }

    private async void SelectSupabase_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.SelectStorageProviderAsync(VectorStorageProvider.Supabase);
        }
    }

    private async void SelectQdrant_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsWindowViewModel viewModel)
        {
            await viewModel.SelectStorageProviderAsync(VectorStorageProvider.Qdrant);
        }
    }
}
