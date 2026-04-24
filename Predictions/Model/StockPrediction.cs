using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Predictions.Model
{
    public class StockPrediction
    {

        [ColumnName("Score")]

        public float ValorPrevisto { get; set; }
    }
}
