using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECController.Models
{
    public class EcGroup
    {
        public string Name { get; set; }

        public List<EcMode> Modes { get; set; } = new List<EcMode>();
    }
}