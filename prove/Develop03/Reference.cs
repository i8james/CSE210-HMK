using System;

public class Reference
{
    private string _book;
    private int _chapter;
    private int _startVerse;
    private int? _endVerse;

    // Single-verse constructor
    public Reference(string book, int chapter, int verse)
    {
        _book = book ?? throw new ArgumentNullException(nameof(book));
        _chapter = chapter;
        _startVerse = verse;
        _endVerse = null;
    }

    // Verse-range constructor
    public Reference(string book, int chapter, int startVerse, int endVerse)
    {
        _book = book ?? throw new ArgumentNullException(nameof(book));
        _chapter = chapter;
        _startVerse = startVerse;
        _endVerse = endVerse;
    }

    public override string ToString()
    {
        if (_endVerse.HasValue)
        {
            return $"{_book} {_chapter}:{_startVerse}-{_endVerse.Value}";
        }
        return $"{_book} {_chapter}:{_startVerse}";
    }
}