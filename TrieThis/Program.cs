using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

string splitter = @"(?:[^\w]|\d|_)+";
string splitterWithoutCursor = @"(?:[^\w]|\d)+";

Measure(
    "Read translations file",
    () =>
    {
        XmlDocument xmlDocument = new XmlDocument();
        xmlDocument.Load(@"C:\Users\ernjus\Desktop\BC21\Translations\Base Application.lt-LT.xlf");
        XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(xmlDocument.NameTable);
        xmlNamespaceManager.AddNamespace("xlf", "urn:oasis:names:tc:xliff:document:1.2");
        XmlNodeList xmlNodeList = xmlDocument.SelectNodes("xlf:xliff/xlf:file/xlf:body/xlf:group/xlf:trans-unit/xlf:target/text()", xmlNamespaceManager)!;
        HashSet<string> translations = new();
        foreach (XmlNode xmlNode in xmlNodeList)
        {
            string wordsText = Regex.Replace(xmlNode.InnerText, splitter, "|").Trim('|').ToLowerInvariant();
            if (wordsText != "")
                translations.Add(wordsText);
        }
        return translations.ToArray()!;
    },
    out string[] translations
);

Measure(
    "Split translations to words",
    () => translations.Select(x => x.Split('|').ToArray()).Where(x => x.Length > 0).ToArray(),
    out string[][] words
);

Measure(
    "Build next word suggestions",
    () =>
    {
        SortedDictionary<string, SortedSet<string>> nextWords = new();
        foreach (string[] translationWords in words)
            for (int i = 1; i < translationWords.Length; i++)
                if (translationWords[i - 1].Length > 1 && translationWords[i].Length > 1)
                {
                    if (!nextWords.TryGetValue(translationWords[i - 1], out SortedSet<string>? suggestions))
                    {
                        suggestions = new();
                        nextWords.Add(translationWords[i - 1], suggestions);
                    }
                    suggestions.Add(translationWords[i]);
                }

        return nextWords;
    },
    out SortedDictionary<string, SortedSet<string>> nextWords
);

Measure(
    "Build trie",
    () =>
    {
        Trie trie = new();
        foreach (string[] translationWords in words)
            foreach (string word in translationWords)
                trie.Put(word);
        return trie;
    },
    out Trie trie
);

/*
Console.WriteLine();
foreach (var entry in nextWords)
{
    Console.WriteLine($"Next words for \"{entry.Key}\": {string.Join('|', entry.Value)}");
}
*/

// The underscore marks the cursor position.
MeasureAutocomplete("_");
MeasureAutocomplete(" _ ");
MeasureAutocomplete("XZZZZZZZZZZZZZZZZZZZY _ XZZZZZZZZZZZZZZZZZZZZZZZY");
MeasureAutocomplete("dok_");
MeasureAutocomplete("_dok");
MeasureAutocomplete("_Dok");
MeasureAutocomplete("Cutting onions on iPad_.");
MeasureAutocomplete("Cutting onions ON iPad_.");
MeasureAutocomplete("Dokumento _");
MeasureAutocomplete("Dokumento Nr_");
MeasureAutocomplete("PREKĖS Nr_");
MeasureAutocomplete("PREKĖS N_");
MeasureAutocomplete("asdasdsda PREKĖS N_ aįėęįšėęšėwq");

void Autocomplete(string text)
{
    Console.WriteLine();
    Console.WriteLine();
    Console.Write($"Completions for \"{text}\"");

    string[] words = Regex.Split(text, splitterWithoutCursor).ToArray();

    string currentWord = words.First(x => x.Contains("_"));
    int currentWordIndex = Array.IndexOf(words, currentWord);
    currentWord = currentWord.Replace("_", "");

    string previousWord = currentWordIndex > 0 ? words[currentWordIndex - 1] : "";

    Case previousWordCase = GetWordCase(previousWord);
    Case currentWordCase = GetWordCase(currentWord);
    Case @case = previousWordCase;
    if (currentWord.Length >= 2)
        @case = currentWordCase;
    else if (currentWord.Length > previousWord.Length)
        @case = currentWordCase;

    IEnumerable<string> completions = new List<string>();
    if (previousWord != "" && nextWords.TryGetValue(previousWord.ToLowerInvariant(), out SortedSet<string>? suggestions))
    {
        Trie suggestionTrie = new();
        foreach (string word in suggestions)
            suggestionTrie.Put(word);
        completions = completions.Concat(suggestionTrie.StartsWith(currentWord.ToLowerInvariant()).Select(x => x + " (based on the previous word)"));
    }
    completions = completions.Concat(trie.StartsWith(currentWord.ToLowerInvariant()).Select(x => x + " (based on the current word)"));

    Console.WriteLine($" (found {completions.Count()} completions):");
    int maxCompletions = 5;
    foreach (string completion in completions.Take(maxCompletions))
        Console.WriteLine("  " + RewriteWord(completion, @case));
    if (completions.Count() > maxCompletions)
        Console.WriteLine("  ...");
}

Case GetWordCase(string word)
{
    if (word.Length >= 2 && char.IsUpper(word[1]))
        return Case.UpperCase;
    if (word.Length >= 1 && char.IsUpper(word[0]))
        return Case.TitleCase;
    return Case.LowerCase;
}

string RewriteWord(string word, Case @case)
{
    switch (@case)
    {
        case Case.UpperCase:
            return word.ToUpper();
        case Case.TitleCase:
            return word.Substring(0, 1).ToUpper() + word.Substring(1);
    }
    return word;
}

void MeasureAutocomplete(string text)
{
    Measure(
        $"Autocomplete \"{text}\"",
        () =>
        {
            Autocomplete(text);
            return 0;
        },
        out _
    );
}

void Measure<T>(string name, Func<T> operation, out T result)
{
    DateTime start = DateTime.Now;
    result = operation();
    DateTime end = DateTime.Now;
    TimeSpan duration = end - start;
    Console.WriteLine($"<{name}> took {duration.TotalSeconds:0.00} seconds.");
}

enum Case
{
    LowerCase,
    UpperCase,
    TitleCase
}