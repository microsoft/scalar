using NUnit.Framework;
using Scalar.FunctionalTests.Properties;
using Scalar.Tests.Should;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Scalar.FunctionalTests.FileSystemRunners
{
    public class BashRunner : ShellRunner
    {
        private static string[] fileNotFoundMessages = new string[]
        {
            "cannot stat",
            "cannot remove",
            "No such file or directory"
        };

        private static string[] invalidMovePathMessages = new string[]
        {
            "cannot move",
            "No such file or directory"
        };

        private static string[] moveDirectoryNotSupportedMessage = new string[]
        {
            "Function not implemented"
        };

        private static string[] windowsPermissionDeniedMessage = new string[]
        {
            "Permission denied"
        };

        private static string[] macPermissionDeniedMessage = new string[]
        {
            "Resource temporarily unavailable"
        };

        private readonly string pathToBash;

        public BashRunner()
        {
            if (File.Exists(Settings.Default.PathToBash))
            {
                this.pathToBash = Settings.Default.PathToBash;
            }
            else
            {
                this.pathToBash = "bash.exe";
            }
        }

        private enum FileType
        {
            Invalid,
            File,
            Directory,
            SymLink,
        }

        protected override string FileName
        {
            get
            {
                return this.pathToBash;
            }
        }

        public static void DeleteDirectoryWithLimitedRetries(string path, int maxRetries = 10)
        {
            BashRunner runner = new BashRunner();
            bool pathExists = Directory.Exists(path);
            int retryCount = 0;
            while (pathExists && maxRetries-- > 0)
            {
                string output = runner.DeleteDirectory(path);
                pathExists = Directory.Exists(path);
                if (pathExists)
                {
                    ++retryCount;
                    Thread.Sleep(500);
                    if (retryCount > 10)
                    {
                        retryCount = 0;
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }
                    }
                }
            }
        }

        public bool IsSymbolicLink(string path)
        {
            return this.FileExistsOnDisk(path, FileType.SymLink);
        }

        public void CreateSymbolicLink(string newLinkFilePath, string existingFilePath)
        {
            string existingFileBashPath = this.ConvertWinPathToBashPath(existingFilePath);
            string newLinkBashPath = this.ConvertWinPathToBashPath(newLinkFilePath);

            this.RunProcess(string.Format("-c \"ln -s -F '{0}' '{1}'\"", existingFileBashPath, newLinkBashPath));
        }

        public override bool FileExists(string path)
        {
            return this.FileExistsOnDisk(path, FileType.File);
        }

        public override string MoveFile(string sourcePath, string targetPath)
        {
            string sourceBashPath = this.ConvertWinPathToBashPath(sourcePath);
            string targetBashPath = this.ConvertWinPathToBashPath(targetPath);

            return this.RunProcess(string.Format("-c \"mv '{0}' '{1}'\"", sourceBashPath, targetBashPath));
        }

        public override string ReplaceFile(string sourcePath, string targetPath)
        {
            string sourceBashPath = this.ConvertWinPathToBashPath(sourcePath);
            string targetBashPath = this.ConvertWinPathToBashPath(targetPath);

            return this.RunProcess(string.Format("-c \"mv -f '{0}' '{1}'\"", sourceBashPath, targetBashPath));
        }

        public override string DeleteFile(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            return this.RunProcess(string.Format("-c \"rm '{0}'\"", bashPath));
        }

        public override string ReadAllText(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);
            string output = this.RunProcess(string.Format("-c \"cat '{0}'\"", bashPath));

            // Bash sometimes sticks a trailing "\n" at the end of the output that we need to remove
            // Until we can figure out why we cannot use this runner with files that have trailing newlines
            if (output.Length > 0 &&
                output.Substring(output.Length - 1).Equals("\n", StringComparison.InvariantCultureIgnoreCase) &&
                !(output.Length > 1 &&
                  output.Substring(output.Length - 2).Equals("\r\n", StringComparison.InvariantCultureIgnoreCase)))
            {
                output = output.Remove(output.Length - 1, 1);
            }

            return output;
        }

        public override void AppendAllText(string path, string contents)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            this.RunProcess(string.Format("-c \"echo -n \\\"{0}\\\" >> '{1}'\"", contents, bashPath));
        }

        public override void CreateEmptyFile(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            this.RunProcess(string.Format("-c \"touch '{0}'\"", bashPath));
        }

        public override void CreateHardLink(string newLinkFilePath, string existingFilePath)
        {
            string existingFileBashPath = this.ConvertWinPathToBashPath(existingFilePath);
            string newLinkBashPath = this.ConvertWinPathToBashPath(newLinkFilePath);

            this.RunProcess(string.Format("-c \"ln '{0}' '{1}'\"", existingFileBashPath, newLinkBashPath));
        }

        public override void WriteAllText(string path, string contents)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            this.RunProcess(string.Format("-c \"echo \\\"{0}\\\" > '{1}'\"", contents, bashPath));
        }

        public override bool DirectoryExists(string path)
        {
            return this.FileExistsOnDisk(path, FileType.Directory);
        }

        public override void MoveDirectory(string sourcePath, string targetPath)
        {
            this.MoveFile(sourcePath, targetPath);
        }

        public override void RenameDirectory(string workingDirectory, string source, string target)
        {
            this.MoveDirectory(Path.Combine(workingDirectory, source), Path.Combine(workingDirectory, target));
        }

        public override void CreateDirectory(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            this.RunProcess(string.Format("-c \"mkdir '{0}'\"", bashPath));
        }

        public override string DeleteDirectory(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            return this.RunProcess(string.Format("-c \"rm -rf '{0}'\"", bashPath));
        }

        public override string EnumerateDirectory(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            return this.RunProcess(string.Format("-c \"ls '{0}'\"", bashPath));
        }

        public override void ChangeMode(string path, ushort mode)
        {
            string octalMode = Convert.ToString(mode, 8);
            string bashPath = this.ConvertWinPathToBashPath(path);
            string command = $"-c \"chmod {octalMode} '{bashPath}'\"";
            this.RunProcess(command);
        }

        public override long FileSize(string path)
        {
            string bashPath = this.ConvertWinPathToBashPath(path);

            string statCommand = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                statCommand = string.Format("-c \"stat -f \"%z\" '{0}'\"", bashPath);
            }
            else
            {
                statCommand = string.Format("-c \"stat --format \"%s\" '{0}'\"", bashPath);
            }

            return long.Parse(this.RunProcess(statCommand));
        }

        public override IDisposable OpenFileAndWrite(string path, string data)
        {
            throw new NotImplementedException();
        }

        private bool FileExistsOnDisk(string path, FileType type)
        {
            string checkArgument = string.Empty;
            switch (type)
            {
                case FileType.File:
                    checkArgument = "-f";
                    break;
                case FileType.Directory:
                    checkArgument = "-d";
                    break;
                case FileType.SymLink:
                    checkArgument = "-h";
                    break;
                default:
                    Assert.Fail($"{nameof(this.FileExistsOnDisk)} does not support {nameof(FileType)} {type}");
                    break;
            }

            string bashPath = this.ConvertWinPathToBashPath(path);
            string command = $"-c  \"[ {checkArgument} '{bashPath}' ] && echo {ShellRunner.SuccessOutput} || echo {ShellRunner.FailureOutput}\"";
            string output = this.RunProcess(command).Trim();
            return output.Equals(ShellRunner.SuccessOutput, StringComparison.InvariantCulture);
        }

        private string ConvertWinPathToBashPath(string winPath)
        {
            string bashPath = string.Concat("/", winPath);
            bashPath = bashPath.Replace(":\\", "/");
            bashPath = bashPath.Replace('\\', '/');
            return bashPath;
        }
    }
}
