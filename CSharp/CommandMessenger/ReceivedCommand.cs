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
    /// <summary> A command received from CmdMessenger </summary>
    public class ReceivedCommand
    {
        private readonly string[] _arguments;   // The arguments
        private int _parameter; // The parameter
        private bool _dumped = true;	// true to dumped

        /// <summary> Gets or sets the time stamp. </summary>
        /// <value> The time stamp. </value>
        public long TimeStamp { get; set; }

        /// <summary> Default constructor. </summary>
        public ReceivedCommand()
        {
        }

        /// <summary> Constructor. </summary>
        /// <param name="arguments"> All command arguments, first one is command ID </param>
        public ReceivedCommand(string[] arguments)
        {
            _arguments = arguments;
        }

        /// <summary> Returns whether this is a valid & filled command. </summary>
        /// <value> true if ok, false if not. </value>
        public bool Ok
        {
            get { return (CommandId >= 0); }
        }

        // Index arguments directly

        /// <summary> Indexer to get arguments directly. </summary>
        /// <value> The indexed item. </value>
        public string this[int index]
        {
            get { return _arguments[index]; }
        }

        /// <summary> Gets the command ID. </summary>
        /// <value> The command ID. </value>
        public int CommandId
        {
            get
            {
                int commandId;

                if (_arguments != null && int.TryParse(_arguments[0], out commandId))
                    return commandId;
                return -1;
            }
        }

        /// <summary> Fetches the next argument. </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Next()
        {
            // If this parameter has already been read, see if there is another one
            if (_dumped)
            {
                if (_parameter < _arguments.Length - 1)
                {
                    _parameter++;
                    _dumped = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary> returns if a next command is available </summary>
        /// <returns> true if it succeeds, false if it fails. </returns>
        public bool Available()
        {
            return Next();
        }

        // ***** String based **** /

        /// <summary> Reads the current argument as short value. </summary>
        /// <returns> The short value. </returns>
        public Int16 ReadInt16Arg()
        {
            if (Next())
            {
                Int16 current;
                if (Int16.TryParse(_arguments[_parameter], out current))
                {
                    _dumped = true;
                    return current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current argument as unsigned short value. </summary>
        /// <returns> The unsigned short value. </returns>
        public UInt16 ReadUInt16Arg()
        {
            if (Next())
            {
                UInt16 current;
                if (UInt16.TryParse(_arguments[_parameter], out current))
                {
                    _dumped = true;
                    return current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current argument as boolean value. </summary>
        /// <returns> The boolean value. </returns>
        public bool ReadBoolArg()
        {
            return (ReadInt32Arg() != 0);
        }

        /// <summary> Reads the current argument as int value. </summary>
        /// <returns> The int value. </returns>
        public Int32 ReadInt32Arg()
        {
            if (Next())
            {
                Int32 current;
                if (Int32.TryParse(_arguments[_parameter], out current))
                {
                    _dumped = true;
                    return current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current argument as unsigned int value. </summary>
        /// <returns> The unsigned int value. </returns>
        public UInt32 ReadUInt32Arg()
        {
            if (Next())
            {
                UInt32 current;
                if (UInt32.TryParse(_arguments[_parameter], out current))
                {
                    _dumped = true;
                    return current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current argument as a float value. </summary>
        /// <returns> The float value. </returns>
        public Single ReadFloatArg()
        {
            if (Next())
            {
                Single current;
                if (Single.TryParse(_arguments[_parameter], out current))
                {
                    _dumped = true;
                    return current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current argument as a double value. </summary>
        /// <returns> The unsigned double value. </returns>
        public Single ReadDoubleArg()
        {
            if (Next())
            {
                Single current;
                if (Single.TryParse(_arguments[_parameter], out current))
                {
                    _dumped = true;
                    return current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current argument as a string value. </summary>
        /// <returns> The string value. </returns>
        public String ReadStringArg()
        {
            if (Next())
            {
                if (_arguments[_parameter] != null)
                {
                    _dumped = true;
                    return _arguments[_parameter];
                }
            }
            return "";
        }

        // ***** Binary **** /

        /// <summary> Reads the current binary argument as a float value. </summary>
        /// <returns> The float value. </returns>
        public Single ReadBinFloatArg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToFloat(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (float) current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current binary argument as a double value. </summary>
        /// <returns> The double value. </returns>
        public Double ReadBinDoubleArg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToDouble(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (double) current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current binary argument as a short value. </summary>
        /// <returns> The short value. </returns>
        public Int16 ReadBinInt16Arg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToInt16(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (Int16) current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current binary argument as a unsigned short value. </summary>
        /// <returns> The unsigned short value. </returns>
        public UInt16 ReadBinUInt16Arg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToUInt16(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (UInt16) current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current binary argument as a int value. </summary>
        /// <returns> The int value. </returns>
        public Int32 ReadBinInt32Arg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToInt32(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (Int32) current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current binary argument as a unsigned int value. </summary>
        /// <returns> The unsigned int value. </returns>
        public UInt32 ReadBinUInt32Arg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToUInt32(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (UInt32) current;
                }
            }
            return 0;
        }

        /// <summary> Reads the current binary argument as a string value. </summary>
        /// <returns> The string value. </returns>
        public String ReadBinStringArg()
        {
            if (Next())
            {
                if (_arguments[_parameter] != null)
                {
                    _dumped = true;
                    return Escaping.Unescape(_arguments[_parameter]);
                }
            }
            return "";
        }

        /// <summary> Reads the current binary argument as a boolean value. </summary>
        /// <returns> The boolean value. </returns>
        public bool ReadBinBoolArg()
        {
            if (Next())
            {
                var current = BinaryConverter.ToByte(_arguments[_parameter]);
                if (current != null)
                {
                    _dumped = true;
                    return (current != 0);
                }
            }
            return false;
        }
    }
}