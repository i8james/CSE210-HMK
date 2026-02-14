using System;

class Program
{
    static void Main(string[] args)
    {
        // Reference supports single verse and verse-range constructors
        var reference = new Reference("Proverbs", 3, 5, 6);
        var text = "Trust in the LORD with all thine heart; and lean not unto thine own understanding. In all thy ways acknowledge him, and he shall direct thy paths.";

        var scripture = new Scripture(reference, text);

        while (true)
        {
            Console.Clear();
            scripture.Display();

            if (scripture.IsCompletelyHidden())
            {
                // All words hidden -> program ends
                break;
            }

            Console.WriteLine("\nPress ENTER to hide some words or type 'quit' to exit.");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input) && input.Trim().ToLower() == "quit")
            {
                break;
            }

            // Hide a few random words (up to 3)
            scripture.HideRandomWords(3);
        }
    }
}