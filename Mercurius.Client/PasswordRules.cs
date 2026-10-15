namespace Mercurius.Client
{
    // Mirrors the Identity password options in the server's Program.cs; the server still enforces
    // them and its message wins if they ever drift.
    public static class PasswordRules
    {
        public const string Hint = "At least 8 characters, with an upper- and lower-case letter, a digit and a symbol.";
    }
}
