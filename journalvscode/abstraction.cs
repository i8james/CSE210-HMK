//Start
//↓
//Display menu
//↓
//User chooses:
//  1. Write entry → get prompt → get response → create Entry → add to Journal
//  2. Display entries → Journal.DisplayAll()
//  3. Save → ask filename → Journal.SaveToFile()
//  4. Load → ask filename → Journal.LoadFromFile()
//    5. Quit
//↓
//Loop until quit

using System;
using System.Collections.Generic;
using System.IO;
// Ensure the journal saves to a specific file
class Entry
{
    public string Prompt { get; set; }
    public string Response { get; set; }
    public string Date { get; set; }

    public Entry(string prompt, string response)
    {
        Prompt = prompt;
        Response = response;
        Date = DateTime.Now.ToString("yyyy-MM-dd");
    }

    public override string ToString()
    {
        return $"captains log ({Date}): {Prompt} - {Response}";
    }
}

class Journal
{
    private List<Entry> entries = new List<Entry>();

    public void AddEntry(Entry entry)
    {
        entries.Add(entry);
    }

    public void DisplayAll()
    {
        foreach (var entry in entries)
        {
            Console.WriteLine(entry.ToString());
        }
    }

    public void SaveToFile(string filename)
    {
        using (StreamWriter writer = new StreamWriter(filename))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                writer.WriteLine($"captains log ({i + 1}): {entries[i].Prompt} - {entries[i].Response}");
            }
        }
    }

    public void LoadFromFile(string filename)
    {
        entries.Clear();
        if (File.Exists(filename))
        {
            foreach (string line in File.ReadAllLines(filename))
            {
                Console.WriteLine(line);
            }
        }
    }
}

class Program
{
    static void Main()
    {
        Journal journal = new Journal();
        bool running = true;

        while (running)
        {
            Console.WriteLine("\n1. Write entry\n2. Display entries\n3. Save\n4. Load\n5. Quit");
            Console.Write("Choose: ");
            string choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    Console.Write("Enter prompt: ");
                    string prompt = Console.ReadLine();
                    Console.Write("Your response: ");
                    string response = Console.ReadLine();
                    journal.AddEntry(new Entry(prompt, response));
                    break;
                case "2":
                    journal.DisplayAll();
                    break;
                case "3":
                    Console.Write("Filename: ");
                    journal.SaveToFile(Console.ReadLine());
                    break;
                case "4":
                    Console.Write("Filename: ");
                    journal.LoadFromFile(Console.ReadLine());
                    break;
                case "5":
                    running = false;
                    break;
            }
        }
    }
}