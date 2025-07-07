using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Android.Content;
using Android.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_Proxy.Models;
using MAUI_Proxy.Platforms.Android.Services;

namespace MAUI_Proxy.ViewModels
{
    public partial class ProxyViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _status = "Готов для подключения";

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isConnecting;
        public ProxySettings Settings { get; } = new();

        [RelayCommand]
        private async Task ToggleConnection()
        {
            try
            {
                if (IsConnected)
                {
                    await Disconnect();
                    return;
                }

                IsConnecting = true;
                Status = "Запрос разрешения для впн";

#if ANDROID
                var context = Platform.CurrentActivity;
                var vpnIntent = VpnService.Prepare(context);

                if (vpnIntent != null)
                {
                    // Проверяем, что Shell инициализирован
                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync("//vpnpermission");
                    }
                    else
                    {
                        context.StartActivity(vpnIntent);
                    }
                    return;
                }

                var startIntent = ProxyVpnService.GetStartIntent(context, Settings);
                context.StartService(startIntent);

                IsConnected = true;
                Status = "Connected to proxy";
#endif
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }


        private async Task Connect()
        {
            IsConnecting = true;
            Status = "Запрос разрешения для VPN";
            
            try
            {
                var context = Platform.CurrentActivity;
                var vpnIntent = VpnService.Prepare(context);
                
                if (vpnIntent != null)
                {
                    await Shell.Current.GoToAsync("//vpnpermission");
                    var result = await Task.Run(() => 
                    {
                        context.StartActivity(vpnIntent);
                        return false; // В реальности нужно отслеживать результат
                    });
                    
                    if (!result)
                        return;
                }
                var startIntent = ProxyVpnService.GetStartIntent(context, Settings);
                context.StartService(startIntent);
                
                IsConnected = true;
                Status = "Connected to proxy";
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private async Task Disconnect()
        {
            try
            {
                var context = Platform.CurrentActivity;
                var stopIntent = new Intent(context, typeof(ProxyVpnService));
                context.StopService(stopIntent);
                
                IsConnected = false;
                Status = "Disconnected";
            }
            catch (Exception ex)
            {
                Status = $"Disconnect error: {ex.Message}";
            }
        }
    }

}
