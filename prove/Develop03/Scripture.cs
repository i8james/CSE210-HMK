using System;
using System.Collections.Generic;
using System.Linq;

public class Scripture
{
    private Reference _reference;
    private List<Word> _words;
    private static readonly Random _random = new Random();

    public Scripture(Reference reference, string text)
    {
        _reference = reference ?? throw new ArgumentNullException(nameof(reference));
        text ??= string.Empty;
        _words = text.Split(' ').Select(t => new Word(t)).ToList();
    }

    public void Display()
    {
        Console.WriteLine(_reference.ToString());
        Console.WriteLine();
        Console.WriteLine(string.Join(" ", _words.Select(w => w.Display())));
    }

    public bool IsCompletelyHidden() => _words.All(w => w.IsHidden);

    // Hide up to `count` random words that are not already hidden. Returns how many were hidden.
    public int HideRandomWords(int count = 3)
    {
        var candidates = _words.Where(w => !w.IsHidden).ToList();
        if (candidates.Count == 0) return 0;

        int toHide = Math.Min(count, candidates.Count);
        for (int i = 0; i < toHide; i++)
        {
            int idx = _random.Next(candidates.Count);
            candidates[idx].Hide();
            candidates.RemoveAt(idx);
        }

        return toHide;
    }
}