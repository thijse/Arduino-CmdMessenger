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
using System.Text;

namespace CommandMessenger
{
    /// <summary> String utilities. </summary>
    public class StringUtils
    {
        /// <summary> Convert string from one codepage to another. </summary>
        /// <param name="input">        The string. </param>
        /// <param name="fromEncoding"> input encoding codepage. </param>
        /// <param name="toEncoding">   output encoding codepage. </param>
        /// <returns> . </returns>
        static public string ConvertEncoding(string input, Encoding  fromEncoding, Encoding toEncoding)
        {
            var byteArray = fromEncoding.GetBytes(input);
            var asciiArray = Encoding.Convert(fromEncoding, toEncoding, byteArray);
            var finalString = toEncoding.GetString(asciiArray);
            return finalString;
        }
    }
}
