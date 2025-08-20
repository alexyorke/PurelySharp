using System;
using PurelySharp.Attributes;

namespace PurelySharp.Demo
{
	public class Demo
	{
		private int _counter = 0;

		[EnforcePure]
		public int AddImpure(int a, int b)
		{
			_counter++;
			return a + b + _counter;
		}

		public static int PureAdd(int a, int b) => a + b;
	}

	internal static class Program
	{
		private static void Main()
		{
			Console.WriteLine(Demo.PureAdd(1, 2));
			Console.WriteLine(new Demo().AddImpure(3, 4));
		}
	}
}


