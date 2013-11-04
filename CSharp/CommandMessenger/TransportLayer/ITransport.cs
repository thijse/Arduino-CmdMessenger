using System;
namespace CommandMessenger.TransportLayer
{
    public interface ITransport
    {
        int BytesInBuffer();
        byte[] Read();
        bool StartListening();
        bool StopListening();
        void Write(byte[] buffer);
        event EventHandler NewDataReceived; 
    }
}
