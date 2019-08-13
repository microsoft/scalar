using System.Xml.Serialization;

namespace Scalar.Service.UI.Data
{
    public class VisualData
    {
        [XmlElement("binding")]
        public BindingData Binding { get; set; }
    }
}
