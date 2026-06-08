using System.Threading.Tasks;

namespace CommandMessenger
{
    // ACK waiting is now handled via TaskCompletionSource in CommunicationManager.
    // This class is retained as a thin shim; new code should use CommunicationManager.RegisterAckWait().
    internal class ReceivedCommandSignal
    {
        // No longer used — kept for binary compatibility during transition.
    }
}
