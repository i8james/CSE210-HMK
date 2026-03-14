using System;

public class SimpleGoal : Goal
{
    public SimpleGoal(string name, string description, int points) : base(name, description, points) {}

    public override void RecordEvent()
    {
        _isComplete = true;
    }

    public override string GetDisplayString()
    {
        return $"[{(IsComplete() ? "X" : " ")}] {_name} ({_description})";
    }
}