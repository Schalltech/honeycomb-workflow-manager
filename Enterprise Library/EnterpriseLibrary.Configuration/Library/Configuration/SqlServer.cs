using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Schalltech.EnterpriseLibrary.Configuration.SqlServer
{
    public class SqlServerConfiguration
    {
        [XmlElement(ElementName = "ConnectionString")]
        public string ConnectionString { get; set; }
    }
}
