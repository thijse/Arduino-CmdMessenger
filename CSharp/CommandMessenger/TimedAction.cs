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
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CommandMessenger
{
    /// <summary>
    /// Starts a recurring action with fixed interval   
    /// If still running at next call, the action is skipped
    /// </summary>
    public class TimedAction
    {
        /// <summary> Thread state.  </summary>
        private class ThreadState
        {
            public volatile bool IsRunning;
        }


        private readonly Action _action;	        // The action to execute
        private readonly Timer _actionTimer;	    // The action timer
        private readonly ThreadState _threadState;  // State of the thread

        /// <summary> Returns whether this object is running. </summary>
        /// <value> true if this object is running, false if not. </value>
        public bool IsRunning
        {
            get { return _threadState.IsRunning; }
        }

        /// <summary> Constructor. </summary>
        /// <param name="interval"> The execution interval. </param>
        /// <param name="action">   The action to execute. </param>
        public TimedAction(double interval, Action action)
        {
            _action = action;
            _threadState = new ThreadState {IsRunning = false};


            _actionTimer = new Timer(interval) {Enabled = false, SynchronizingObject = null};
            _actionTimer.Elapsed += OnActionTimer;
        }


        /// <summary> Finaliser. </summary>
        ~TimedAction()
        {
            // Stop elapsed event handler
            StopAndWait();
            _actionTimer.Elapsed -= OnActionTimer;
            // Wait until last action has been executed or timeout
        }

        // On timer event run non-blocking action

        /// <summary> Executes the non-blocking action timer action. </summary>
        /// <param name="source"> Ignored. </param>
        /// <param name="e">      Ignored. </param>
        private void OnActionTimer(object source, ElapsedEventArgs e)
        {
            // Start background thread, but only if not yet running
            if (!_threadState.IsRunning)
            {
                RunNonBlockingAction(_action);
            }
        }

        // Execute the action if not already running

        /// <summary> Executes the non blocking action operation. </summary>
        /// <param name="action"> The action. </param>
        private void RunNonBlockingAction(Action action)
        {
            // Additional (non-blocking) test on _threadIsRunning
            // Request the lock for running background thread
            if (Monitor.TryEnter(_threadState))
            {
                try
                {
                    if (_actionTimer.Enabled)
                    {
                        action();
                    }
                }
                catch (NullReferenceException e)
                {
                    Console.WriteLine("{0} Caught exception while running ActionBackground #1.", e);
                }
                finally
                {
                    // Ensure that the lock is released.
                    _threadState.IsRunning = false;
                    Monitor.Exit(_threadState);
                }
                return;
            }
            // Exit because Action is already running
            return;
        }

        /// <summary> Start timed actions. </summary>
        public void Start()
        {
            // Start interval events
            _actionTimer.Enabled = true;
        }

        /// <summary> Stop timed actions. </summary>
        public void Stop()
        {
            // Halt new interval events
            _actionTimer.Enabled = false;
        }

        /// <summary> Stop timed actions and wait until running function has finished. </summary>
        public void StopAndWait()
        {
            // Halt new interval events
            _actionTimer.Enabled = false;
            while (_threadState.IsRunning)
            {
            }
        }
    }
}