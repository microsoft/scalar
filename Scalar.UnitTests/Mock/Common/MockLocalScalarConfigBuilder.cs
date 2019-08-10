using Scalar.Common;
using System.Collections.Generic;

namespace Scalar.UnitTests.Mock.Common
{
    public class MockLocalScalarConfigBuilder
    {
        private string defaultRing;
        private string defaultUpgradeFeedUrl;
        private string defaultUpgradeFeedPackageName;
        private string defaultOrgServerUrl;

        private Dictionary<string, string> entries;

        public MockLocalScalarConfigBuilder(
            string defaultRing,
            string defaultUpgradeFeedUrl,
            string defaultUpgradeFeedPackageName,
            string defaultOrgServerUrl)
        {
            this.defaultRing = defaultRing;
            this.defaultUpgradeFeedUrl = defaultUpgradeFeedUrl;
            this.defaultUpgradeFeedPackageName = defaultUpgradeFeedPackageName;
            this.defaultOrgServerUrl = defaultOrgServerUrl;
            this.entries = new Dictionary<string, string>();
        }

        public MockLocalScalarConfigBuilder WithUpgradeRing(string value = null)
        {
            return this.With(ScalarConstants.LocalScalarConfig.UpgradeRing, value ?? this.defaultRing);
        }

        public MockLocalScalarConfigBuilder WithNoUpgradeRing()
        {
            return this.WithNo(ScalarConstants.LocalScalarConfig.UpgradeRing);
        }

        public MockLocalScalarConfigBuilder WithUpgradeFeedPackageName(string value = null)
        {
            return this.With(ScalarConstants.LocalScalarConfig.UpgradeFeedPackageName, value ?? this.defaultUpgradeFeedPackageName);
        }

        public MockLocalScalarConfigBuilder WithNoUpgradeFeedPackageName()
        {
            return this.WithNo(ScalarConstants.LocalScalarConfig.UpgradeFeedPackageName);
        }

        public MockLocalScalarConfigBuilder WithUpgradeFeedUrl(string value = null)
        {
            return this.With(ScalarConstants.LocalScalarConfig.UpgradeFeedUrl, value ?? this.defaultUpgradeFeedUrl);
        }

        public MockLocalScalarConfigBuilder WithNoUpgradeFeedUrl()
        {
            return this.WithNo(ScalarConstants.LocalScalarConfig.UpgradeFeedUrl);
        }

        public MockLocalScalarConfigBuilder WithOrgInfoServerUrl(string value = null)
        {
            return this.With(ScalarConstants.LocalScalarConfig.OrgInfoServerUrl, value ?? this.defaultUpgradeFeedUrl);
        }

        public MockLocalScalarConfigBuilder WithNoOrgInfoServerUrl()
        {
            return this.WithNo(ScalarConstants.LocalScalarConfig.OrgInfoServerUrl);
        }

        public MockLocalScalarConfig Build()
        {
            MockLocalScalarConfig scalarConfig = new MockLocalScalarConfig();
            foreach (KeyValuePair<string, string> kvp in this.entries)
            {
                scalarConfig.TrySetConfig(kvp.Key, kvp.Value, out _);
            }

            return scalarConfig;
        }

        private MockLocalScalarConfigBuilder With(string key, string value)
        {
            this.entries.Add(key, value);
            return this;
        }

        private MockLocalScalarConfigBuilder WithNo(string key)
        {
            this.entries.Remove(key);
            return this;
        }
    }
}
