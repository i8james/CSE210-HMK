using System;
using System.Linq;

public class Word
{
    private string _text;
    private bool _hidden;

    public Word(string text)
    {
        _text = text ?? string.Empty;
        _hidden = false;
    }

    public bool IsHidden => _hidden;

    public void Hide() => _hidden = true;

    // When hidden, replace letters/digits with underscores but preserve punctuation
    public string Display()
    {
        if (!_hidden) return _text;

        var chars = _text.Select(c => Char.IsLetterOrDigit(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}