using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello Develop02 World!");

        resume myResume = new resume();
        myResume._name = "James";
        jobs job1 = new jobs("2022-05-22, Rock Climbing Instructor", "2026-01-27");

        myResume._jobs.Add(job1);

        jobs job2
            = new jobs("2020-06-15", "Software Developer", "2022-05-20");
        myResume._jobs.Add(job2);

        education edu1 = new education();
        edu1._school = "State University";
        myResume._experience = new List<education>();
        myResume._experience.Add(edu1);
    }
}