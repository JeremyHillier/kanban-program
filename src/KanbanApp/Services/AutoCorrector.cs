namespace KanbanApp.Services;

// Auto-correct for the task box and Notes: when a word is finished (a space or punctuation is
// typed after it) and it is a known slip, it is swapped for the right word. Pure, so the rules are
// unit-tested; Views/AutoCorrectBox does the typing side. Only whole words made of letters are
// touched, never one inside a web address, e-mail address, path or number.
public static class AutoCorrector
{
    // Typical slips only: every right-hand side is a real word, and no left-hand side is one.
    public static readonly IReadOnlyDictionary<string, string> BuiltIn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["teh"] = "the", ["hte"] = "the", ["adn"] = "and", ["nad"] = "and", ["taht"] = "that", ["thta"] = "that",
        ["wiht"] = "with", ["wtih"] = "with", ["waht"] = "what", ["whta"] = "what", ["yuo"] = "you", ["yoru"] = "your",
        ["jsut"] = "just", ["cna"] = "can", ["ahve"] = "have", ["hvae"] = "have",
        ["thier"] = "their", ["theri"] = "their", ["becuase"] = "because", ["becasue"] = "because", ["beacuse"] = "because",
        ["recieve"] = "receive", ["recieved"] = "received", ["reciept"] = "receipt", ["freind"] = "friend", ["wierd"] = "weird",
        ["seperate"] = "separate", ["definately"] = "definitely", ["definatly"] = "definitely", ["occured"] = "occurred",
        ["occuring"] = "occurring", ["untill"] = "until", ["alot"] = "a lot", ["pleae"] = "please",
        ["plase"] = "please", ["tomorow"] = "tomorrow", ["tommorow"] = "tomorrow", ["tommorrow"] = "tomorrow",
        ["tomorrw"] = "tomorrow", ["wensday"] = "Wednesday", ["wednsday"] = "Wednesday", ["thrusday"] = "Thursday",
        ["febuary"] = "February", ["calender"] = "calendar", ["scedule"] = "schedule", ["shedule"] = "schedule",
        ["meetting"] = "meeting", ["meating"] = "meeting", ["appointmnet"] = "appointment", ["apointment"] = "appointment",
        ["buisness"] = "business", ["bussiness"] = "business", ["acount"] = "account", ["accout"] = "account",
        ["adress"] = "address", ["addres"] = "address", ["inovice"] = "invoice", ["invoce"] = "invoice",
        ["proect"] = "project", ["porject"] = "project", ["projcet"] = "project", ["documnet"] = "document",
        ["docuemnt"] = "document", ["infomation"] = "information", ["informaiton"] = "information",
        ["managment"] = "management", ["enviroment"] = "environment", ["goverment"] = "government",
        ["sucessful"] = "successful", ["succesful"] = "successful", ["sucess"] = "success", ["neccessary"] = "necessary",
        ["necesary"] = "necessary", ["accomodate"] = "accommodate", ["embarass"] = "embarrass", ["existance"] = "existence",
        ["foward"] = "forward", ["follwing"] = "following", ["folowing"] = "following", ["beleive"] = "believe",
        ["acheive"] = "achieve", ["wich"] = "which", ["whihc"] = "which", ["woudl"] = "would", ["shoudl"] = "should",
        ["coudl"] = "could", ["abotu"] = "about", ["aobut"] = "about", ["agian"] = "again", ["anohter"] = "another",
        ["probaly"] = "probably", ["probly"] = "probably", ["realy"] = "really", ["actualy"] = "actually",
        ["i"] = "I", ["im"] = "I'm", ["ive"] = "I've", ["dont"] = "don't", ["doesnt"] = "doesn't", ["didnt"] = "didn't",
        ["cant"] = "can't", ["wont"] = "won't", ["isnt"] = "isn't", ["wasnt"] = "wasn't", ["couldnt"] = "couldn't",
        ["shouldnt"] = "shouldn't", ["wouldnt"] = "wouldn't", ["havent"] = "haven't", ["thats"] = "that's"
    };

    public static bool IsDelimiter(char c) => c is ' ' or '\t' or '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '"';

    private static bool IsWordChar(char c) => char.IsLetter(c) || c == '\'' || c == '’';

    // "wrong = right" lines (also "wrong=right"); blanks and anything that isn't one plain word on
    // the left are skipped. Later lines win over earlier ones.
    public static Dictionary<string, string> ParseEntries(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var at = raw.IndexOf('=');
            if (at <= 0) continue;

            var wrong = raw[..at].Trim();
            var right = raw[(at + 1)..].Trim();
            if (wrong.Length == 0 || right.Length == 0 || !wrong.All(IsWordChar) || wrong.Equals(right, StringComparison.Ordinal)) continue;
            result[wrong] = right;
        }
        return result;
    }

    // The word that ends exactly at caret, if it is a known slip. start/length say what to replace.
    public static bool TryCorrect(string text, int caret, IReadOnlyDictionary<string, string>? own,
        out int start, out int length, out string replacement)
    {
        start = length = 0;
        replacement = string.Empty;
        if (caret <= 0 || caret > text.Length) return false;

        var begin = caret;
        while (begin > 0 && IsWordChar(text[begin - 1])) begin--;
        while (begin < caret && (text[begin] == '\'' || text[begin] == '’')) begin++; // a quote mark isn't part of the word
        if (begin == caret) return false;

        // Part of an address, path, number or identifier rather than a word on its own.
        if (begin > 0 && (char.IsDigit(text[begin - 1]) || "./\\@_-#$%".Contains(text[begin - 1]))) return false;

        var word = text[begin..caret];
        var key = word.Replace('’', '\'');
        string? fix = null;
        if (own is not null && own.TryGetValue(key, out var mine)) fix = mine;
        else if (BuiltIn.TryGetValue(key, out var builtIn)) fix = builtIn;
        if (fix is null) return false;

        replacement = MatchCase(word, fix);
        if (replacement == word) return false;
        start = begin;
        length = word.Length;
        return true;
    }

    // "Teh" -> "The", "TEH" -> "THE", "teh" -> "the"; a right-hand side with its own capitals
    // ("I'm", "Wednesday") is kept as written.
    private static string MatchCase(string typed, string fix)
    {
        if (fix.Any(char.IsUpper)) return fix;
        if (typed.Length > 1 && typed.All(c => !char.IsLetter(c) || char.IsUpper(c))) return fix.ToUpperInvariant();
        if (char.IsUpper(typed[0])) return char.ToUpperInvariant(fix[0]) + fix[1..];
        return fix;
    }
}
