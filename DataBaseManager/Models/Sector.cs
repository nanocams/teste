using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Models
{
    [Table("sectors")]
    public class Sector
    {
        [Key]
        public int id { get; set; }
        public string designation { get; set; }
        [ForeignKey("sector_id")]
        public ICollection<Stock> stocks { get; set; }
    }
}
