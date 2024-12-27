using System;
using System.Text;
using System.Text.RegularExpressions;

namespace KodeRunner
{
    public class TerminalCodeParser
    {
        private static readonly Regex AnsiCodeRegex = new Regex(@"\u001b\[([\d;]*)([A-Za-z])");

        /// <summary>
        /// Parses ANSI codes to Resonite format.
        /// </summary>
        /// <param name="input">The input string containing ANSI codes.</param>
        /// <returns>The parsed string in Resonite format.</returns>
        public static string ParseToResonite(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return AnsiCodeRegex.Replace(
                input,
                match =>
                {
                    var code = match.Groups[1].Value;
                    var command = match.Groups[2].Value;

                    return command switch
                    {
                        "m" => ParseColorCode(code),
                        _ => string.Empty,
                    };
                }
            );
        }

        private static string ParseColorCode(string code)
        {
            return code switch
            {
                "0" => "</color>", // Reset
                "30" => "<color=#000000>", // Black
                "31" => "<color=#FF0000>", // Red
                "32" => "<color=#00FF00>", // Green
                "33" => "<color=#FFFF00>", // Yellow
                "34" => "<color=#0000FF>", // Blue
                "35" => "<color=#FF00FF>", // Magenta
                "36" => "<color=#00FFFF>", // Cyan
                "37" => "<color=#FFFFFF>", // White
                "1" => "<b>", // Bold
                "22" => "</b>", // Reset bold
                "C" => " ", // space
                _ => string.Empty,
            };
        }
    }
}
