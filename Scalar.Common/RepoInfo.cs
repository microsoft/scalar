namespace Scalar.Common
{
    // This is the subset of data returned by /vsts/info that Scalar needs
    public class RepoInfo
    {
        public string serverUrl { get; set; }
        public Repository repository { get; set; }

        public class Repository
        {
            public string id { get; set; }
            public string name { get; set; }
            public string remoteUrl { get; set; }
        }
    }
}
