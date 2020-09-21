namespace Scalar.Service
{
    public interface IRegisteredUserStore
    {
        UserAndSession RegisteredUser { get; }
    }
}
