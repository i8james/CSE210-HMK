using System;
using System.Collections.Generic;
using System.IO;

// Creativity: Added a leveling system where the user's level is calculated as score / 1000 + 1.
// Display the level with fun messages to encourage progression.
// Also, added a simple badge system: earn badges at certain score milestones (e.g., 5000, 10000).
// Badges are displayed in the menu.

class Program
{
    static List<Goal> goals = new List<Goal>();
    static int score = 0;
    static List<string> badges = new List<string>();

    static void Main(string[] args)
    {
        LoadGoals();
        bool running = true;
        while (running)
        {
            Console.Clear();
            DisplayScore();
            DisplayMenu();
            string choice = Console.ReadLine();
            switch (choice)
            {
                case "1":
                    DisplayGoals();
                    break;
                case "2":
                    CreateGoal();
                    break;
                case "3":
                    RecordEvent();
                    break;
                case "4":
                    SaveGoals();
                    break;
                case "5":
                    LoadGoals();
                    break;
                case "6":
                    running = false;
                    break;
                default:
                    Console.WriteLine("Invalid choice. Press enter to continue.");
                    Console.ReadLine();
                    break;
            }
        }
    }

    static void DisplayScore()
    {
        int level = score / 1000 + 1;
        Console.WriteLine($"You have {score} points. You are level {level}!");
        if (level >= 2) Console.WriteLine("Keep going, you're on fire!");
        if (level >= 5) Console.WriteLine("Legendary quester!");
        if (badges.Count > 0)
        {
            Console.WriteLine("Badges earned:");
            foreach (var badge in badges)
            {
                Console.WriteLine($"- {badge}");
            }
        }
    }

    static void DisplayMenu()
    {
        Console.WriteLine("\nMenu:");
        Console.WriteLine("1. Display Goals");
        Console.WriteLine("2. Create New Goal");
        Console.WriteLine("3. Record Event");
        Console.WriteLine("4. Save Goals");
        Console.WriteLine("5. Load Goals");
        Console.WriteLine("6. Quit");
        Console.Write("Choose an option: ");
    }

    static void DisplayGoals()
    {
        if (goals.Count == 0)
        {
            Console.WriteLine("No goals yet.");
        }
        else
        {
            for (int i = 0; i < goals.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {goals[i].GetDisplayString()}");
            }
        }
        Console.WriteLine("Press enter to continue.");
        Console.ReadLine();
    }

    static void CreateGoal()
    {
        Console.WriteLine("What type of goal?");
        Console.WriteLine("1. Simple Goal");
        Console.WriteLine("2. Eternal Goal");
        Console.WriteLine("3. Checklist Goal");
        string type = Console.ReadLine();
        Console.Write("Enter goal name: ");
        string name = Console.ReadLine();
        Console.Write("Enter description: ");
        string desc = Console.ReadLine();
        Console.Write("Enter points: ");
        int points = int.Parse(Console.ReadLine());

        Goal goal = null;
        if (type == "1")
        {
            goal = new SimpleGoal(name, desc, points);
        }
        else if (type == "2")
        {
            goal = new EternalGoal(name, desc, points);
        }
        else if (type == "3")
        {
            Console.Write("Enter target count: ");
            int target = int.Parse(Console.ReadLine());
            Console.Write("Enter bonus points: ");
            int bonus = int.Parse(Console.ReadLine());
            goal = new ChecklistGoal(name, desc, points, target, bonus);
        }
        if (goal != null)
        {
            goals.Add(goal);
            Console.WriteLine("Goal created!");
        }
        else
        {
            Console.WriteLine("Invalid type.");
        }
        Console.WriteLine("Press enter to continue.");
        Console.ReadLine();
    }

    static void RecordEvent()
    {
        if (goals.Count == 0)
        {
            Console.WriteLine("No goals to record.");
            Console.ReadLine();
            return;
        }
        DisplayGoals();
        Console.Write("Enter the number of the goal to record: ");
        if (int.TryParse(Console.ReadLine(), out int index) && index >= 1 && index <= goals.Count)
        {
            Goal goal = goals[index - 1];
            bool wasComplete = goal.IsComplete();
            goal.RecordEvent();
            score += goal.GetPoints();
            if (goal is ChecklistGoal checklist && !wasComplete && goal.IsComplete())
            {
                score += checklist.GetBonusPoints();
                Console.WriteLine($"Bonus {checklist.GetBonusPoints()} points awarded!");
            }
            CheckBadges();
            Console.WriteLine("Event recorded!");
        }
        else
        {
            Console.WriteLine("Invalid choice.");
        }
        Console.WriteLine("Press enter to continue.");
        Console.ReadLine();
    }

    static void CheckBadges()
    {
        if (score >= 5000 && !badges.Contains("Apprentice Quester"))
        {
            badges.Add("Apprentice Quester");
            Console.WriteLine("Badge earned: Apprentice Quester!");
        }
        if (score >= 10000 && !badges.Contains("Master Quester"))
        {
            badges.Add("Master Quester");
            Console.WriteLine("Badge earned: Master Quester!");
        }
        // Add more if wanted
    }

    static void SaveGoals()
    {
        using (StreamWriter writer = new StreamWriter("goals.txt"))
        {
            writer.WriteLine(score);
            foreach (var badge in badges)
            {
                writer.WriteLine($"Badge:{badge}");
            }
            foreach (var goal in goals)
            {
                writer.WriteLine(goal.GetSaveString());
            }
        }
        Console.WriteLine("Goals saved!");
        Console.WriteLine("Press enter to continue.");
        Console.ReadLine();
    }

    static void LoadGoals()
    {
        if (!File.Exists("goals.txt"))
        {
            Console.WriteLine("No save file found.");
            Console.ReadLine();
            return;
        }
        goals.Clear();
        badges.Clear();
        using (StreamReader reader = new StreamReader("goals.txt"))
        {
            string line = reader.ReadLine();
            if (line != null) score = int.Parse(line);
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split(':');
                if (parts[0] == "Badge")
                {
                    badges.Add(parts[1]);
                }
                else if (parts[0] == "SimpleGoal")
                {
                    string name = parts[1];
                    string desc = parts[2];
                    int points = int.Parse(parts[3]);
                    bool complete = bool.Parse(parts[4]);
                    var goal = new SimpleGoal(name, desc, points);
                    if (complete) goal.RecordEvent();
                    goals.Add(goal);
                }
                else if (parts[0] == "EternalGoal")
                {
                    string name = parts[1];
                    string desc = parts[2];
                    int points = int.Parse(parts[3]);
                    goals.Add(new EternalGoal(name, desc, points));
                }
                else if (parts[0] == "ChecklistGoal")
                {
                    string name = parts[1];
                    string desc = parts[2];
                    int points = int.Parse(parts[3]);
                    bool complete = bool.Parse(parts[4]);
                    int target = int.Parse(parts[5]);
                    int current = int.Parse(parts[6]);
                    int bonus = int.Parse(parts[7]);
                    var goal = new ChecklistGoal(name, desc, points, target, bonus);
                    for (int i = 0; i < current; i++)
                    {
                        goal.RecordEvent();
                    }
                    goals.Add(goal);
                }
            }
        }
        Console.WriteLine("Goals loaded!");
        Console.WriteLine("Press enter to continue.");
        Console.ReadLine();
    }
}