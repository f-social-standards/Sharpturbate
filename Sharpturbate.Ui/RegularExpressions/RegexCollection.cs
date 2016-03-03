namespace Sharpturbate.Ui.RegularExpressions
{
    public static class RegexCollection
    {
        public static readonly string Base64 =
            "^([A-Za-z0-9+/]{4})*([A-Za-z0-9+/]{4}|[A-Za-z0-9+/]{3}=|[A-Za-z0-9+/]{2}==)$";

        public static readonly string NewLine = "\r\n|\r|\n";
        public static readonly string ChaturbateUrl = @"(http|https):(\/\/chaturbate.com\/)[a-zA-Z0-9._-]+(\/?)";
    }
}