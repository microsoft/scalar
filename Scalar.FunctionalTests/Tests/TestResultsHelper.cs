using Scalar.FunctionalTests.Tools;
using Scalar.Tests.Should;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scalar.FunctionalTests.Tests
{
    public static class TestResultsHelper
    {
        public static void OutputScalarLogs(ScalarFunctionalTestEnlistment enlistment)
        {
            if (enlistment == null)
            {
                return;
            }

            Console.WriteLine("Scalar logs output attached below.\n\n");

            foreach (string filename in GetAllFilesInDirectory(enlistment.ScalarLogsRoot))
            {
                if (filename.Contains("mount_process"))
                {
                    // Validate that all mount processes started by the functional tests were started
                    // by verbs, and that "StartedByVerb" was set to true when the mount process was launched
                    OutputFileContents(
                        filename,
                        contents => contents.ShouldContain("\"StartedByVerb\":true"));
                }
                else
                {
                    OutputFileContents(filename);
                }
            }
        }

        public static void OutputFileContents(string filename, Action<string> contentsValidator = null)
        {
            try
            {
                using (StreamReader reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    Console.WriteLine("----- {0} -----", filename);

                    string contents = reader.ReadToEnd();

                    if (contentsValidator != null)
                    {
                        contentsValidator(contents);
                    }

                    Console.WriteLine(contents + "\n\n");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("Unable to read logfile at {0}: {1}", filename, ex.ToString());
            }
        }

        public static IEnumerable<string> GetAllFilesInDirectory(string folderName)
        {
            DirectoryInfo directory = new DirectoryInfo(folderName);
            if (!directory.Exists)
            {
                return Enumerable.Empty<string>();
            }

            return directory.GetFiles().Select(file => file.FullName);
        }
    }
}
