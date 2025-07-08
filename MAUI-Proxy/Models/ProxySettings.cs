using Android.OS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MAUI_Proxy.Models
{
    public class ProxySettings
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public List<string> BypassList { get; set; } = new List<string>
        {
            "https://speedtest.net" 
        }; // Фильтр по пакетам приложений
        

        public Bundle ToBundle()
        {
            var bundle = new Bundle();
            bundle.PutString("server_address", IpAddress);
            bundle.PutInt("port", Port);
            bundle.PutString("username", Username);
            bundle.PutString("password", Password);
            bundle.PutStringArrayList("bypass_list", BypassList.ToArray());
            return bundle;
        }

        public static ProxySettings FromBundle(Bundle bundle)
        {
            return new ProxySettings
            {
                IpAddress = bundle.GetString("server_address") ?? "proxy.example.com",
                Port = bundle.GetInt("port", 64186),
                Username = bundle.GetString("username") ?? "",
                Password = bundle.GetString("password") ?? "",
                BypassList = bundle.GetStringArrayList("bypass_list")?.ToList() ?? new List<string>()
            };
        }
    }
}
