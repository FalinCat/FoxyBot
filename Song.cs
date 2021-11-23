using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FoxyBot
{
    public class Song
    {
        public Song(string link, string path)
        {
            Link = link;
            Path = path;
        }

        public string Link { get; set; }
        public string Path { get; set; }
    }
}
