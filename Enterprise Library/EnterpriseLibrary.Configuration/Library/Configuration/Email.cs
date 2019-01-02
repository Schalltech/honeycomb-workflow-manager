using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Schalltech.EnterpriseLibrary.Configuration.Email
{
    public class EmailConfiguration
    {
        [XmlElement(ElementName = "DefaultFromAddress")]
        public string DefaultFromAddress { get; set; }

        [XmlElement(ElementName = "DefaultReplyToAddress")]
        public string DefaultReplyToAddress { get; set; }

        [XmlElement(ElementName = "Port")]
        public string Port { get; set; }

        [XmlElement(ElementName = "Servers")]
        public ServerCollection ServerCollection { get; set; }
    }

    public class ServerCollection
    {
        [XmlElement(ElementName = "Server")]
        public List<string> Items { get; set; }

        public int Count
        {
            get
            {
                if (Items != null)
                    return Items.Count;

                return 0;
            }
        }
    }
}
