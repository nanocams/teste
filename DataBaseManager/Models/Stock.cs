using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Models
{
    [Table("stocks")]
    public class Stock
    {
        [Key]
        public int id { get; set; }
        public string symbol { get; set; }
        public string name { get; set; }
        [ForeignKey("stock_id")]
        public ICollection<Price> prices { get; set; }
        [ForeignKey("stock_id")]
        public ICollection<Fundamentals> fundamentals { get; set; }
        [ForeignKey("stock_id")]
        public ICollection<News> news { get; set; }
        [ForeignKey("stock_id")]
        public ICollection<Indicators> indicators { get; set; }
    }
}
