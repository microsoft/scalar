using Newtonsoft.Json;

namespace Scalar.Common
{
    public class InternalVerbParameters
    {
        public InternalVerbParameters(
            string serviceName = null,
            bool startedByService = true)
        {
            this.ServiceName = serviceName;
            this.StartedByService = startedByService;
        }

        public string ServiceName { get; private set; }
        public bool StartedByService { get; private set; }

        public static InternalVerbParameters FromJson(string json)
        {
            return JsonConvert.DeserializeObject<InternalVerbParameters>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
