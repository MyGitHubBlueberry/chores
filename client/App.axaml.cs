using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using client.ViewModels;
using client.Views;
using Networking;
using Microsoft.Extensions.DependencyInjection;
namespace client;

public partial class App : Application
{
    public new static App Current => (App)Application.Current!;
    public IServiceProvider? Services { get; private set; }
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<Client>();
        collection.AddSingleton<UserSessionStore>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<AuthViewModel>();
        collection.AddSingleton<MyChoresViewModel>();
        collection.AddSingleton<ConnectionViewModel>();
        collection.AddSingleton<HomeViewModel>();
        collection.AddSingleton<CreateChoreViewModel>();
        collection.AddSingleton<ChoreSettingsViewModel>();
        collection.AddSingleton<CreateChorePopupViewModel>();
        collection.AddTransient<ChoreManagementViewModel>();
        Services = collection.BuildServiceProvider();
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var mainViewModel = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}