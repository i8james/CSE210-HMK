using System;

public class EternalGoal : Goal
{
    public EternalGoal(string name, string description, int points) : base(name, description, points) {}

    public override void RecordEvent()
    {
        // Eternal goals are never complete, but points are awarded each time
    }

    public override string GetDisplayString()
    {
        return $"[ ] {_name} ({_description}) - Eternal";
    }
}