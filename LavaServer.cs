using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;

namespace FoxyBot
{
    internal class LavaServer
    {
        public string? Host { get; set; }
        public ushort Port { get; set; }
        public string? Password { get; set; }
        public bool Secure { get; set; }
    }
}
