using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Overlayer.Core
{
    public static class ExceptionCatcher
    {
        private static object lockObj = new object();
        private static Exception catched;
        public static void Catch()
        {
            AppDomain.CurrentDomain.FirstChanceException += All;
            AppDomain.CurrentDomain.UnhandledException += NotCatched;
        }
        public static void Drop()
        {
            AppDomain.CurrentDomain.FirstChanceException -= All;
            AppDomain.CurrentDomain.UnhandledException -= NotCatched;
        }
        public static event CatchedEvent Catched = delegate { };
        public static event CatchedEvent Unhandled = delegate { };
        private static void All(object sender, FirstChanceExceptionEventArgs e)
        {
            lock(lockObj)
            catched = e.Exception;
            Catched(e.Exception, null);
        }
        private static void NotCatched(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            while (queue.Count > 0)
            {
                Exception e = queue.Dequeue();
            }
            Unhandled(ex, false);
        }
    }
    public delegate void CatchedEvent(Exception exception, bool? handled);
}
