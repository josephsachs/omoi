using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Omoi.Models;
using Omoi.Services;
using Omoi.ViewModels;

namespace Omoi.Views;

public partial class MainWindow : Window
{
    private ThoughtProcessWindow? _thoughtProcessWindow;

    public MainWindow()
    {
        InitializeComponent();
        
        this.Icon = new WindowIcon("icon.ico");

        Closing += OnWindowClosing;
        
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            viewModel.Messages.CollectionChanged += OnMessagesChanged;
            
            await viewModel.LoadAutoSaveAsync();
            ScrollToBottom();
            
            BuildModelMenu();

            var config = await viewModel.GetConfigAsync();
            if (config.ThoughtWindowWasOpen)
            {
                ShowThoughtProcess_Click(null, new RoutedEventArgs());
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedModelId) ||
            e.PropertyName == nameof(MainWindowViewModel.ThoughtModel) ||
            e.PropertyName == nameof(MainWindowViewModel.MemoryModel) ||
            e.PropertyName == nameof(MainWindowViewModel.VectorModel))
        {
            UpdateModelMenuCheckmarks();
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (Message msg in e.NewItems)
            {
                if (!msg.IsUser)
                {
                    ScrollToBottom();
                }
            }
        }
    }

    private async void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Message message)
        {
            if (DataContext is not MainWindowViewModel viewModel)
                return;

            if (message.IsEditing)
            {
                await viewModel.SubmitEditedMessageAsync(message);
            }
            else
            {
                viewModel.StartEditingMessage(message);
            }
        }
    }

    private async void BuildModelMenu()
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var modelMenu = this.FindControl<MenuItem>("ModelMenu");
        if (modelMenu == null)
            return;

        modelMenu.Items.Clear();

        var chatModels = new List<ModelInfo>();
        var thoughtModels = new List<ModelInfo>();
        var memoryModels = new List<ModelInfo>();
        var vectorModels = new List<ModelInfo>();

        foreach (var model in viewModel.AvailableModels)
        {
            if (model.Categories.Contains("chat"))
                chatModels.Add(model);
            if (model.Categories.Contains("thought"))
                thoughtModels.Add(model);
            if (model.Categories.Contains("memory"))
                memoryModels.Add(model);
            if (model.Categories.Contains("vector"))
                vectorModels.Add(model);
        }

        var chatMenuItem = new MenuItem { Header = "_Chat Models" };
        foreach (var model in chatModels)
        {
            var menuItem = new MenuItem
            {
                Header = model.DisplayName,
                Tag = model.Id
            };

            if (model.Id == viewModel.SelectedModelId)
            {
                menuItem.Icon = new TextBlock { Text = "✓" };
            }

            menuItem.Click += async (s, e) => await OnChatModelSelected(s, e);
            chatMenuItem.Items.Add(menuItem);
        }
        modelMenu.Items.Add(chatMenuItem);

        var thoughtMenuItem = new MenuItem { Header = "_Thought Models" };
        foreach (var model in thoughtModels)
        {
            var menuItem = new MenuItem
            {
                Header = model.DisplayName,
                Tag = model.Id
            };

            if (model.Id == viewModel.ThoughtModel)
            {
                menuItem.Icon = new TextBlock { Text = "✓" };
            }

            menuItem.Click += async (s, e) => await OnThoughtModelSelected(s, e);
            thoughtMenuItem.Items.Add(menuItem);
        }
        modelMenu.Items.Add(thoughtMenuItem);

        
        var memoryMenuItem = new MenuItem { Header = "_Memory Models" };
        foreach (var model in memoryModels)
        {
            var menuItem = new MenuItem
            {
                Header = model.DisplayName,
                Tag = model.Id
            };

            if (model.Id == viewModel.MemoryModel)
            {
                menuItem.Icon = new TextBlock { Text = "✓" };
            }

            menuItem.Click += async (s, e) => await OnMemoryModelSelected(s, e);
            memoryMenuItem.Items.Add(menuItem);
        }
        modelMenu.Items.Add(memoryMenuItem);

        var hasOpenRouterKey = await viewModel.CredentialsService.HasApiKeyAsync(CredentialsService.OPENROUTER);
        var vectorMenuItem = new MenuItem
        {
            Header = "_Vector Models",
            IsEnabled = hasOpenRouterKey
        };
        
        foreach (var model in vectorModels)
        {
            var menuItem = new MenuItem
            {
                Header = model.DisplayName,
                Tag = model.Id
            };

            if (model.Id == viewModel.VectorModel)
            {
                menuItem.Icon = new TextBlock { Text = "✓" };
            }

            menuItem.Click += async (s, e) => await OnVectorModelSelected(s, e);
            vectorMenuItem.Items.Add(menuItem);
        }
        modelMenu.Items.Add(vectorMenuItem);

        modelMenu.Items.Add(new Separator());

        var refreshItem = new MenuItem { Header = "_Refresh" };
        refreshItem.Click += async (s, e) => await OnRefreshModels(s, e);
        modelMenu.Items.Add(refreshItem);
    }

    private void UpdateModelMenuCheckmarks()
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var modelMenu = this.FindControl<MenuItem>("ModelMenu");
        if (modelMenu == null)
            return;

        foreach (var item in modelMenu.Items)
        {
            if (item is MenuItem subMenu && subMenu.Header is string header)
            {
                if (header == "_Chat Models")
                {
                    UpdateCheckmarksInSubmenu(subMenu, viewModel.SelectedModelId);
                }
                else if (header == "_Thought Models")
                {
                    UpdateCheckmarksInSubmenu(subMenu, viewModel.ThoughtModel);
                }
                else if (header == "_Memory Models")
                {
                    UpdateCheckmarksInSubmenu(subMenu, viewModel.MemoryModel);
                }
                else if (header == "_Vector Models")
                {
                    UpdateCheckmarksInSubmenu(subMenu, viewModel.VectorModel);
                }
            }
        }
    }

    private void UpdateCheckmarksInSubmenu(MenuItem subMenu, string selectedModelId)
    {
        foreach (var item in subMenu.Items)
        {
            if (item is MenuItem menuItem && menuItem.Tag is string modelId)
            {
                if (modelId == selectedModelId)
                {
                    menuItem.Icon = new TextBlock { Text = "✓" };
                }
                else
                {
                    menuItem.Icon = null;
                }
            }
        }
    }

    private async Task OnChatModelSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.Tag is string modelId &&
            DataContext is MainWindowViewModel viewModel)
        {
            try
            {
                await viewModel.SelectChatModelAsync(modelId);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Model Selection Error", ex.Message);
            }
        }
    }

    private async Task OnThoughtModelSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.Tag is string modelId &&
            DataContext is MainWindowViewModel viewModel)
        {
            try
            {
                await viewModel.SelectThoughtModelAsync(modelId);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Model Selection Error", ex.Message);
            }
        }
    }

    private async Task OnMemoryModelSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.Tag is string modelId &&
            DataContext is MainWindowViewModel viewModel)
        {
            try
            {
                await viewModel.SelectMemoryModelAsync(modelId);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Model Selection Error", ex.Message);
            }
        }
    }

    private async Task OnVectorModelSelected(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && 
            menuItem.Tag is string modelId &&
            DataContext is MainWindowViewModel viewModel)
        {
            try
            {
                await viewModel.SelectVectorModelAsync(modelId);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Model Selection Error", ex.Message);
            }
        }
    }

    private async Task OnRefreshModels(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            try
            {
                await viewModel.RefreshModelsAsync();
                BuildModelMenu();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Model Refresh Error", ex.Message);
            }
        }
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15
        };

        stack.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var okButton = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        okButton.Click += (s, e) => dialog.Close();
        stack.Children.Add(okButton);

        dialog.Content = stack;
        await dialog.ShowDialog(this);
    }

    private async void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Source == this.FindControl<TextBox>("InputTextBox") &&
            e.Key == Key.Enter && 
            !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            
            if (DataContext is MainWindowViewModel viewModel)
            {
                try
                {
                    viewModel.CanSendMessage();
                    
                    var messageText = viewModel.InputMessage;
                    viewModel.InputMessage = string.Empty;
                    
                    var success = await viewModel.TrySendMessageAsync(messageText);
                    
                    if (!success)
                    {
                        viewModel.InputMessage = messageText;
                    }
                    
                    ScrollToBottom();
                }
                catch (Exception ex)
                {
                    viewModel.ErrorMessage = ex.Message;
                }
            }
        }
    }

    private async void SendMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            try
            {
                viewModel.CanSendMessage();
                
                var messageText = viewModel.InputMessage;
                viewModel.InputMessage = string.Empty;
                
                var success = await viewModel.TrySendMessageAsync(messageText);
                
                if (!success)
                {
                    viewModel.InputMessage = messageText;
                }
                
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = ex.Message;
            }
        }
    }

    private async void NewConversation_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.NewConversationAsync();
        }
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var filepath = viewModel.GenerateTimestampedFilename();
            await viewModel.SaveConversationAsync(filepath);
        }
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var conversationsDir = viewModel.GetConversationsDirectory();
            var folder = await StorageProvider.TryGetFolderFromPathAsync(conversationsDir);
            
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Conversation",
                DefaultExtension = "json",
                SuggestedFileName = $"conversation_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json",
                SuggestedStartLocation = folder,
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("JSON Files") { Patterns = new[] { "*.json" } },
                    new("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (file != null)
            {
                await viewModel.SaveConversationAsync(file.Path.LocalPath);
            }
        }
    }

    private async void Load_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var conversationsDir = viewModel.GetConversationsDirectory();
            var folder = await StorageProvider.TryGetFolderFromPathAsync(conversationsDir);
            
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Conversation",
                AllowMultiple = false,
                SuggestedStartLocation = folder,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("JSON Files") { Patterns = new[] { "*.json" } },
                    new("All Files") { Patterns = new[] { "*" } }
                }
            });

            if (files.Count > 0)
            {
                await viewModel.LoadConversationAsync(files[0].Path.LocalPath);
                ScrollToBottom();
            }
        }
    }

    private async void ShowChatSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var chatSettingsWindow = new ChatSettingsWindow
            {
                DataContext = new ChatSettingsViewModel(viewModel.ConfigService)
            };
            
            await chatSettingsWindow.ShowDialog(this);
        }
    }

    private async void ShowSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            var settingsWindow = new SettingsWindow
            {
                DataContext = new SettingsWindowViewModel(
                    viewModel.CredentialsService,
                    viewModel.GetOpenRouterClient(),
                    viewModel.ConfigService)
            };
            
            await settingsWindow.ShowDialog(this);
        }
    }

    private void Quit_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!e.Cancel)
        {
            e.Cancel = true;
            await HandleQuitAsync();
            Closing -= OnWindowClosing;
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        }
    }

    private async Task HandleQuitAsync()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.AutoSaveAsync();
            
            double mainWidth = Bounds.Width;
            double mainHeight = Bounds.Height;
            int mainX = Position.X;
            int mainY = Position.Y;
            
            double thoughtWidth = _thoughtProcessWindow?.Bounds.Width ?? 700;
            double thoughtHeight = _thoughtProcessWindow?.Bounds.Height ?? 500;
            int thoughtX = _thoughtProcessWindow?.Position.X ?? 0;
            int thoughtY = _thoughtProcessWindow?.Position.Y ?? 0;
            bool thoughtWasOpen = _thoughtProcessWindow?.IsVisible ?? false;
            
            await viewModel.SaveWindowStateAsync(
                mainX,
                mainY,
                mainWidth,
                mainHeight,
                thoughtX,
                thoughtY,
                thoughtWidth,
                thoughtHeight,
                thoughtWasOpen
            );
        }
    }

    private async void ShowThoughtProcess_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            if (_thoughtProcessWindow == null)
            {
                _thoughtProcessWindow = new ThoughtProcessWindow
                {
                    DataContext = new ThoughtProcessViewModel(viewModel.Logger)
                };
                
                await RestoreThoughtProcessWindowStateAsync();
            }

            if (_thoughtProcessWindow.IsVisible)
            {
                _thoughtProcessWindow.Hide();
            }
            else
            {
                _thoughtProcessWindow.Show();
            }
        }
    }

    private async Task RestoreThoughtProcessWindowStateAsync()
    {
        if (_thoughtProcessWindow == null)
            return;

        if (DataContext is MainWindowViewModel viewModel)
        {
            var config = await viewModel.GetConfigAsync();
            
            if (config.ThoughtWindowWidth > 0 && config.ThoughtWindowHeight > 0)
            {
                _thoughtProcessWindow.Width = config.ThoughtWindowWidth;
                _thoughtProcessWindow.Height = config.ThoughtWindowHeight;
            }

            if (config.ThoughtWindowX != 0 || config.ThoughtWindowY != 0)
            {
                _thoughtProcessWindow.Position = new PixelPoint(
                    (int)config.ThoughtWindowX, 
                    (int)config.ThoughtWindowY);
            }
        }
    }

    private void ScrollToBottom()
    {
        if (this.FindControl<ScrollViewer>("MessageScrollViewer") is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToEnd();
        }
    }
}