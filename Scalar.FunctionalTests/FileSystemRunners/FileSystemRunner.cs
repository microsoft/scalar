using NUnit.Framework;
using System;

namespace Scalar.FunctionalTests.FileSystemRunners
{
    public abstract class FileSystemRunner
    {
        private static FileSystemRunner defaultRunner = new SystemIORunner();

        public static object[] AllWindowsRunners { get; } =
            new[]
            {
                new object[] { new SystemIORunner() },
                new object[] { new CmdRunner() },
                new object[] { new PowerShellRunner() },
                new object[] { new BashRunner() },
            };

        public static object[] AllPOSIXRunners { get; } =
            new[]
            {
                new object[] { new SystemIORunner() },
                new object[] { new BashRunner() },
            };

        public static object[] DefaultRunners { get; } =
            new[]
            {
                new object[] { defaultRunner }
            };

        public static object[] Runners
        {
            get { return ScalarTestConfig.FileSystemRunners; }
        }

        /// <summary>
        /// Default runner to use (for tests that do not need to be run with multiple runners)
        /// </summary>
        public static FileSystemRunner DefaultRunner
        {
            get { return defaultRunner; }
        }

        // File methods
        public abstract bool FileExists(string path);
        public abstract string MoveFile(string sourcePath, string targetPath);

        public abstract string ReplaceFile(string sourcePath, string targetPath);
        public abstract string DeleteFile(string path);
        public abstract string ReadAllText(string path);

        public abstract void CreateEmptyFile(string path);
        public abstract void CreateHardLink(string newLinkFilePath, string existingFilePath);
        public abstract void ChangeMode(string path, ushort mode);

        /// <summary>
        /// Write the specified contents to the specified file.  By calling this method the caller is
        /// indicating that they expect the write to succeed. However, the caller is responsible for verifying that
        /// the write succeeded.
        /// </summary>
        /// <param name="path">Path to file</param>
        /// <param name="contents">File contents</param>
        public abstract void WriteAllText(string path, string contents);
        public abstract IDisposable OpenFileAndWrite(string path, string data);

        /// <summary>
        /// Append the specified contents to the specified file.  By calling this method the caller is
        /// indicating that they expect the write to succeed. However, the caller is responsible for verifying that
        /// the write succeeded.
        /// </summary>
        /// <param name="path">Path to file</param>
        /// <param name="contents">File contents</param>
        public abstract void AppendAllText(string path, string contents);

        // Directory methods
        public abstract bool DirectoryExists(string path);
        public abstract void MoveDirectory(string sourcePath, string targetPath);
        public abstract void RenameDirectory(string workingDirectory, string source, string target);
        public abstract void CreateDirectory(string path);
        public abstract string EnumerateDirectory(string path);
        public abstract long FileSize(string path);

        /// <summary>
        /// A recursive delete of a directory
        /// </summary>
        public abstract string DeleteDirectory(string path);
    }
}
