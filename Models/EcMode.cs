using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ECController.Models
{
    public class EcMode
    {
        public string Name { get; set; }

        public List<EcItem> Items { get; set; } = new List<EcItem>();
    }
}