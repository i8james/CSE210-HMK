using System;

class Program
{
    static void Main(string[] args)
    {
        Random randomGenerator = new Random();
        int randomNumber = randomGenerator.Next(1, 101); // Generates a number between 1 and 100

        int guess = -1;

        while (guess != randomNumber)
        {
            Console.Write("Guess a number between 1 and 100: ");
            string input = Console.ReadLine();

            if (int.TryParse(input, out guess))
            {
                if (guess < randomNumber)
                {
                    Console.WriteLine("Too low! Try again.");
                }
                else if (guess > randomNumber)
                {
                    Console.WriteLine("Too high! Try again.");
                }
                else
                {
                    Console.WriteLine("Congratulations! You've guessed the number!");
                }
            }
            else
            {
                Console.WriteLine("Invalid input. Please enter a valid number.");
            }
        }
    }
}