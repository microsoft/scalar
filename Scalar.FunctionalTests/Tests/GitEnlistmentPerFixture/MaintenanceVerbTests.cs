using NUnit.Framework;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.Text;

namespace Scalar.FunctionalTests.Tests.GitEnlistmentPerFixture
{
    [Category(Categories.GitRepository)]
    public class MaintenanceVerbTests : TestsWithGitEnlistmentPerFixture
    {
        [TestCase]
        [Order(1)]
        public void CommitGraphStep()
        {
            this.Enlistment.CommitGraphStep();
        }

        [TestCase]
        [Order(2)]
        public void PackfileMaintenanceStep()
        {
            this.Enlistment
                .PackfileMaintenanceStep()
                .ShouldNotContain(false, "Skipping pack maintenance due to no .keep file.");
        }

        [TestCase]
        [Order(3)]
        public void LooseObjectsStep()
        {
            this.Enlistment.LooseObjectStep();
        }
    }
}
