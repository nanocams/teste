using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Models
{
    [Table("indicators")]
    public class Indicators
    {
        [Key]
        public int id { get; set; }
        public DateTime date { get; set; }
        public decimal sma { get; set; }
        public decimal rsi { get; set; }
        public decimal macd { get; set; }
    }
}
