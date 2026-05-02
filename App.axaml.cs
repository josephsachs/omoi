using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Omoi.Services;
using Omoi.Services.Agents;
using Omoi.Services.Agents.Quade;
using Omoi.ViewModels;
using Omoi.Views;

namespace Omoi;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var configService = new ConfigService();
            var credentialsService = new CredentialsService();
            var logger = new ThoughtProcessLogger();
            var openRouterClient = new OpenRouterClient(logger);
            var supabaseClient = new SupabaseClient();
            var qdrantClient = new QdrantClient();
            var conversationService = new ConversationService();

            var providerResolver = new ModelProviderResolver(openRouterClient);
            var vectorProviderResolver = new VectorProviderResolver(openRouterClient);
            var vectorStorageResolver = new VectorStorageResolver(supabaseClient, qdrantClient);

            var contextBuilder = new ChatContextBuilder(vectorProviderResolver, vectorStorageResolver, configService, logger);
            var modeDetector = new ModeDetector(providerResolver, logger, configService);

            var quadeAgent = new QuadeAgent(
                providerResolver,
                vectorProviderResolver,
                vectorStorageResolver,
                configService,
                logger,
                modeDetector,
                contextBuilder
            );
            var chatService = new ChatService(quadeAgent, conversationService, logger);

            var hasApiKey = await credentialsService.HasApiKeyAsync(CredentialsService.OPENROUTER);

            if (!hasApiKey)
            {
                var welcomeWindow = new WelcomeWindow();
                welcomeWindow.Show();
            }

            var openRouterKey = await credentialsService.GetApiKeyAsync(CredentialsService.OPENROUTER);
            if (!string.IsNullOrWhiteSpace(openRouterKey))
            {
                openRouterClient.SetApiKey(openRouterKey);
            }

            var appConfig = await configService.LoadConfigAsync();

            var supabaseKey = await credentialsService.GetApiKeyAsync(CredentialsService.SUPABASE);
            if (!string.IsNullOrWhiteSpace(supabaseKey) && !string.IsNullOrWhiteSpace(appConfig.SupabaseUrl))
            {
                supabaseClient.SetApiKey(supabaseKey, appConfig.SupabaseUrl);

                if (appConfig.SelectedVectorStorage == Omoi.Models.VectorStorageProvider.Supabase)
                {
                    try
                    {
                        await supabaseClient.EnsureReadyAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogInfo($"Failed to initialize Supabase: {ex.Message}");
                    }
                }
            }

            var qdrantKey = await credentialsService.GetApiKeyAsync(CredentialsService.QDRANT);
            if (!string.IsNullOrWhiteSpace(qdrantKey) && !string.IsNullOrWhiteSpace(appConfig.QdrantUrl))
            {
                qdrantClient.SetApiKey(qdrantKey, appConfig.QdrantUrl);

                if (appConfig.SelectedVectorStorage == Omoi.Models.VectorStorageProvider.Qdrant)
                {
                    try
                    {
                        await qdrantClient.EnsureReadyAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogInfo($"Failed to initialize Qdrant: {ex.Message}");
                    }
                }
            }

            var viewModel = new MainWindowViewModel(
                chatService,
                configService,
                openRouterClient,
                logger,
                conversationService,
                credentialsService);

            await viewModel.InitializeAsync();

            var config = await configService.LoadConfigAsync();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            bool hasCustomPosition = config.MainWindowX != 0 || config.MainWindowY != 0;
            bool hasCustomSize = config.MainWindowWidth > 0 && config.MainWindowHeight > 0;

            if (hasCustomPosition || hasCustomSize)
            {
                desktop.MainWindow.WindowStartupLocation = WindowStartupLocation.Manual;

                if (hasCustomSize)
                {
                    desktop.MainWindow.Width = config.MainWindowWidth;
                    desktop.MainWindow.Height = config.MainWindowHeight;
                }

                if (hasCustomPosition)
                {
                    desktop.MainWindow.Position = new PixelPoint((int)config.MainWindowX, (int)config.MainWindowY);
                }
            }

            desktop.MainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
