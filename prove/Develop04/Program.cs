using System;
using System.Collections.Generic;
using System.Threading;

class Program
{
    static void Main(string[] args)
    {
        bool keepRunning = true;
        while (keepRunning)
        {
            Console.Clear();
            Console.WriteLine("Mindfulness Program");
            Console.WriteLine("Menu Options:");
            Console.WriteLine("  1. Breathing Activity");
            Console.WriteLine("  2. Reflection Activity");
            Console.WriteLine("  3. Listing Activity");
            Console.WriteLine("  4. Quit");
            Console.Write("Select a choice: ");
            string choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    new BreathingActivity().Run();
                    break;
                case "2":
                    new ReflectionActivity().Run();
                    break;
                case "3":
                    new ListingActivity().Run();
                    break;
                case "4":
                    keepRunning = false;
                    Console.WriteLine("Goodbye!");
                    Thread.Sleep(1000);
                    break;
                default:
                    Console.WriteLine("Invalid selection. Please choose 1-4.");
                    Thread.Sleep(1000);
                    break;
            }
        }
    }
}

abstract class Activity
{
    protected string _name;
    protected string _description;
    protected int _duration;

    public Activity(string name, string description)
    {
        _name = name;
        _description = description;
    }

    public void Run()
    {
        DisplayStartMessage();
        StartActivity();
        DisplayEndMessage();
    }

    protected virtual void StartActivity()
    {
        // override in subclasses
    }

    protected void DisplayStartMessage()
    {
        Console.Clear();
        Console.WriteLine($"--- {_name} ---");
        Console.WriteLine();
        Console.WriteLine(_description);
        Console.WriteLine();
        Console.Write("Enter duration in seconds: ");
        if (!int.TryParse(Console.ReadLine(), out _duration))
        {
            _duration = 0;
        }
        Console.WriteLine();
        Console.WriteLine("Get ready...");
        ShowSpinner(3);
    }

    protected void DisplayEndMessage()
    {
        Console.WriteLine();
        Console.WriteLine("Well done!!");
        ShowSpinner(3);
        Console.WriteLine($"You have completed the {_name} activity for {_duration} seconds.");
        ShowSpinner(3);
    }

    protected void ShowSpinner(int durationSeconds)
    {
        string[] spinner = { "|", "/", "-", "\\" };
        DateTime endTime = DateTime.Now.AddSeconds(durationSeconds);
        int i = 0;
        while (DateTime.Now < endTime)
        {
            Console.Write(spinner[i % spinner.Length]);
            Thread.Sleep(250);
            Console.Write("\b \b");
            i++;
        }
    }

    protected void ShowCountdown(int count)
    {
        for (int i = count; i > 0; i--)
        {
            Console.Write(i);
            Thread.Sleep(1000);
            Console.Write("\b \b");
        }
    }
}

class BreathingActivity : Activity
{
    public BreathingActivity()
        : base("Breathing Activity",
               "This activity will help you relax by walking you through breathing in and out slowly. Clear your mind and focus on your breathing.")
    { }

    protected override void StartActivity()
    {
        DateTime endTime = DateTime.Now.AddSeconds(_duration);
        while (DateTime.Now < endTime)
        {
            Console.Write("Breathe in... ");
            ShowCountdown(4);
            Console.WriteLine();
            Console.Write("Breathe out... ");
            ShowCountdown(6);
            Console.WriteLine();
        }
    }
}

class ReflectionActivity : Activity
{
    private List<string> _prompts = new List<string>()
    {
        "Think of a time when you stood up for someone else.",
        "Think of a time when you did something really difficult.",
        "Think of a time when you helped someone in need.",
        "Think of a time when you did something truly selfless."
    };

    private List<string> _questions = new List<string>()
    {
        "Why was this experience meaningful to you?",
        "Have you ever done anything like this before?",
        "How did you get started?",
        "How did you feel when it was complete?",
        "What made this time different than other times when you were not as successful?",
        "What is your favorite thing about this experience?",
        "What could you learn from this experience that applies to other situations?",
        "What did you learn about yourself through this experience?",
        "How can you keep this experience in mind in the future?"
    };

    public ReflectionActivity()
        : base("Reflection Activity",
               "This activity will help you reflect on times in your life when you have shown strength and resilience. " +
               "This will help you recognize the power you have and how you can use it in other aspects of your life.")
    { }

    protected override void StartActivity()
    {
        Random rand = new Random();
        Console.WriteLine();
        string prompt = _prompts[rand.Next(_prompts.Count)];
        Console.WriteLine($"-- {prompt} --");
        Console.WriteLine();
        Console.WriteLine("When you have something in mind, press enter to continue.");
        Console.ReadLine();
        DateTime endTime = DateTime.Now.AddSeconds(_duration);
        while (DateTime.Now < endTime)
        {
            string question = _questions[rand.Next(_questions.Count)];
            Console.Write($"> {question} ");
            ShowSpinner(5);
            Console.WriteLine();
        }
    }
}

class ListingActivity : Activity
{
    private List<string> _prompts = new List<string>()
    {
        "Who are people that you appreciate?",
        "What are personal strengths of yours?",
        "Who are people that you have helped this week?",
        "When have you felt the Holy Ghost this month?",
        "Who are some of your personal heroes?"
    };

    public ListingActivity()
        : base("Listing Activity",
               "This activity will help you reflect on the good things in your life by having you list as many things as you can in a certain area.")
    { }

    protected override void StartActivity()
    {
        Random rand = new Random();
        Console.WriteLine();
        string prompt = _prompts[rand.Next(_prompts.Count)];
        Console.WriteLine($"-- {prompt} --");
        Console.WriteLine("You may begin in:");
        ShowCountdown(5);
        Console.WriteLine();
        List<string> items = new List<string>();
        DateTime endTime = DateTime.Now.AddSeconds(_duration);
        while (DateTime.Now < endTime)
        {
            Console.Write("> ");
            string entry = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(entry))
            {
                items.Add(entry);
            }
        }
        Console.WriteLine();
        Console.WriteLine($"You listed {items.Count} items.");
    }
}