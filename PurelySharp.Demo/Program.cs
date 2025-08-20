using PurelySharp.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Demo
{
    internal class Program
    {
        static void Main()
        {
            Console.WriteLine("PurelySharp Demo");
            Console.WriteLine(DemoAlgorithms.AddAndDouble(1, 2)); // expected PS0004 (pure, missing attribute)
            Console.WriteLine(new StatefulCalculator().AddImpure(3, 4)); // PS0002
        }
    }

    // PS0002: Marked pure but mutates instance state
    public class StatefulCalculator
    {
        private int _counter = 0;

        [EnforcePure]
        public int AddImpure(int a, int b)
        {
            _counter++;
            return a + b + _counter;
        }
    }

    public static class DemoAlgorithms
    {
        // PS0004: appears pure but not annotated — keep it trivially pure to match current rules
        public static int AddAndDouble(int a, int b)
        {
            int sum = a + b;
            return sum * 2;
        }

        // PS0002: impure due to I/O
        [EnforcePure]
        public static int ReadThenAdd(int a, int b)
        {
            Console.WriteLine($"Adding {a} + {b}");
            return a + b;
        }

        // Avoid complex functional pipeline in PS0004 demo to reduce false PS0002 under current rules

        // PS0002: volatile reads are impure
        private static volatile int s_flag;

        [EnforcePure]
        public static bool CheckFlag() => s_flag == 1;

        // PS0002: unsafe operations are impure (if analyzer flags)
        [EnforcePure]
        public static int ModifyArrayImpure(int[] arr)
        {
            arr[0] = 42;
            return arr[0];
        }
    }
}
