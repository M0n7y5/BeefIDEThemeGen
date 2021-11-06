using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeefIDEThemeGen
{
    public class FilterConfig
    {
        public int SharpeningSize { get; set; }

        public double SharpeningSigma { get; set; }

        public List<string> ApplyFilterOn { get; set; }
    }
}