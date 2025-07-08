using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Android.App;
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
        private string _status = "Готов к подключению";

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isConnecting;

        [ObservableProperty]
        private string _ipAddress;

        [ObservableProperty]
        private int _port = 1;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private string _password;

        public ProxySettings Settings => new()
        {
            IpAddress = IpAddress,
            Port = Port,
            Username = Username,
            Password = Password,
            BypassList = new List<string>()
        };

        [RelayCommand]
        private async Task ToggleConnection()
        {
            if (IsConnected)
            {
                await Disconnect();
                return;
            }

            await Connect();
        }

        private async Task Connect()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
            {
                Status = "Введите адрес сервера";
                return;
            }

            IsConnecting = true;
            Status = "Запрос разрешения для VPN";

            try
            {
#if ANDROID
                var context = Platform.CurrentActivity;
                if (context == null)
                {
                    Status = "Ошибка: контекст не найден";
                    return;
                }

                // Проверяем разрешение VPN
                var vpnIntent = VpnService.Prepare(context);
                if (vpnIntent != null)
                {
                    var result = await StartVpnPermissionActivity(context, vpnIntent);
                    if (!result)
                    {
                        Status = "Разрешение не получено";
                        return;
                    }
                }

                // Запускаем сервис
                var startIntent = ProxyVpnService.GetStartIntent(context, Settings);

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                {
                    context.StartForegroundService(startIntent);
                }
                else
                {
                    context.StartService(startIntent);
                }

                IsConnected = true;
                Status = $"Подключено к {IpAddress}";
#endif
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
            }
            finally
            {
                IsConnecting = false;
            }
        }

#if ANDROID
        private Task<bool> StartVpnPermissionActivity(Context context, Intent vpnIntent)
        {
            var tcs = new TaskCompletionSource<bool>();

            var activity = Platform.CurrentActivity as MainActivity;
            if (activity == null)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            activity.VpnPermissionResultHandler = (result) =>
            {
                tcs.SetResult(result == Result.Ok);
            };

            activity.StartActivityForResult(vpnIntent, 0);
            return tcs.Task;
        }
#endif

        private async Task Disconnect()
        {
            try
            {
#if ANDROID
                var context = Platform.CurrentActivity;
                if (context == null) return;

                var stopIntent = new Intent(context, typeof(ProxyVpnService));
                context.StopService(stopIntent);

                IsConnected = false;
                Status = "Отключено";
#endif
            }
            catch (Exception ex)
            {
                Status = $"Ошибка отключения: {ex.Message}";
            }
        }
    }

}
