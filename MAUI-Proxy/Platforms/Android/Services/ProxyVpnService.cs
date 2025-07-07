using Android.App;
using Android.Content;
using Android.Net;
using Android.Util;
using Android.OS;
using Android.Runtime;
using Java.Net;
using MAUI_Proxy.Models;
using MAUI_Proxy.Platforms.Android.Extensions;
using System.Net;
using System.Net.Sockets;

namespace MAUI_Proxy.Platforms.Android.Services
{
    [Service(Name = "MAUI_Proxy.Platforms.Android.Services.ProxyVpnService", Exported = true, Permission = "android.permission.BIND_VPN_SERVICE")]
    [IntentFilter(new[] { "android.net.VpnService" })]
    public class ProxyVpnService : VpnService
    {
        private const string Tag = "ProxyVpnService";
        private const string VpnInterfaceName = "ProxyVPN";
        private const int Mtu = 1500;

        private ParcelFileDescriptor _vpnInterface;
        private Thread _proxyThread;
        private ProxySettings _settings;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            _settings = intent.Extras.FromBundle();

            var builder = new Builder(this)
                .SetSession(VpnInterfaceName)
                .SetMtu(Mtu)
                .AddAddress("10.8.0.2", 24)
                .AddDnsServer("8.8.8.8");

            foreach (var app in _settings.BypassList)
            {
                builder.AddAllowedApplication(app);
            }
            _vpnInterface = builder.Establish();

            _proxyThread = new Thread(RunProxy)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.Highest
            };
            _proxyThread.Start();

            return StartCommandResult.Sticky;
        }

        private void RunProxy()
        {
            try
            {
                // TCP/UDP проксирование
                // Чтение/запись через _vpnInterface.FileDescriptor
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Proxy error: {ex}");
            }
        }

        public override void OnDestroy()
        {
            _proxyThread?.Interrupt();
            _vpnInterface?.Close();
            base.OnDestroy();
        }

        public static Intent GetStartIntent(Context context, ProxySettings settings)
        {
            var intent = new Intent(context, typeof(ProxyVpnService));
            intent.PutExtras(settings.ToBundle());
            return intent;
        }
    }
}

