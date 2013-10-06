namespace CommandMessenger
{
    public class CollapseCommandStrategy : CommandStrategy
    {
        public CollapseCommandStrategy(Command command) : base(command)
        {}

        public override void Enqueue()
        {
            //Console.WriteLine("Enqueue {0}", CommandQueue.Count);
            // Remove a command with the same CmdId
            var index = CommandQueue.FindIndex(strategy => strategy.Command.CmdId == Command.CmdId);
            if (index < 0)
            {
                CommandQueue.Enqueue(this);
            }
            else
            {
                CommandQueue[index] = this;
            }      
        }
    }
}
