public class jobs
{
    private string _startDate;
    private string _title;
    private string _endDate;

    public jobs(string startDate, string title, string endDate)
    {
        _startDate = startDate;
        _title = title;
        _endDate = endDate;
    }

    public string GetStartDate()
    {
        return _startDate;
    }

    public void SetStartDate(string startDate)
    {
        _startDate = startDate;
    }

    public string GetTitle()
    {
        return _title;
    }

    public void SetTitle(string title)
    {
        _title = title;
    }

    public string GetEndDate()
    {
        return _endDate;
    }

    public void SetEndDate(string endDate)
    {
        _endDate = endDate;
    }
    public string GetDescription()
    {
        return $"{_title} ({_startDate} - {_endDate})";
    }
}