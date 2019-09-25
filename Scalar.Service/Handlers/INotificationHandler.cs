using Scalar.Common.NamedPipes;

namespace Scalar.Service.Handlers
{
    public interface INotificationHandler
    {
        void SendNotification(NamedPipeMessages.Notification.Request request);
    }
}
