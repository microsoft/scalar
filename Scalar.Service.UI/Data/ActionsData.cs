using System.Xml.Serialization;

namespace Scalar.Service.UI.Data
{
    public class ActionsData
    {
        [XmlAnyElement("actions")]
        public XmlList<ActionItem> Actions { get; set; }
    }
}
