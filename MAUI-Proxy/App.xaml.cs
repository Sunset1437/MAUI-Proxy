using MAUI_Proxy.ViewModels;

namespace MAUI_Proxy
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            var mainPage = new MainPage();
            MainPage = new NavigationPage(mainPage);

            // Отложим установку BindingContext до момента, когда MauiContext будет доступен
            this.HandlerChanged += OnHandlerChanged;
        }

        private void OnHandlerChanged(object sender, EventArgs e)
        {
            if (this.Handler != null && this.Handler.MauiContext != null)
            {
                var mainPage = (MainPage)((NavigationPage)MainPage).CurrentPage;
                mainPage.BindingContext = this.Handler.MauiContext.Services.GetService<ProxyViewModel>();
                this.HandlerChanged -= OnHandlerChanged; // Отписываемся после использования
            }
        }
    }
}
