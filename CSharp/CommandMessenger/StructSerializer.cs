using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace CommandMessenger
{
    class StructSerializer
    {
        public static byte[] StructureToByteArray(object obj)
        {
            int len    = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        public static void ByteArrayToStructure(byte[] bytearray, ref object obj)
        {
            int len  = Marshal.SizeOf(obj);
            IntPtr i = Marshal.AllocHGlobal(len);
            Marshal.Copy(bytearray, 0, i, len);
            obj      = Marshal.PtrToStructure(i, obj.GetType());
            Marshal.FreeHGlobal(i);
        }
    }
}
