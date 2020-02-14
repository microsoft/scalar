using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Scalar.Common.Tracing;

namespace Scalar.Common.Git
{
    public class GitCredentialManagerProcess : ICredentialStore
    {
        private static readonly Encoding UTF8NoBOM = new UTF8Encoding(false);

        private readonly string gcmBinPath;
        private readonly string workingDirectoryRoot;

        public GitCredentialManagerProcess(string gcmBinPath, string workingDirectoryRoot = null)
        {
            if (string.IsNullOrWhiteSpace(gcmBinPath))
            {
                throw new ArgumentException(nameof(gcmBinPath));
            }

            this.gcmBinPath = gcmBinPath;
            this.workingDirectoryRoot = workingDirectoryRoot;
        }

        public virtual bool TryDeleteCredential(ITracer tracer, string repoUrl, string username, string password, out string errorMessage)
        {
            var stdin = new StringBuilder();
            if (!TryAppendUrlLines(stdin, repoUrl, out errorMessage))
            {
                return false;
            }

            // Passing the username and password that we want to signal rejection for is optional.
            // Credential helpers that support it can use the provided username/password values to
            // perform a check that they're being asked to delete the same stored credential that
            // the caller is asking them to erase.
            // Ideally, we would provide these values if available, however it does not work as expected
            // with our main credential helper - Windows GCM. With GCM for Windows, the credential acquired
            // with credential fill for dev.azure.com URLs are not erased when the user name / password are passed in.
            // Until the default credential helper works with this pattern, reject credential with just the URL.

            stdin.Append("\n");

            Result result = this.InvokeGcm("erase", stdin.ToString());

            if (!result.IsSuccess)
            {
                tracer.RelatedError("GCM could not erase credentials: {0}", result.Errors);
                errorMessage = result.Errors;
                return false;
            }

            errorMessage = null;
            return true;
        }

        public virtual bool TryStoreCredential(ITracer tracer, string repoUrl, string username, string password, out string errorMessage)
        {
            var stdin = new StringBuilder();
            if (!TryAppendUrlLines(stdin, repoUrl, out errorMessage))
            {
                return false;
            }
            stdin.AppendFormat("username={0}\n", username);
            stdin.AppendFormat("password={0}\n", password);
            stdin.Append("\n");

            Result result = this.InvokeGcm("store", stdin.ToString());

            if (!result.IsSuccess)
            {
                tracer.RelatedError("GCM could not store credentials: {0}", result.Errors);
                errorMessage = result.Errors;
                return false;
            }

            errorMessage = null;
            return true;
        }

        public virtual bool TryGetCredential(
            ITracer tracer,
            string repoUrl,
            out string username,
            out string password,
            out string errorMessage)
        {
            username = null;
            password = null;
            errorMessage = null;

            var stdin = new StringBuilder();
            if (!TryAppendUrlLines(stdin, repoUrl, out errorMessage))
            {
                return false;
            }
            stdin.Append("\n");

            using (ITracer activity = tracer.StartActivity(nameof(this.TryGetCredential), EventLevel.Informational))
            {
                Result result = this.InvokeGcm("get", stdin.ToString());

                if (!result.IsSuccess)
                {
                    tracer.RelatedError("GCM could not get credentials: {0}", result.Errors);
                    errorMessage = result.Errors;
                    return false;
                }

                username = ParseValue(result.Output, "username=");
                password = ParseValue(result.Output, "password=");

                bool success = username != null && password != null;

                var metadata = new EventMetadata();
                metadata.Add("Success", success);
                if (!success)
                {
                    metadata.Add("Output", result.Output);
                }

                activity.Stop(metadata);
                return success;
            }
        }

        private static bool TryAppendUrlLines(StringBuilder sb, string url, out string errorMessage)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                sb.AppendFormat("protocol={0}\n", uri.Scheme);
                sb.AppendFormat("host={0}\n", uri.Host);

                if (uri.PathAndQuery != null)
                {
                    string[] pathAndQuery = uri.PathAndQuery.Split('?');
                    if (pathAndQuery.Length > 0)
                    {
                        // Trim any trailing "/" from the path
                        string path = pathAndQuery[0]?.TrimEnd('/');
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            sb.AppendFormat("path={0}\n", uri.PathAndQuery);
                        }
                    }
                }

                errorMessage = null;
                return true;
            }

            errorMessage = $"Failed to parse URL '{url}' as a valid URI";
            return false;
        }

        private static string ParseValue(string contents, string prefix)
        {
            int startIndex = contents.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
            if (startIndex >= 0 && startIndex < contents.Length)
            {
                int endIndex = contents.IndexOf('\n', startIndex);
                if (endIndex >= 0 && endIndex < contents.Length)
                {
                    return
                        contents
                            .Substring(startIndex, endIndex - startIndex)
                            .Trim('\r');
                }
            }

            return null;
        }

        protected virtual Result InvokeGcm(string command, string stdIn)
        {
            int timeoutMs = -1;

            try
            {
                var processInfo = new ProcessStartInfo(this.gcmBinPath)
                {
                    Arguments = command,
                    WorkingDirectory = this.workingDirectoryRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = UTF8NoBOM,
                    StandardOutputEncoding = UTF8NoBOM,
                    StandardErrorEncoding = UTF8NoBOM,
                };

                processInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                // From https://msdn.microsoft.com/en-us/library/system.diagnostics.process.standardoutput.aspx
                // To avoid deadlocks, use asynchronous read operations on at least one of the streams.
                // Do not perform a synchronous read to the end of both redirected streams.
                using (var process = new Process {StartInfo = processInfo})
                {
                    var output = new StringBuilder();
                    var errors = new StringBuilder();

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            errors.Append(args.Data + "\n");
                        }
                    };

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            output.Append(args.Data + "\n");
                        }
                    };


                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    try
                    {
                        if (stdIn != null)
                        {
                            process.StandardInput.Write(stdIn);
                            process.StandardInput.Close();
                        }
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
                    {
                        // This is thrown if the process completes before we can set a property.
                    }

                    if (!process.WaitForExit(timeoutMs))
                    {
                        process.Kill();

                        return new Result(output.ToString(), $"Operation timed out: {errors}", Result.GenericFailureCode);
                    }

                    return new Result(output.ToString(), errors.ToString(), process.ExitCode);
                }
            }
            catch (Win32Exception e)
            {
                return new Result(string.Empty, e.Message, Result.GenericFailureCode);
            }
        }

        public class Result
        {
            public const int SuccessCode = 0;
            public const int GenericFailureCode = 1;

            public Result(string stdout, string stderr, int exitCode)
            {
                this.Output = stdout;
                this.Errors = stderr;
                this.ExitCode = exitCode;
            }

            public string Output { get; }
            public string Errors { get; }
            public int ExitCode { get; }

            public bool IsSuccess => this.ExitCode == Result.SuccessCode;
        }
    }
}
