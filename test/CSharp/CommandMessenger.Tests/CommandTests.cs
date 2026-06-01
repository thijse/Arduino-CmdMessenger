using Xunit;

namespace CommandMessenger.Tests
{
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
    }
}
