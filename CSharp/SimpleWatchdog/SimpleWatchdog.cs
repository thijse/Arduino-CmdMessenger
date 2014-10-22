// *** SimpleWatchdog ***

// This example shows the usage of the watchdog for communication over virtual serial port and Bluetooth,
// 
// - Use bluetooth connection
// - Use auto scanning and connecting
// - Use watchdog 

using System;
using CommandMessenger;
using CommandMessenger.Serialport;
using CommandMessenger.TransportLayer;

namespace SimpleWatchdog
{
    class SimpleWatchdog
    {
        enum Command
        {
            Identify,          // Command to identify device
            Acknowledge        // Command to acknowledge a received command
        };

        public bool RunLoop { get; set; }

        private const string UniqueDeviceId = "BFAF4176-766E-436A-ADF2-96133C02B03C";
        private static ITransport _transport;
        private static CmdMessenger _cmdMessenger;
        private static ConnectionManager _connectionManager;

        // Setup function
        public void Setup()
        {
            _transport = new SerialTransport {CurrentSerialSettings = {DtrEnable = false}};
                // some boards (e.g. Sparkfun Pro Micro) DtrEnable may need to be true.                        
            // We do not need to set serial port and baud rate: it will be found by the connection manager                                                           

            // Initialize the command messenger with the Serial Port transport layer
            _cmdMessenger = new CmdMessenger(_transport)
            {
                BoardType = BoardType.Bit16, // Set if it is communicating with a 16- or 32-bit Arduino board
                PrintLfCr = false // Do not print newLine at end of command, to reduce data being sent
            };

            _cmdMessenger.Attach(OnUnknownCommand);
            _cmdMessenger.Attach((int) Command.Acknowledge, OnAcknowledge);

            // We don't need to provide a handler for identify command - this is a job for Connection Manager.
            _connectionManager = new SerialConnectionManager((_transport as SerialTransport), _cmdMessenger,
                (int) Command.Identify, UniqueDeviceId)
            {
                // Enable watchdog functionality.
                WatchdogEnabled = true,

                // By default connection settings are persisted. 
                // In this way, when the application is restarted the previously succesfull settings are first tried
                //PersistentSettings = false,

                // Instead of scanning for the connected port, you can also use a fixed port. Set this port through the CurrentSerialSettings
                //UseFixedPort = false
            };

            // Show all connection progress on command line             
            _connectionManager.Progress += (sender, eventArgs) =>
            {
                // If you want to reduce verbosity, you can only show events of level 1 or 2
                if (eventArgs.Level <= 3) Console.WriteLine(eventArgs.Description);
            };
        
            // Finally - activate connection manager
            _connectionManager.StartConnectionManager();
        }

        // Loop function
        public void Loop()
        {
            //Wait for key
            Console.ReadKey();            
            // Stop loop
            RunLoop = false;  
        }

        // Exit function
        public void Exit()
        {
            _connectionManager.Dispose();
            _cmdMessenger.Disconnect();
            _cmdMessenger.Dispose();
            _transport.Dispose();
        }

        static void OnUnknownCommand(ReceivedCommand command)
        {
            // Add your handling of unknown commands here
        }

        static void OnAcknowledge(ReceivedCommand command)
        {
            // Add your handling of command acknowlegdement here
        }
    }
}
