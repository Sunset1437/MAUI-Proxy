using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.OS;
using MAUI_Proxy.Models;

namespace MAUI_Proxy.Platforms.Android.Extensions
{
    public static class BundleExtensions
    {
        public static Bundle ToBundle(this ProxySettings settings)
        {
            var bundle = new Bundle();
            bundle.PutString("ip_address", settings.IpAddress);
            bundle.PutInt("port", settings.Port);
            bundle.PutString("username", settings.Username);
            bundle.PutString("password", settings.Password);
            bundle.PutStringArray("bypass_list", settings.BypassList.ToArray());
            return bundle;
        }
        public static ProxySettings FromBundle(this Bundle bundle)
        {
            return new ProxySettings
            {
                IpAddress = bundle.GetString("ip_address"),
                Port = bundle.GetInt("port"),
                Username = bundle.GetString("username"),
                Password = bundle.GetString("password"),
                BypassList = bundle.GetStringArray("bypass_list")?.ToList() ?? new()
            };
        }
    }
}
