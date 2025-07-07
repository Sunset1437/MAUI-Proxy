#if ANDROID
using Android.OS;
using MAUI_Proxy.Models;

namespace MAUI_Proxy.Extensions
{

    public static class ProxyExtensions
    {
        public static Bundle ToBundle(this ProxySettings settings)
        {
            var bundle = new Bundle();
            bundle.PutString("IpAddress", settings.IpAddress);
            bundle.PutInt("Port", settings.Port);
            bundle.PutString("Username", settings.Username);
            bundle.PutString("Password", settings.Password);
            return bundle;
        }
    }
}
#endif

