using System.Text.RegularExpressions;

namespace DiffToJsonLib.Redactors;

public partial class RegexPiiRedactor : IPiiRedactor
{
    private readonly Regex _regex;
    
    public RegexPiiRedactor()
    {
        _regex = MyRegex();
    }
    
    [GeneratedRegex(@"<([^>\s]+@[^>\s]+\.[^>\s]+)>|\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
    
    public string RedactPii(string input)
    {
        return  _regex.Replace(input, "REDACTED");
    }
}