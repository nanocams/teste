using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Models
{
    [Table("industries")]
    public class Industries
    {
        [Key]
        public int id { get; set; }
        public string description { get; set; }
        [ForeignKey("industry_id")]
        public ICollection<Stock> stocks { get; set; }
    }
}
