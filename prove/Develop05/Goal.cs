using System;

public abstract class Goal
{
    protected string _name;
    protected string _description;
    protected int _points;
    protected bool _isComplete;

    public Goal(string name, string description, int points)
    {
        _name = name;
        _description = description;
        _points = points;
        _isComplete = false;
    }

    public string GetName() => _name;
    public string GetDescription() => _description;
    public int GetPoints() => _points;
    public bool IsComplete() => _isComplete;

    public abstract void RecordEvent();
    public abstract string GetDisplayString();

    public virtual string GetSaveString()
    {
        return $"{GetType().Name}:{_name}:{_description}:{_points}:{_isComplete}";
    }
}