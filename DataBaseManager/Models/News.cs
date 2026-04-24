using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataBaseManager.Models
{
    [Table("news")]
    public class News
    {
        [Key]
        public int id { get; set; }
        public DateTime date { get; set; }
        public string headline { get; set; }
        public decimal sentiment { get; set; }
        public string source { get; set; }

    }
}
