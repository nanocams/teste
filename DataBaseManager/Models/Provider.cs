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
    [Table("providers")]
    public class Provider
    {
        [Key]
        public int id { get; set; }
        public string name { get; set; }
        public string api_url { get; set; }
        public string auth_type { get; set; }
        [ForeignKey("provider_id")]
        public ICollection<Price> prices { get; set; }
        [ForeignKey("provider_id")]
        public ICollection<Fundamentals> fundamentals { get; set; }
        [ForeignKey("provider_id")]
        public ICollection<Indicators> indicators { get; set; } // test
    }
}
