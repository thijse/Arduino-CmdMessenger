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
    public class SendCommand : Command
    {
        //private List<String> _arguments;	// The argument list of the command, first one is the command ID
        
        ///// <summary> Gets or sets the command ID. </summary>
        ///// <value> The command ID. </value>
        //public int CmdId { get; set; }

        /// <summary> Indicates if we want to wait for an acknowlegde command. </summary>
        /// <value> true if request acknowledge, false if not. </value>
        public bool ReqAc { get; set; }

        /// <summary> Gets or sets the acknowledge command ID. </summary>
        /// <value> the acknowledge command ID. </value>
        public int AckCmdId { get; set; }

        /// <summary> Gets or sets the time we want to wait for the acknowledgde command. </summary>
        /// <value> The timeout on waiting for an acknowlegde</value>
        public int Timeout { get; set; }
  
        ///// <summary> Gets the command arguments. </summary>
        ///// <value> The arguments, first one is the command ID </value>
        //public String[] Arguments
        //{
        //    get { return _arguments.ToArray(); }
        //}

        /// <summary> Constructor. </summary>
        /// <param name="cmdId"> the command ID. </param>
        public SendCommand(int cmdId)
        {
            Init(cmdId, false, 0, 0);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, string argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">     Command ID </param>
        /// <param name="arguments"> The arguments. </param>
        public SendCommand(int cmdId, string[] arguments)
        {
            Init(cmdId, false, 0, 0);
            AddArguments(arguments);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, float argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, double argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, UInt16 argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, Int16 argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, UInt32 argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, Int32 argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        public SendCommand(int cmdId, bool argument)
        {
            Init(cmdId, false, 0, 0);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, string argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">     Command ID </param>
        /// <param name="arguments"> The arguments. </param>
        /// <param name="ackCmdId">  Acknowlegde command ID. </param>
        /// <param name="timeout">   The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, string[] arguments, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArguments(arguments);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, float argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, double argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, Int16 argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, UInt16 argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, Int32 argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Constructor. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="argument"> The argument. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        public SendCommand(int cmdId, UInt32 argument, int ackCmdId, int timeout)
        {
            Init(cmdId, true, ackCmdId, timeout);
            AddArgument(argument);
        }

        /// <summary> Initialises this object. </summary>
        /// <param name="cmdId">    Command ID </param>
        /// <param name="reqAc">    true to request ac. </param>
        /// <param name="ackCmdId"> Acknowlegde command ID. </param>
        /// <param name="timeout">  The timeout on waiting for an acknowlegde</param>
        private void Init(int cmdId, bool reqAc, int ackCmdId, int timeout)
        {
            ReqAc = reqAc;
            CmdId = cmdId;
            AckCmdId = ackCmdId;
            Timeout = timeout;
            _arguments = new List<string>();
        }

        // ***** String based **** /

        /// <summary> Adds a command argument.  </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(string argument)
        {
            if (argument != null)
                _arguments.Add(argument);
        }

        /// <summary> Adds command arguments.  </summary>
        /// <param name="arguments"> The arguments. </param>
        public void AddArguments(string[] arguments)
        {
            if (arguments != null)
                _arguments.AddRange(arguments);
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(Single argument)
        {
            _arguments.Add(argument.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(Double argument)
        {
            _arguments.Add(argument.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(Int16 argument)
        {
            _arguments.Add(argument.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(UInt16 argument)
        {
            _arguments.Add(argument.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(Int32 argument)
        {
            _arguments.Add(argument.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(UInt32 argument)
        {
            _arguments.Add(argument.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Adds a command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddArgument(bool argument)
        {
            AddArgument((Int32) (argument ? 1 : 0));
        }

        // ***** Binary **** /

        /// <summary> Adds a binary command argument.  </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(string argument)
        {
            _arguments.Add(Escaping.Escape(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(Single argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(Double argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(Int16 argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(UInt16 argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(Int32 argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(UInt32 argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument));
        }

        /// <summary> Adds a binary command argument. </summary>
        /// <param name="argument"> The argument. </param>
        public void AddBinArgument(bool argument)
        {
            _arguments.Add(BinaryConverter.ToString(argument ? (byte) 1 : (byte) 0));
        }
    }
}
