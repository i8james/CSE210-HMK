using System;

class Program
{
    static void Main(string[] args)
    {
        // Create fractions using different constructors
        Fraction f1 = new Fraction();
        Console.WriteLine($"f1: {f1}");
        Console.WriteLine($"f1 decimal: {f1.GetDecimalValue()}");
        Console.WriteLine();

        Fraction f2 = new Fraction(5);
        Console.WriteLine($"f2: {f2}");
        Console.WriteLine($"f2 decimal: {f2.GetDecimalValue()}");
        Console.WriteLine();

        Fraction f3 = new Fraction(3, 4);
        Console.WriteLine($"f3: {f3}");
        Console.WriteLine($"f3 decimal: {f3.GetDecimalValue()}");
        Console.WriteLine();

        Fraction f4 = new Fraction(1, 3);
        Console.WriteLine($"f4: {f4}");
        Console.WriteLine($"f4 decimal: {f4.GetDecimalValue()}");
    }
}