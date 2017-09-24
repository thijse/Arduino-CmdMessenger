using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommandMessenger
{
    class SpinWait
    {
        public static void SpinUntil(Func<bool> func)
        {
            while (!func.Invoke()) ;
        }
    }
}
