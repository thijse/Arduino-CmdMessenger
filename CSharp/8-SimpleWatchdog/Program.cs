using System;
using CommandMessenger;
using CommandMessenger.Serialport;
using CommandMessenger.TransportLayer;

namespace SimpleWatchdog
{
    class Program
    {
        enum Command
        {
            Identify,          // Command to identify device
            Acknowledge        // Command to acknowledge a received command
        };

        private const string UniqueDeviceId = "BFAF4176-766E-436A-ADF2-96133C02B03C";

        private static ITransport _transport;
        private static CmdMessenger _cmdMessenger;
        private static ConnectionManager _connectionManager;

        static void Main(string[] args)
        {
            _transport = new SerialTransport { CurrentSerialSettings = { DtrEnable = false } }; // some boards (e.g. Sparkfun Pro Micro) DtrEnable may need to be true.                        
            // We do not need to set serial port and baud rate: it will be found by the connection manager                                                           

            // Initialize the command messenger with the Serial Port transport layer
            _cmdMessenger = new CmdMessenger(_transport)
            {
                BoardType = BoardType.Bit16, // Set if it is communicating with a 16- or 32-bit Arduino board
                PrintLfCr = false            // Do not print newLine at end of command, to reduce data being sent
            };

            _cmdMessenger.Attach(OnUnknownCommand);
            _cmdMessenger.Attach((int)Command.Acknowledge, OnAcknowledge);
            // We don't need to provide a handler for identify command - this is a job for Connection Manager.

            _connectionManager = new SerialConnectionManager((_transport as SerialTransport), _cmdMessenger, (int)Command.Identify, UniqueDeviceId);

            // Enable watchdog functionality.
            _connectionManager.WatchdogEnabled = true;

            // Event notifying on process
            _connectionManager.Progress += (sender, eventArgs) => Console.WriteLine(eventArgs.Description);

            // Finally - activate connection manager
            _connectionManager.StartConnectionManager();


            Console.ReadKey();

            _connectionManager.Dispose();
            _cmdMessenger.Disconnect();
            _cmdMessenger.Dispose();
            _transport.Dispose();
        }

        static void OnUnknownCommand(ReceivedCommand command)
        {
            
        }

        static void OnAcknowledge(ReceivedCommand command)
        {

        }
    }
}
