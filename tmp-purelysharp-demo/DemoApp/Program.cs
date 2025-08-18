using System;
using PurelySharp.Attributes;

class Program
{
    // Should trigger PS0004 (pure but missing EnforcePure)
    static int Add(int a, int b) => a + b;

    // Should trigger PS0002 (marked pure but impure)
    [EnforcePure]
    static int Impure()
    {
        Console.WriteLine("side-effect");
        return 0;
    }

    static void Main()
    {
        Console.WriteLine(Add(1, 2));
        Console.WriteLine(Impure());
    }
}
