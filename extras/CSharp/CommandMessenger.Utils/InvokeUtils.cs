using System.ComponentModel;
using System.Windows.Forms;

namespace CommandMessenger.Utils
{
    public static class InvokeUtils
    {
        public static void InvokeIfRequired(this ISynchronizeInvoke invokableObject, MethodInvoker action)
        {
            if (invokableObject.InvokeRequired)
                invokableObject.Invoke(action, new object[0]);
            else
                action();
        }
    }
}
