using Newtonsoft.Json;

namespace Scalar.Common
{
    // This is the subset of data returned by /vsts/info that Scalar needs
    public class VstsInfoData
    {
        [JsonProperty("serverUrl")]
        public string ServerUrl { get; set; }

        [JsonProperty("repository")]
        public RepositoryDetails Repository { get; set; }

        public class RepositoryDetails
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("remoteUrl")]
            public string RemoteUrl { get; set; }
        }
    }
}
