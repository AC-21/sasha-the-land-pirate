#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace AtomicLandPirate.LastBearingTests
{
    internal sealed class TestHarness
    {
        private readonly List<string> _failures = new List<string>();
        private int _passed;

        public int Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS " + name);
            }
            catch (Exception exception)
            {
                _failures.Add(name + ": " + exception.Message);
                Console.Error.WriteLine("FAIL " + name + ": " + exception);
            }

            return _failures.Count;
        }

        public int Finish(string suite)
        {
            Console.WriteLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "RESULT suite={0} passed={1} failed={2}",
                    suite,
                    _passed,
                    _failures.Count));
            return _failures.Count == 0 ? 0 : 1;
        }

        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: expected {1}, found {2}",
                        message,
                        expected,
                        actual));
            }
        }

        public static TException Throws<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }

            throw new InvalidOperationException(message);
        }
    }
}
