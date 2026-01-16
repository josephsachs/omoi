using Avalonia.Controls;
using Omoi.ViewModels;

namespace Omoi.Views;

public partial class ChatSettingsWindow : Window
{
    public ChatSettingsWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        
        if (DataContext is ChatSettingsViewModel viewModel)
        {
            await viewModel.LoadSettingsAsync();
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is ChatSettingsViewModel viewModel)
        {
            await viewModel.SaveSettingsAsync();
        }
        
        base.OnClosing(e);
    }
}