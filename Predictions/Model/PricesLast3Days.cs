using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Predictions.Model
{
    public class PricesLast3Days
    {
        public float PriceT3 { get; set; }
        public float PriceT2 { get; set; }
        public float PriceT1 { get; set; }
        public float PriceT { get; set; } // Label
    }
}
