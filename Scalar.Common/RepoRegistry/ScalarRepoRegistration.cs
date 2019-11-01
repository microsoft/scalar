using Newtonsoft.Json;

namespace Scalar.Common.RepoRegistry
{
    public class ScalarRepoRegistration
    {
        public ScalarRepoRegistration()
        {
        }

        public ScalarRepoRegistration(string enlistmentRoot, string ownerSID)
        {
            this.EnlistmentRoot = enlistmentRoot;
            this.OwnerSID = ownerSID;
        }

        public string EnlistmentRoot { get; set; }
        public string OwnerSID { get; set; }

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
            return $"({this.OwnerSID}) {this.EnlistmentRoot}";
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
