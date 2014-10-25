#region CmdMessenger - MIT - (c) 2014 Thijs Elenbaas.
/*
  CmdMessenger - library that provides command based messaging

  Permission is hereby granted, free of charge, to any person obtaining
  a copy of this software and associated documentation files (the
  "Software"), to deal in the Software without restriction, including
  without limitation the rights to use, copy, modify, merge, publish,
  distribute, sublicense, and/or sell copies of the Software, and to
  permit persons to whom the Software is furnished to do so, subject to
  the following conditions:

  The above copyright notice and this permission notice shall be
  included in all copies or substantial portions of the Software.

  Copyright 2014 - Thijs Elenbaas
*/

#endregion

using System;
using System.Threading;

namespace CommandMessenger
{
    // Functionality comparable to AutoResetEvent (http://www.albahari.com/threading/part2.aspx#_AutoResetEvent)
    // but it implements a time-out and since it's based on the monitor class it should be more efficient.
    public class EventWaiter
    {
        public enum WaitState
        {
            Quit,
            TimeOut,
            Normal
        }

        readonly object _key = new object();
        bool _block;
        bool _quit;


        // start blocked (waiting for signal)
        public EventWaiter()
        {
            lock (_key)
            {
                _block = true;
            }
        }

        // start blocked (waiting for signal) or not blocked (pass through)
        public EventWaiter(bool block)
        {
            lock (_key)
            {
                _block = block;
            }
        }

        // Wait function. Blocks until signal is set or time-out
        public WaitState Wait(int timeOut)
        {
            lock (_key)
            {
                // Check if quit has been raised before the wait function is entered
                if (_quit)
                {
                    // If so, reset quit and exit
                    _quit = false;
                    return WaitState.Quit;
                }

                // Check if signal has already been raised before the wait function is entered
                
                if (!_block)
                {
                    // If so, reset event for next time and exit wait loop
                    _block = true;
                    return WaitState.Normal;
                }
                
                // Set time 
                var millisBefore = TimeUtils.Millis;
                long elapsed = 0;

                // Wait under conditions
                while (elapsed < timeOut && _block && !_quit)
                {
                    Monitor.Wait(_key, timeOut);
                    elapsed = TimeUtils.Millis - millisBefore;
                }

                _block = true;
                // Check if quit signal has already been raised after wait                
                if (_quit)
                {
                    _quit = false;
                    return WaitState.Quit;
                }

                // Return whether the Wait function was quit because of an Set event or timeout
                return elapsed >= timeOut ? WaitState.TimeOut : WaitState.Normal;
            }
        }

        // Sets signal, will unblock thread in Wait function
        public void Set()
        {
            lock (_key)
            {
                _block = false;
                Monitor.Pulse(_key);
            }
        }

        // Resets signal, will block threads entering Wait function
        public void Reset()
        {
            lock (_key)
            {
                _block = true;
                Monitor.Pulse(_key);
            }
        }

        // Quit. Unblocks thread in Wait function and exits
        public void Quit()
        {
            lock (_key)
            {
                _quit = true;
                Monitor.Pulse(_key);
            }
        }

    }
}
