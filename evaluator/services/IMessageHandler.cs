using System.Threading;
using System.Threading.Tasks;

public interface IMessageHandler<T>
{
    Task ExecuteAsync(T message, CancellationToken cancellationToken);
}