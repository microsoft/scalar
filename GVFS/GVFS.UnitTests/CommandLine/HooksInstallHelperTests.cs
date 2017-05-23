﻿using GVFS.CommandLine;
using GVFS.Common;
using GVFS.Tests.Should;
using GVFS.UnitTests.Category;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GVFS.UnitTests.CommandLine
{
    [TestFixture]
    public class HooksInstallHelperTests
    {
        private const string Filename = "hooksfile";

        [TestCase]
        [Category(CategoryConstants.ExceptionExpected)]
        public void MergeHooksDataThrowsOnFoundGVFSHooks()
        {
            Assert.Throws<HooksInstallHelper.HooksConfigurationException>(
                () => HooksInstallHelper.MergeHooksData(new string[] { "first", "gvfs.hooks.exe" }, Filename, GVFSConstants.DotGit.Hooks.PreCommandHookName));
        }

        [TestCase]
        public void MergeHooksDataEmptyConfig()
        {
            string result = HooksInstallHelper.MergeHooksData(new string[] { }, Filename, GVFSConstants.DotGit.Hooks.PreCommandHookName);
            IEnumerable<string> resultLines = result
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("#"));

            resultLines.Single().ShouldEqual(GVFSConstants.GVFSHooksExecutableName);
        }

        [TestCase]
        public void MergeHooksDataPreCommandLast()
        {
            string result = HooksInstallHelper.MergeHooksData(new string[] { "first", "second" }, Filename, GVFSConstants.DotGit.Hooks.PreCommandHookName);
            IEnumerable<string> resultLines = result
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("#"));

            resultLines.Count().ShouldEqual(3);
            resultLines.ElementAt(0).ShouldEqual("first");
            resultLines.ElementAt(1).ShouldEqual("second");
            resultLines.ElementAt(2).ShouldEqual(GVFSConstants.GVFSHooksExecutableName);
        }

        [TestCase]
        public void MergeHooksDataPostCommandFirst()
        {
            string result = HooksInstallHelper.MergeHooksData(new string[] { "first", "second" }, Filename, GVFSConstants.DotGit.Hooks.PostCommandHookName);
            IEnumerable<string> resultLines = result
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("#"));

            resultLines.Count().ShouldEqual(3);
            resultLines.ElementAt(0).ShouldEqual(GVFSConstants.GVFSHooksExecutableName);
            resultLines.ElementAt(1).ShouldEqual("first");
            resultLines.ElementAt(2).ShouldEqual("second");
        }

        [TestCase]
        public void MergeHooksDataDiscardBlankLines()
        {
            string result = HooksInstallHelper.MergeHooksData(new string[] { "first", "second", string.Empty, " " }, Filename, GVFSConstants.DotGit.Hooks.PreCommandHookName);
            IEnumerable<string> resultLines = result
                .Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("#"));

            resultLines.Count().ShouldEqual(3);
            resultLines.ElementAt(0).ShouldEqual("first");
            resultLines.ElementAt(1).ShouldEqual("second");
            resultLines.ElementAt(2).ShouldEqual(GVFSConstants.GVFSHooksExecutableName);
        }
    }
}
