using Newtonsoft.Json;

namespace Scalar.Common.RepoRegistry
{
    public class ScalarRepoRegistration
    {
        public ScalarRepoRegistration()
        {
        }

        public ScalarRepoRegistration(string normalizedRepoRoot, string userId)
        {
            this.NormalizedRepoRoot = normalizedRepoRoot;
            this.UserId = userId;
        }

        public string NormalizedRepoRoot { get; set; }
        public string UserId { get; set; }

        public static ScalarRepoRegistration FromJson(string json)
        {
            return JsonConvert.DeserializeObject<ScalarRepoRegistration>(
                json,
                new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore
                });
        }

        public override string ToString()
        {
            return $"({this.UserId}) {this.NormalizedRepoRoot}";
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
