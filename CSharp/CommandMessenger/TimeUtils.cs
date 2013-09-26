#region CmdMessenger - LGPL - (c) 2013 Thijs Elenbaas.
/*
  CmdMessenger - library that provides command based messaging

  The library is free software; you can redistribute it and/or
  modify it under the terms of the GNU Lesser General Public
  License as published by the Free Software Foundation; either
  version 2.1 of the License, or (at your option) any later version.

  This library is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
  Lesser General Public License for more details.

  You should have received a copy of the GNU Lesser General Public
  License along with this library; if not, write to the Free Software
  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA

    Copyright 2013 - Thijs Elenbaas
 */
#endregion
using System;

namespace CommandMessenger
{
    /// <summary>Class to get a timestamp </summary>
    public static class TimeUtils
    {
        static public DateTime Jan1St1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);  // 1 January 1970

        /// <summary> Gets the milliseconds since 1 Jan 1970. </summary>
        /// <value> The milliseconds since 1 Jan 1970. </value>
        public static long Millis { get { return (long)((DateTime.Now.ToUniversalTime() - Jan1St1970).TotalMilliseconds); } }

        /// <summary> Gets the seconds since 1 Jan 1970. </summary>
        /// <value> The seconds since 1 Jan 1970. </value>
        public static long Seconds { get { return (long)((DateTime.Now.ToUniversalTime() - Jan1St1970).TotalSeconds); } }

        // Returns if it has been more than interval (in ms) ago. Used for periodic actions
        public static bool HasExpired(ref long prevTime, long interval)
        {
            var millis = Millis;
            if (millis - prevTime > interval)
            {
                prevTime = millis;
                return true;
            }
            return false;
        }
    }
}