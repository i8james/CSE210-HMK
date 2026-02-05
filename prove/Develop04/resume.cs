public class resume
{
    public string _name;
    public list<jobs> _jobs = new list<jobs>();
    public list<education> _experience;

    public void DisplayFullResume()
    {
        Console.WriteLine($"Name: {_name}");
        Console.WriteLine("Jobs:");
        foreach (jobs job in _jobs)
        {
            job.GetDescription();
            Console.WriteLine($"  {desc}");
        }
        Console.WriteLine("Education:");
        foreach (education education in _experience)
        {
            Console.WriteLine(education.GetDescription());
        }
    }
}