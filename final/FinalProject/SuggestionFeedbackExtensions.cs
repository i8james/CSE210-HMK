using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

internal static class SuggestionFeedbackExtensions
{
    public static IEnumerable<string> NormalizeNames(this IEnumerable<string> names)
    {
        return names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim());
    }
}
