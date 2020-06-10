using System;
using System.Linq;
using System.Text;

namespace CsToMd
{
    public static class CommentStripper
    {
        public static StringBuilder StripMdComments(string[] inputLines)
        {
            var outputBuilder = new StringBuilder(inputLines.Length * 20);
            var lastLineIndex = inputLines.Length - 1;
            for (var i = 0; i < inputLines.Length; i++)
            {
                var line = inputLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(CsToMd.StripSymbols, StringSplitOptions.None);
                    if (parts.Length != 1)
                        line = parts.Any(s => !string.IsNullOrWhiteSpace(s)) ? string.Concat(parts) : null;
                }

                if (line != null)
                {
                    if (i < lastLineIndex)
                        outputBuilder.AppendLine(line);
                    else
                        outputBuilder.Append(line);
                }
            }

            return outputBuilder;
        }
    }
}