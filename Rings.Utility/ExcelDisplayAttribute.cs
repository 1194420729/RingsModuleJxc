using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Jxc.Utility
{
    [System.AttributeUsage(System.AttributeTargets.All )]
    public class ExcelDisplayAttribute : Attribute
    {
        private string name = "";
        public ExcelDisplayAttribute(string name)
        {
            this.name=name;
        }

        public string GetName()
        {
            return name;
        }
    }
}
