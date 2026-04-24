using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Models
{
    [Table("fundamentals")]
    public class Fundamentals
    {
        [Key]
        public int id { get; set; }
        public DateTime date { get; set; }
        public decimal earnings { get; set; }
        public string balance_sheet { get; set; }
        public string cash_flow { get; set; }
    }
}
