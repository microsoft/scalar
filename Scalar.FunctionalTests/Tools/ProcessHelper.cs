using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Scalar.FunctionalTests.Tools
{
    public static class ProcessHelper
    {
        public static ProcessResult Run(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.FileName = fileName;
            startInfo.Arguments = arguments;

            return Run(startInfo);
        }

        public static ProcessResult Run(ProcessStartInfo processInfo, string errorMsgDelimeter = "\r\n", object executionLock = null, Stream inputStream = null, int? timeoutSeconds = null)
        {
            using (Process executingProcess = new Process())
            {
                string output = string.Empty;
                string errors = string.Empty;

                // From https://msdn.microsoft.com/en-us/library/system.diagnostics.process.standardoutput.aspx
                // To avoid deadlocks, use asynchronous read operations on at least one of the streams.
                // Do not perform a synchronous read to the end of both redirected streams.
                executingProcess.StartInfo = processInfo;
                executingProcess.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errors = errors + args.Data + errorMsgDelimeter;
                    }
                };

                if (executionLock != null)
                {
                    lock (executionLock)
                    {
                        output = StartProcess(executingProcess, inputStream, timeoutSeconds);
                    }
                }
                else
                {
                    output = StartProcess(executingProcess, inputStream, timeoutSeconds);
                }

                return new ProcessResult(output.ToString(), errors.ToString(), executingProcess.ExitCode);
            }
        }

        public static string GetProgramLocation(string processName)
        {
            ProcessResult result = ProcessHelper.Run(GetProgramLocator(), processName);
            if (result.ExitCode != 0)
            {
                return null;
            }

            string firstPath =
                string.IsNullOrWhiteSpace(result.Output)
                ? null
                : result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (firstPath == null)
            {
                return null;
            }

            try
            {
                return Path.GetDirectoryName(firstPath);
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static string GetProgramLocator()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "where";
            }
            else
            {
                return "which";
            }
        }

        private static string StartProcess(Process executingProcess, Stream inputStream = null, int? timeoutSeconds = null)
        {
            executingProcess.Start();

            if (inputStream != null)
            {
                inputStream.CopyTo(executingProcess.StandardInput.BaseStream);
                executingProcess.StandardInput.BaseStream.Close();
            }

            if (executingProcess.StartInfo.RedirectStandardError)
            {
                executingProcess.BeginErrorReadLine();
            }

            string output = string.Empty;
            if (executingProcess.StartInfo.RedirectStandardOutput)
            {
                output = executingProcess.StandardOutput.ReadToEnd();
            }

            executingProcess.WaitForExit(timeoutSeconds.HasValue ? timeoutSeconds.Value * 1000 : 5 * 60 * 1000);

            if (!executingProcess.HasExited)
            {
                executingProcess.Kill();
                throw new Exception("Command failed to exit on time");
            }

            return output;
        }
    }
}
