using Newtonsoft.Json;
using System.Collections.Generic;

namespace Scalar.Common.NamedPipes
{
    /// <summary>
    /// Define messages used to communicate via the named-pipe in Scalar.
    /// </summary>
    /// <remarks>
    /// This class is defined as partial so that Scalar.Hooks
    /// can compile the portions of it that it cares about (see LockedNamedPipeMessages).
    /// </remarks>
    public static partial class NamedPipeMessages
    {
        public const string UnknownRequest = "UnknownRequest";
        public const string UnknownScalarState = "UnknownScalarState";
        public const string MountNotReadyResult = "MountNotReady";

        private const string ResponseSuffix = "Response";
        private const char MessageSeparator = '|';

        public enum CompletionState
        {
            NotCompleted,
            Success,
            Failure
        }

        public static class GetStatus
        {
            public const string Request = "GetStatus";
            public const string Mounting = "Mounting";
            public const string Ready = "Ready";
            public const string Unmounting = "Unmounting";
            public const string MountFailed = "MountFailed";

            public class Response
            {
                public string MountStatus { get; set; }
                public string EnlistmentRoot { get; set; }
                public string LocalCacheRoot { get; set; }
                public string RepoUrl { get; set; }
                public string CacheServer { get; set; }
                public int BackgroundOperationCount { get; set; }
                public string DiskLayoutVersion { get; set; }

                public static Response FromJson(string json)
                {
                    return JsonConvert.DeserializeObject<Response>(json);
                }

                public string ToJson()
                {
                    return JsonConvert.SerializeObject(this);
                }
            }
        }

        public static class Unmount
        {
            public const string Request = "Unmount";
            public const string NotMounted = "NotMounted";
            public const string Acknowledged = "Ack";
            public const string Completed = "Complete";
            public const string AlreadyUnmounting = "AlreadyUnmounting";
            public const string MountFailed = "MountFailed";
        }

        public static class Notification
        {
            public class Request
            {
                public const string Header = nameof(Notification);

                public enum Identifier
                {
                    AutomountStart,
                    MountSuccess,
                    MountFailure,
                    UpgradeAvailable
                }

                public Identifier Id { get; set; }

                public string Title { get; set; }

                public string Message { get; set; }

                public string Enlistment { get; set; }

                public int EnlistmentCount { get; set; }

                public string NewVersion { get; set; }

                public static Request FromMessage(Message message)
                {
                    return JsonConvert.DeserializeObject<Request>(message.Body);
                }

                public Message ToMessage()
                {
                    return new Message(Header, JsonConvert.SerializeObject(this));
                }
            }
        }

        public class Message
        {
            public Message(string header, string body)
            {
                this.Header = header;
                this.Body = body;
            }

            public string Header { get; }

            public string Body { get; }

            public static Message FromString(string message)
            {
                string header = null;
                string body = null;
                if (!string.IsNullOrEmpty(message))
                {
                    string[] parts = message.Split(new[] { NamedPipeMessages.MessageSeparator }, count: 2);
                    header = parts[0];
                    if (parts.Length > 1)
                    {
                        body = parts[1];
                    }
                }

                return new Message(header, body);
            }

            public override string ToString()
            {
                string result = string.Empty;
                if (!string.IsNullOrEmpty(this.Header))
                {
                    result = this.Header;
                }

                if (this.Body != null)
                {
                    result = result + NamedPipeMessages.MessageSeparator + this.Body;
                }

                return result;
            }
        }

        public class UnregisterRepoRequest
        {
            public const string Header = nameof(UnregisterRepoRequest);

            public string EnlistmentRoot { get; set; }

            public static UnregisterRepoRequest FromMessage(Message message)
            {
                return JsonConvert.DeserializeObject<UnregisterRepoRequest>(message.Body);
            }

            public Message ToMessage()
            {
                return new Message(Header, JsonConvert.SerializeObject(this));
            }

            public class Response : BaseResponse<UnregisterRepoRequest>
            {
                public static Response FromMessage(Message message)
                {
                    return JsonConvert.DeserializeObject<Response>(message.Body);
                }
            }
        }

        public class RegisterRepoRequest
        {
            public const string Header = nameof(RegisterRepoRequest);

            public string EnlistmentRoot { get; set; }
            public string OwnerSID { get; set; }

            public static RegisterRepoRequest FromMessage(Message message)
            {
                return JsonConvert.DeserializeObject<RegisterRepoRequest>(message.Body);
            }

            public Message ToMessage()
            {
                return new Message(Header, JsonConvert.SerializeObject(this));
            }

            public class Response : BaseResponse<RegisterRepoRequest>
            {
                public static Response FromMessage(Message message)
                {
                    return JsonConvert.DeserializeObject<Response>(message.Body);
                }
            }
        }

        public class GetActiveRepoListRequest
        {
            public const string Header = nameof(GetActiveRepoListRequest);

            public static GetActiveRepoListRequest FromMessage(Message message)
            {
                return JsonConvert.DeserializeObject<GetActiveRepoListRequest>(message.Body);
            }

            public Message ToMessage()
            {
                return new Message(Header, JsonConvert.SerializeObject(this));
            }

            public class Response : BaseResponse<GetActiveRepoListRequest>
            {
                public List<string> RepoList { get; set; }

                public static Response FromMessage(Message message)
                {
                    return JsonConvert.DeserializeObject<Response>(message.Body);
                }
            }
        }

        public class BaseResponse<TRequest>
        {
            public const string Header = nameof(TRequest) + ResponseSuffix;

            public CompletionState State { get; set; }
            public string ErrorMessage { get; set; }

            public Message ToMessage()
            {
                return new Message(Header, JsonConvert.SerializeObject(this));
            }
        }
    }
}
