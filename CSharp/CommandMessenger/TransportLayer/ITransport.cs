using System;
namespace CommandMessenger.TransportLayer
{
    interface ITransport
    {
        int BytesInBuffer();
        byte[] Read();
        bool StartListening();
        bool StopListening();
        void Write(byte[] buffer);
    }
}
