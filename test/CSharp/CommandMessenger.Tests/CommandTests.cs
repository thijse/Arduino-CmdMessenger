using Xunit;

namespace CommandMessenger.Tests
{
    /// <summary>
    /// Tests for the Command data structures (SendCommand, ReceivedCommand).
    ///
    /// SendCommand: builds outgoing commands with typed arguments.
    ///   - Verifies CmdId assignment, Ok flag, and argument serialisation for
    ///     string, float, Int16, UInt16, Int32, bool types.
    ///   - Verifies ACK properties (ReqAc, AckCmdId, Timeout).
    ///
    /// ReceivedCommand: parses incoming raw argument arrays.
    ///   - Verifies CmdId extraction from first element.
    ///   - Verifies sequential ReadXxxArg() calls consume arguments in order.
    ///   - Verifies boundary cases: null input, empty array, invalid CmdId,
    ///     reading past available arguments.
    /// </summary>
    public class CommandTests
    {
        [Fact]
        public void SendCommand_CmdId_IsSet()
        {
            var cmd = new SendCommand(5);
            Assert.Equal(5, cmd.CmdId);
            Assert.True(cmd.Ok);
        }

        [Fact]
        public void SendCommand_NegativeId_NotOk()
        {
            var cmd = new SendCommand(-1);
            Assert.False(cmd.Ok);
        }

        [Fact]
        public void SendCommand_WithStringArg()
        {
            var cmd = new SendCommand(3, "hello");
            cmd.InitArguments();
            Assert.Single(cmd.Arguments);
            Assert.Equal("hello", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_WithMultipleStringArgs()
        {
            var cmd = new SendCommand(3, new[] { "a", "b", "c" });
            cmd.InitArguments();
            Assert.Equal(3, cmd.Arguments.Length);
            Assert.Equal(new[] { "a", "b", "c" }, cmd.Arguments);
        }

        [Fact]
        public void SendCommand_WithFloatArg()
        {
            var cmd = new SendCommand(1, 3.14f);
            cmd.InitArguments();
            Assert.Single(cmd.Arguments);
            Assert.Equal("3.14", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_WithInt16Arg()
        {
            var cmd = new SendCommand(1);
            cmd.AddArgument((short)-42);
            cmd.InitArguments();
            Assert.Equal("-42", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_WithUInt16Arg()
        {
            var cmd = new SendCommand(1);
            cmd.AddArgument((ushort)65535);
            cmd.InitArguments();
            Assert.Equal("65535", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_WithInt32Arg()
        {
            var cmd = new SendCommand(1, 123456);
            cmd.InitArguments();
            Assert.Equal("123456", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_WithBoolArg()
        {
            var cmdTrue = new SendCommand(1, true);
            cmdTrue.InitArguments();
            Assert.Equal("1", cmdTrue.Arguments[0]);

            var cmdFalse = new SendCommand(1, false);
            cmdFalse.InitArguments();
            Assert.Equal("0", cmdFalse.Arguments[0]);
        }

        [Fact]
        public void SendCommand_AckProperties()
        {
            var cmd = new SendCommand(5, 10, 2000);
            Assert.True(cmd.ReqAc);
            Assert.Equal(10, cmd.AckCmdId);
            Assert.Equal(2000, cmd.Timeout);
        }

        [Fact]
        public void ReceivedCommand_ParsesRawArguments()
        {
            var cmd = new ReceivedCommand(new[] { "3", "hello", "42" });
            Assert.Equal(3, cmd.CmdId);
            Assert.True(cmd.Ok);
        }

        [Fact]
        public void ReceivedCommand_ReadStringArg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "world" });
            Assert.Equal("world", cmd.ReadStringArg());
        }

        [Fact]
        public void ReceivedCommand_ReadInt16Arg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "-100" });
            Assert.Equal((short)-100, cmd.ReadInt16Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadUInt16Arg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "65000" });
            Assert.Equal((ushort)65000, cmd.ReadUInt16Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadInt32Arg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "123456" });
            Assert.Equal(123456, cmd.ReadInt32Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadUInt32Arg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "4000000000" });
            Assert.Equal(4000000000u, cmd.ReadUInt32Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadFloatArg()
        {
            var cmd = new ReceivedCommand(new[] { "1", "3.14" });
            Assert.Equal(3.14f, cmd.ReadFloatArg(), 5);
        }

        [Fact]
        public void ReceivedCommand_ReadBoolArg()
        {
            var cmdTrue = new ReceivedCommand(new[] { "1", "1" });
            Assert.True(cmdTrue.ReadBoolArg());

            var cmdFalse = new ReceivedCommand(new[] { "1", "0" });
            Assert.False(cmdFalse.ReadBoolArg());
        }

        [Fact]
        public void ReceivedCommand_MultipleArgs_ReadSequentially()
        {
            var cmd = new ReceivedCommand(new[] { "2", "hello", "42", "3.14" });
            Assert.Equal("hello", cmd.ReadStringArg());
            Assert.Equal(42, cmd.ReadInt32Arg());
            Assert.Equal(3.14f, cmd.ReadFloatArg(), 5);
        }

        [Fact]
        public void ReceivedCommand_ReadBeyondAvailable_ReturnsDefault()
        {
            var cmd = new ReceivedCommand(new[] { "1", "42" });
            Assert.Equal(42, cmd.ReadInt32Arg());
            // No more args — should return 0
            Assert.Equal(0, cmd.ReadInt32Arg());
        }

        [Fact]
        public void ReceivedCommand_Available_ReturnsFalseWhenEmpty()
        {
            var cmd = new ReceivedCommand(new[] { "1" }); // no args
            Assert.False(cmd.Available());
        }

        [Fact]
        public void ReceivedCommand_Available_ReturnsTrueWhenHasArgs()
        {
            var cmd = new ReceivedCommand(new[] { "1", "hello" });
            Assert.True(cmd.Available());
        }

        [Fact]
        public void ReceivedCommand_NullRawArgs_NotOk()
        {
            var cmd = new ReceivedCommand(null);
            Assert.False(cmd.Ok);
        }

        [Fact]
        public void ReceivedCommand_EmptyRawArgs_NotOk()
        {
            var cmd = new ReceivedCommand(new string[0]);
            Assert.False(cmd.Ok);
        }

        [Fact]
        public void ReceivedCommand_InvalidCmdId_NotOk()
        {
            var cmd = new ReceivedCommand(new[] { "abc" });
            Assert.False(cmd.Ok);
        }

        // --- Numeric boundary edge cases ---

        [Theory]
        [InlineData("2147483647", 2147483647)]   // Int32.MaxValue
        [InlineData("-2147483648", -2147483648)] // Int32.MinValue
        [InlineData("0", 0)]
        public void ReceivedCommand_ReadInt32Arg_Boundaries(string raw, int expected)
        {
            var cmd = new ReceivedCommand(new[] { "1", raw });
            Assert.Equal(expected, cmd.ReadInt32Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadInt32Arg_Overflow_ReturnsZero()
        {
            // Value exceeds Int32 range
            var cmd = new ReceivedCommand(new[] { "1", "9999999999999" });
            Assert.Equal(0, cmd.ReadInt32Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadInt32Arg_EmptyString_ReturnsZero()
        {
            var cmd = new ReceivedCommand(new[] { "1", "" });
            Assert.Equal(0, cmd.ReadInt32Arg());
        }

        [Theory]
        [InlineData("65535", (ushort)65535)] // UInt16.MaxValue
        [InlineData("0", (ushort)0)]         // UInt16.MinValue
        public void ReceivedCommand_ReadUInt16Arg_Boundaries(string raw, ushort expected)
        {
            var cmd = new ReceivedCommand(new[] { "1", raw });
            Assert.Equal(expected, cmd.ReadUInt16Arg());
        }

        [Fact]
        public void ReceivedCommand_ReadFloatArg_NaN()
        {
            var cmd = new ReceivedCommand(new[] { "1", "NaN" });
            Assert.True(float.IsNaN(cmd.ReadFloatArg()));
        }

        [Fact]
        public void ReceivedCommand_ReadFloatArg_Infinity()
        {
            var cmd = new ReceivedCommand(new[] { "1", "Infinity" });
            Assert.True(float.IsPositiveInfinity(cmd.ReadFloatArg()));
        }

        [Fact]
        public void ReceivedCommand_ReadFloatArg_NegativeInfinity()
        {
            var cmd = new ReceivedCommand(new[] { "1", "-Infinity" });
            Assert.True(float.IsNegativeInfinity(cmd.ReadFloatArg()));
        }

        [Fact]
        public void ReceivedCommand_ReadFloatArg_EmptyString_ReturnsZero()
        {
            var cmd = new ReceivedCommand(new[] { "1", "" });
            Assert.Equal(0f, cmd.ReadFloatArg());
        }

        // --- String edge cases ---

        [Fact]
        public void ReceivedCommand_ReadStringArg_EmptyString()
        {
            var cmd = new ReceivedCommand(new[] { "1", "" });
            Assert.Equal("", cmd.ReadStringArg());
        }

        [Fact]
        public void ReceivedCommand_ReadStringArg_WhitespaceOnly()
        {
            var cmd = new ReceivedCommand(new[] { "1", "   " });
            Assert.Equal("   ", cmd.ReadStringArg());
        }

        [Fact]
        public void SendCommand_EmptyStringArg()
        {
            var cmd = new SendCommand(1, "");
            cmd.InitArguments();
            Assert.Single(cmd.Arguments);
            Assert.Equal("", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_WhitespaceArg()
        {
            var cmd = new SendCommand(1, "  \t  ");
            cmd.InitArguments();
            Assert.Equal("  \t  ", cmd.Arguments[0]);
        }

        [Fact]
        public void SendCommand_Latin1StringArg()
        {
            var cmd = new SendCommand(1, "café");
            cmd.InitArguments();
            Assert.Equal("café", cmd.Arguments[0]);
        }
    }
}
