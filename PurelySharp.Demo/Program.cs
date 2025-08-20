using System;
using PurelySharp.Attributes;

namespace PurelySharp.Demo
{
	public class Demo
	{
		private int _counter = 0;

		// Impure under [EnforcePure] (mutates instance state) → PS0002
		[EnforcePure]
		public int AddImpure(int a, int b)
		{
			_counter++;
			return a + b + _counter;
		}

		// Pure without [EnforcePure] → PS0004
		public static int PureAdd(int a, int b) => a + b;

		// Pure and correctly annotated → no diagnostic
		[EnforcePure]
		public static int ProperPureAdd(int a, int b) => a + b;
	}

	// Demonstrate PS0003 (misplaced attribute on type)
	[EnforcePure]
	public class MisplacedAttributeExample
	{
		// Demonstrate PS0003 (misplaced attribute on property)
		[EnforcePure]
		public int Value { get; } = 42;
	}

	public static class ImpureScenarios
	{
		// I/O under [EnforcePure] → PS0002
		[EnforcePure]
		public static void Log(string message) => Console.WriteLine(message);

		private static int _global;

		// Static state mutation under [EnforcePure] → PS0002
		[EnforcePure]
		public static int IncrementGlobal(int delta)
		{
			_global += delta;
			return _global;
		}

		// Using [Pure] as enforcement, still impure → PS0002
		[Pure]
		public static void MutateThroughPureAlias()
		{
			_global++;
		}
	}

	public class PureScenarios
	{
		public int X { get; }

		// Pure constructor without [EnforcePure] → PS0004
		public PureScenarios(int x) { X = x; }

		// Pure method without [EnforcePure] → PS0004
		public static string Concat(string a, string b) => a + b;

		// Pure property getter without [EnforcePure] → PS0004
		public int DoubleX => X * 2;

		// Properly annotated pure method → no diagnostic
		[EnforcePure]
		public static bool IsEven(int v) => (v & 1) == 0;
	}

	internal static class Program
	{
		private static void Main()
		{
			Console.WriteLine(Demo.PureAdd(1, 2));
			Console.WriteLine(Demo.ProperPureAdd(2, 3));
			Console.WriteLine(new Demo().AddImpure(3, 4));

			ImpureScenarios.Log("hello");
			Console.WriteLine(ImpureScenarios.IncrementGlobal(5));
			ImpureScenarios.MutateThroughPureAlias();

			var p = new PureScenarios(10);
			Console.WriteLine(PureScenarios.Concat("A", "B"));
			Console.WriteLine(p.DoubleX);
			Console.WriteLine(PureScenarios.IsEven(4));
		}
	}
}


