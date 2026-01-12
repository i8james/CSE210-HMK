using System;

class Program
{
    static void Main(string[] args)
    {
        DisplayWelcomeMessage();

        string userName = PromptUserName();
        int userNumber = PromptUserNumber();

        int squaredNumber = SquareNumber(userNumber);

        int birthYear;
        PromptUserBirthYear(out birthYear);

        DisplayResult(userName, squaredNumber, birthYear);
    }

    static void DisplayWelcomeMessage()
    {
        Console.WriteLine("Welcome to the Prep5 Program!");
    }

    static string PromptUserName()
    {
        Console.Write("Please enter your name: ");
        string name = Console.ReadLine();
        return name;
    }
    static int PromptUserNumber()
    {
        Console.Write("Please enter a number: ");
        string input = Console.ReadLine();
        int number;
        while (!int.TryParse(input, out number))
        {
            Console.Write("Invalid input. Please enter a valid number: ");
            input = Console.ReadLine();
        }
        return number;
    }
    static int SquareNumber(int number)
    {
        return number * number;
    }
    static void PromptUserBirthYear(out int birthYear)
    {
        Console.Write("Please enter your birth year: ");
        string input = Console.ReadLine();
        while (!int.TryParse(input, out birthYear))
        {
            Console.Write("Invalid input. Please enter a valid birth year: ");
            input = Console.ReadLine();
        }
    }
    static void DisplayResult(string name, int squaredNumber, int birthYear)
    {
        Console.WriteLine($"Hello, {name}!");
        Console.WriteLine($"The square of your number is: {squaredNumber}");
        Console.WriteLine($"You were born in the year: {birthYear}");
        Console.WriteLine($" {name}, you will turn {2025 - birthYear} years old in 2025.");
    }
}