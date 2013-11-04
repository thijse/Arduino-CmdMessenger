using System;

namespace CommandMessenger
{
    public class DisposableObject : IDisposable
    {
        protected DisposeStack DisposeStack = new DisposeStack();
        protected bool IsDisposed = false;

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Remove all references and remove children
        /// </summary>
        /// <param name="disposing">If true, cleanup</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    DisposeStack.Dispose();
                    DisposeStack = null;
                    IsDisposed = true;
                }
            }
        }
    }


}
