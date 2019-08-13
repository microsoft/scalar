using Scalar.Common.NamedPipes;

namespace Scalar.Service.Handlers
{
    public interface INotificationHandler
    {
        void SendNotification(int sessionId, NamedPipeMessages.Notification.Request request);
    }
}
