namespace CommandMessenger
{
    public class CommandStrategy
    {
        public CommandStrategy(Command command)
        {
            Command = command;   
        }

        public ListQueue<CommandStrategy> CommandQueue { get; set; }
        public CommandQueue.threadRunStates ThreadRunState { get; set; }
        public Command Command { get; private set; }

        public virtual void Enqueue()
        {
            // Add command (strategy) to command queue
            //Console.WriteLine("Enqueue {0}", CommandQueue.Count);
            CommandQueue.Enqueue(this);
        }

        public virtual void DeQueue()
        {
            // Remove this command (strategy) from command queue
            //Console.WriteLine("Dequeue {0}", CommandQueue.Count);
            CommandQueue.Remove(this);
        }
    }
}
