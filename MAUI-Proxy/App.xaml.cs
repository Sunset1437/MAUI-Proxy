using MAUI_Proxy.ViewModels;

namespace MAUI_Proxy
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();

            // Регистрация зависимостей
            builder.Services.AddSingleton<ProxyViewModel>();
            builder.Services.AddTransient<MainPage>();

            var mauiApp = builder.Build();
            MainPage = new AppShell();
        }
    }
}
