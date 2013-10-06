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
using System.Collections.Generic;
using System.Globalization;

namespace CommandMessenger
{
    /// <summary> A command to be send by CmdMessenger </summary>
    public class Command
    {
        protected List<String> _arguments;	// The argument list of the command, first one is the command ID
        
        /// <summary> Gets or sets the command ID. </summary>
        /// <value> The command ID. </value>
        public int CmdId { get; set; }
  
        /// <summary> Gets the command arguments. </summary>
        /// <value> The arguments, first one is the command ID </value>
        public String[] Arguments
        {
            get { return _arguments.ToArray(); }
        }

        /// <summary> Gets or sets the time stamp. </summary>
        /// <value> The time stamp. </value>
        public long TimeStamp { get; set; }

        /// <summary> Constructor. </summary>
        public Command()
        {
            _arguments = new List<string>();
            TimeStamp =  TimeUtils.Millis;
        }

        /// <summary> Returns whether this is a valid & filled command. </summary>
        /// <value> true if ok, false if not. </value>
        public bool Ok
        {
            get { return (CmdId >= 0); }
        }

    }
}
