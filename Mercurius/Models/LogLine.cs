namespace Mercurius.Models;

public class LogLine
{
    public string Raw { get; set; } = "";
    public bool IsError, IsWarning, IsInfo, IsDebug;
}
