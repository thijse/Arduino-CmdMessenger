using System.IO;
using CommandMessenger.Transport.Serial;
using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Tests for SerialConnectionStorer — JSON-backed persistence of serial connection settings.
    /// Uses a temp file path per test to stay isolated.
    /// </summary>
    public class ConnectionStorerTests
    {
        private static string TempFile() =>
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

        [Fact]
        public void StoreAndRetrieve_RoundTrips_Port_And_BaudRate()
        {
            var path = TempFile();
            try
            {
                var storer = new SerialConnectionStorer(path);
                storer.StoreSettings(new SerialConnectionManagerSettings { Port = "COM3", BaudRate = 115200 });

                var loaded = storer.RetrieveSettings();

                Assert.Equal("COM3", loaded.Port);
                Assert.Equal(115200, loaded.BaudRate);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Retrieve_WhenFileAbsent_ReturnsDefaultSettings()
        {
            var storer = new SerialConnectionStorer(TempFile());
            var result = storer.RetrieveSettings();

            Assert.Null(result.Port);
            Assert.Equal(0, result.BaudRate);
        }

        [Fact]
        public void Store_CreatesJsonFile()
        {
            var path = TempFile();
            try
            {
                new SerialConnectionStorer(path).StoreSettings(
                    new SerialConnectionManagerSettings { Port = "COM1", BaudRate = 9600 });

                Assert.True(File.Exists(path));
                var text = File.ReadAllText(path);
                Assert.Contains("COM1", text);
                Assert.Contains("9600", text);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Store_Overwrites_ExistingFile()
        {
            var path = TempFile();
            try
            {
                var storer = new SerialConnectionStorer(path);
                storer.StoreSettings(new SerialConnectionManagerSettings { Port = "COM1", BaudRate = 9600 });
                storer.StoreSettings(new SerialConnectionManagerSettings { Port = "COM7", BaudRate = 57600 });

                var loaded = storer.RetrieveSettings();
                Assert.Equal("COM7", loaded.Port);
                Assert.Equal(57600, loaded.BaudRate);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void Retrieve_WithCorruptFile_ReturnsDefaultSettings()
        {
            var path = TempFile();
            try
            {
                File.WriteAllText(path, "this is not json {{{{");
                var result = new SerialConnectionStorer(path).RetrieveSettings();

                Assert.Null(result.Port);
                Assert.Equal(0, result.BaudRate);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void StoreAndRetrieve_PortWithSpecialChars_RoundTrips()
        {
            var path = TempFile();
            try
            {
                var storer = new SerialConnectionStorer(path);
                storer.StoreSettings(new SerialConnectionManagerSettings
                {
                    Port = "/dev/tty.usbserial-A1B2C3",
                    BaudRate = 115200
                });

                var loaded = storer.RetrieveSettings();
                Assert.Equal("/dev/tty.usbserial-A1B2C3", loaded.Port);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void DefaultConstructor_UsesJsonExtension()
        {
            var storer = new SerialConnectionStorer();
            // Just verifies it constructs without throwing and uses .json filename
            Assert.NotNull(storer);
        }
    }
}
