using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Omoi.Services;
using Omoi.Services.Agents;
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
            var anthropicClient = new AnthropicClient();
            var openAiClient = new OpenAiClient();
            var deepSeekClient = new DeepSeekClient();
            var supabaseClient = new SupabaseClient();
            var qdrantClient = new QdrantClient();
            var logger = new ThoughtProcessLogger();
            var conversationService = new ConversationService();
            
            var providerResolver = new ModelProviderResolver(anthropicClient, openAiClient, deepSeekClient);
            var vectorProviderResolver = new VectorProviderResolver(openAiClient);
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

            var hasApiKey = await credentialsService.HasApiKeyAsync(CredentialsService.ANTHROPIC);
            
            if (!hasApiKey)
            {
                var welcomeWindow = new WelcomeWindow();
                welcomeWindow.Show();
            }

            var anthropicKey = await credentialsService.GetApiKeyAsync(CredentialsService.ANTHROPIC);
            if (!string.IsNullOrWhiteSpace(anthropicKey))
            {
                anthropicClient.SetApiKey(anthropicKey);
            }

            var openAiKey = await credentialsService.GetApiKeyAsync(CredentialsService.OPENAI);
            if (!string.IsNullOrWhiteSpace(openAiKey))
            {
                openAiClient.SetApiKey(openAiKey);
            }

            var deepSeekKey = await credentialsService.GetApiKeyAsync(CredentialsService.DEEPSEEK);
            if (!string.IsNullOrWhiteSpace(deepSeekKey))
            {
                deepSeekClient.SetApiKey(deepSeekKey);
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
                anthropicClient,
                openAiClient,
                deepSeekClient,
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