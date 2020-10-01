using NUnit.Framework;
using Scalar.Tests.Should;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Scalar.FunctionalTests.FileSystemRunners
{
    public class SystemIORunner : FileSystemRunner
    {
        public override bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public override string MoveFile(string sourcePath, string targetPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.Move(sourcePath, targetPath);
            }
            else
            {
                // Use rename(2) on POSIX instead of separate link(2)/unlink(2)
                // calls, which File.Move() uses to avoid overwriting the
                // target file should it exist.  However, using link(2)
                // results in unexpected missed event notifications from
                // Watchman to Git's fsmonitor hook on some macOS versions,
                // which in turn results in test failures for Scalar.
                Rename(sourcePath, targetPath);
            }
            return string.Empty;
        }

        public override IDisposable OpenFileAndWrite(string path, string content)
        {
            StreamWriter file = new StreamWriter(path);
            file.Write(content);
            return file;
        }

        public override string ReplaceFile(string sourcePath, string targetPath)
        {
            File.Replace(sourcePath, targetPath, null);
            return string.Empty;
        }

        public override string DeleteFile(string path)
        {
            File.Delete(path);
            return string.Empty;
        }

        public override string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public override void CreateEmptyFile(string path)
        {
            using (FileStream fs = File.Create(path))
            {
            }
        }

        public override void CreateHardLink(string newLinkFilePath, string existingFilePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsCreateHardLink(newLinkFilePath, existingFilePath, IntPtr.Zero).ShouldBeTrue($"Failed to create hard link: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                POSIXCreateHardLink(existingFilePath, newLinkFilePath).ShouldEqual(0, $"Failed to create hard link: {Marshal.GetLastWin32Error()}");
            }
        }

        public override void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public override void AppendAllText(string path, string contents)
        {
            File.AppendAllText(path, contents);
        }

        public override bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public override void MoveDirectory(string sourcePath, string targetPath)
        {
            Directory.Move(sourcePath, targetPath);
        }

        public override void RenameDirectory(string workingDirectory, string source, string target)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                MoveFileEx(Path.Combine(workingDirectory, source), Path.Combine(workingDirectory, target), 0);
            }
            else
            {
                Rename(Path.Combine(workingDirectory, source), Path.Combine(workingDirectory, target));
            }
        }

        public override void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public override string DeleteDirectory(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);

            foreach (FileInfo file in directory.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;

                RetryOnException(() => file.Delete());
            }

            foreach (DirectoryInfo subDirectory in directory.GetDirectories())
            {
                this.DeleteDirectory(subDirectory.FullName);
            }

            RetryOnException(() => directory.Delete());
            return string.Empty;
        }

        public override string EnumerateDirectory(string path)
        {
            return string.Join(Environment.NewLine, Directory.GetFileSystemEntries(path));
        }

        public override void ChangeMode(string path, ushort mode)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                LinuxChmod(path, (uint)mode).ShouldEqual(0, $"Failed to chmod: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                MacChmod(path, mode).ShouldEqual(0, $"Failed to chmod: {Marshal.GetLastWin32Error()}");
            }
        }

        public override long FileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

        [DllImport("libc", EntryPoint = "link", SetLastError = true)]
        private static extern int POSIXCreateHardLink(string oldPath, string newPath);

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int LinuxChmod(string pathname, uint mode);

        [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int MacChmod(string pathname, ushort mode);

        [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
        private static extern int Rename(string oldPath, string newPath);

        [DllImport("kernel32.dll", EntryPoint = "CreateHardLink", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WindowsCreateHardLink(
            string newLinkFileName,
            string existingFileName,
            IntPtr securityAttributes);

        private static void RetryOnException(Action action)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Failed to perform action with inner exceptions:");
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException e)
                {
                    Thread.Sleep(500);
                    message.AppendLine(e.Message);
                }
                catch (UnauthorizedAccessException e)
                {
                    Thread.Sleep(500);
                    message.AppendLine(e.Message);
                }
            }

            throw new Exception(message.ToString());
        }
    }
}
