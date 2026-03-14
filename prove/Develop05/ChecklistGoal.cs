using System;

public class ChecklistGoal : Goal
{
    private int _targetCount;
    private int _currentCount;
    private int _bonusPoints;

    public ChecklistGoal(string name, string description, int points, int targetCount, int bonusPoints) : base(name, description, points)
    {
        _targetCount = targetCount;
        _bonusPoints = bonusPoints;
        _currentCount = 0;
    }

    public override void RecordEvent()
    {
        _currentCount++;
        if (_currentCount >= _targetCount)
        {
            _isComplete = true;
        }
    }

    public int GetBonusPoints() => _bonusPoints;
    public int GetCurrentCount() => _currentCount;
    public int GetTargetCount() => _targetCount;

    public override string GetDisplayString()
    {
        return $"[{(IsComplete() ? "X" : " ")}] {_name} ({_description}) - Completed {_currentCount}/{_targetCount}";
    }

    public override string GetSaveString()
    {
        return $"{GetType().Name}:{_name}:{_description}:{_points}:{_isComplete}:{_targetCount}:{_currentCount}:{_bonusPoints}";
    }
}