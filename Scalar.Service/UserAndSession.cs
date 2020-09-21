namespace Scalar.Service
{
    public class UserAndSession
    {
        public UserAndSession(string userId, int sessionId)
        {
            this.UserId = userId;
            this.SessionId = sessionId;
        }

        public string UserId { get; }
        public int SessionId { get; }
    }
}
