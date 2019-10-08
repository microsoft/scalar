using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Scalar.MSBuild
{
    public class GetGitHubReleaseAssets : Task
    {
        private const string GitHubApiBase = "https://api.github.com/";

        [Required]
        public string Repository { get; set; }

        [Required]
        public string Version { get; set; }

        public string Filter { get; set; }

        [Output]
        public ITaskItem[] Assets { get; set; }

        public override bool Execute()
        {
            var assetItems = new List<ITaskItem>();

            ReleaseInfo releaseInfo;

            try
            {
                this.Log.LogMessage(MessageImportance.Normal, $"Getting release information for '{this.Repository}' (tag: '{this.Version}')");
                releaseInfo = GetReleaseInfo(this.Repository, this.Version);
            }
            catch (Exception ex)
            {
                var errorMessage = new StringBuilder();
                errorMessage.AppendLine($"Failed to retrieve release information for '{this.Repository}' (tag: {this.Version}):");
                errorMessage.AppendLine(ex.ToString());

                if (ex is WebException wex)
                {
                    using (var rs = wex.Response.GetResponseStream())
                    {
                        if (rs != null)
                        {
                            using (var reader = new StreamReader(rs))
                            {
                                errorMessage.AppendLine("Response:");
                                errorMessage.Append(reader.ReadToEnd());
                            }
                        }
                    }
                }

                this.Log.LogError(errorMessage.ToString());
                return false;
            }

            this.Log.LogMessage(MessageImportance.Low, $"Release contains {releaseInfo.Assets.Length} assets:");

            if (!string.IsNullOrWhiteSpace(this.Filter))
            {
                this.Log.LogMessage(MessageImportance.Low, $"(Filtering assets with pattern: '{this.Filter}')");
            }

            foreach (AssetInfo asset in releaseInfo.Assets)
            {
                if (!string.IsNullOrWhiteSpace(this.Filter) && !Regex.IsMatch(asset.Name, this.Filter))
                {
                    this.Log.LogMessage(MessageImportance.Low, $" [skipped] {asset.Name} ({asset.Size} bytes)");
                }
                else
                {
                    this.Log.LogMessage(MessageImportance.Low, $" [include] {asset.Name} ({asset.Size} bytes)");

                    var item = new TaskItem(asset.Name, new Dictionary<string, string>
                    {
                        ["Repository"] = this.Repository,
                        ["Version"] = this.Version,
                        ["Size"] = asset.Size.ToString(),
                        ["Url"] = asset.DownloadUrl
                    });

                    assetItems.Add(item);
                }
            }

            this.Assets = assetItems.ToArray();
            return true;
        }

        private ReleaseInfo GetReleaseInfo(string repository, string version)
        {
            var client = new WebClient
            {
                Headers = new WebHeaderCollection
                {
                    {HttpRequestHeader.UserAgent, "MSBuild"},
                    {HttpRequestHeader.Accept, "application/json"}
                }
            };

            Uri releaseTagUri = CreateApiUri($"repos/{repository}/releases/tags/{version}");

            this.Log.LogMessage(MessageImportance.Low, $"GET {releaseTagUri}");
            string releaseJson = client.DownloadString(releaseTagUri);

            this.Log.LogMessage(MessageImportance.Low, $"Response: {releaseJson}");
            return Deserialize<ReleaseInfo>(releaseJson);
        }

        private static Uri CreateApiUri(string uriStem)
        {
            return new Uri(new Uri(GitHubApiBase), uriStem);
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
            {
                DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ssZ")
            });

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var ms = new MemoryStream(bytes))
            {
                return (T)serializer.ReadObject(ms);
            }
        }

#pragma warning disable CS0649
        [DataContract]
        private class ReleaseInfo
        {
            [DataMember(Name = "assets")]
            public AssetInfo[] Assets;
        }

        [DataContract]
        private class AssetInfo
        {
            [DataMember(Name = "name")]
            public string Name;

            [DataMember(Name = "browser_download_url")]
            public string DownloadUrl;

            [DataMember(Name = "size")]
            public int Size;
        }
#pragma warning restore CS0649
    }
}
