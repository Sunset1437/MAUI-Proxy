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
        public List<string> BypassList { get; set; } = new(); // Фильтр по пакетам приложений

    }

}
