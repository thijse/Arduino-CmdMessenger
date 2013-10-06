namespace CommandMessenger
{
    public class GeneralStrategy
    {
        public ListQueue<CommandStrategy> CommandQueue { get; set; }

        public virtual void OnEnqueue()
        {
        }

        public virtual void OnDequeue()
        {
        }
    }
}
