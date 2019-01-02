using Schalltech.EnterpriseLibrary.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WFM.Logging
{
    public class Logger
    {
        public static object Lock = new object();
        static bool Locked = false;

        public static void Aquire()
        {
            while(true)
            {
                System.Threading.Monitor.Enter(Lock);

                try
                {
                    // Attempt to lock the logger.
                    if (!Locked)
                    {
                        Locked = true;
                        break;
                    }
                    else
                    {
                        // The logger is currently locked by another thread.
                        // Sleep a moment and try again.
                        System.Threading.Thread.Sleep(10);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    // Allow other threads to access the logger.
                    System.Threading.Monitor.Exit(Lock);
                }
            }
        }

        public static void Release()
        {
            System.Threading.Monitor.Enter(Lock);

            try
            {
                if (Locked)
                    Locked = false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                System.Threading.Monitor.Exit(Lock);
            }
        }

        /// <summary>
        /// Prints the specified message to the console and log file. This function is thread safe.
        /// </summary>
        /// <param name="title">The title of the logger line.</param>
        /// <param name="message">The message of the logger line.</param>
        /// <param name="severity">The severity of the logger line.</param>
        /// <param name="priority">The priority of the logger line.</param>
        /// <param name="event_id">The thread id invoking the function.</param>
        /// <param name="category">The category the logger will use when writing to the log file.</param>
        public static void WriteLine(string title, object message, TraceEventType severity, int priority, int event_id, LogCategory category)
        {
            Logger.WriteLine(title, message, 0, 0, severity, priority, event_id, category);
        }

        public static void WriteLine(string title, object message, int pad_top, int pad_bottom, TraceEventType severity, int priority, int event_id, LogCategory category)
        {
            try
            {
                Aquire();

                // Add the specified number of blank lines above the message.
                for (int i = 0; i < pad_top; i++ )
                {
                    Console.WriteLine(string.Format("{0} {1}", DateTime.Now.ToString("s"), ""));
                    Schalltech.EnterpriseLibrary.Logging.Logger.Write("", priority, event_id, severity, title, category);
                }

                Console.WriteLine(string.Format("{0} {1}", DateTime.Now.ToString("s"), message));
                Schalltech.EnterpriseLibrary.Logging.Logger.Write(string.Format("{0}", message), priority, event_id, severity, title, category);

                // Add the specified number of blank lines below the message.
                for (int i = 0; i < pad_bottom; i++)
                {
                    Console.WriteLine(string.Format("{0} {1}", DateTime.Now.ToString("s"), ""));
                    Schalltech.EnterpriseLibrary.Logging.Logger.Write("", priority, event_id, severity, title, category);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                Release();
            }
        }

        /// <summary>
        /// Prints the specified message to the console and log file. This function is not thread safe and should be used in conjunction with Aquire() and Release() calls.
        /// </summary>
        /// <param name="title">The title of the logger line.</param>
        /// <param name="message">The message of the logger line.</param>
        /// <param name="severity">The severity of the logger line.</param>
        /// <param name="priority">The priority of the logger line.</param>
        /// <param name="event_id">The thread id invoking the function.</param>
        /// <param name="category">The category the logger will use when writing to the log file.</param>
        public static void Write(string title, object message, TraceEventType severity, int priority, int event_id, LogCategory category)
        {
            try
            {
                Console.WriteLine(string.Format("{0} {1}", DateTime.Now.ToString("s"), message));
                Schalltech.EnterpriseLibrary.Logging.Logger.Write(string.Format("{0}", message), priority, event_id, severity, title, category);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
