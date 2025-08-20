using System;
using PurelySharp.Attributes;

// PS0003: Misplaced attribute on class
[EnforcePure]
public class Demo
{
    private int _counter = 0;

    // PS0002: Marked pure but mutates instance state
    [EnforcePure]
    public int AddImpure(int a, int b)
    {
        _counter++;
        return a + b + _counter;
    }

    // PS0004: Pure method missing [EnforcePure]
    public static int PureAdd(int a, int b) => a + b;

    // PS0003: Misplaced attribute on field
    [EnforcePure]
    private int _misplaced = 0;
}

class Program
{
    static void Main()
    {
        Console.WriteLine(Demo.PureAdd(1, 2));
        Console.WriteLine(new Demo().AddImpure(3, 4));
    }
}
