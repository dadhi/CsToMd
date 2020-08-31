using System;
using System.Linq;
using System.Text;
using static System.Environment;

namespace CsToMd
{
    public static class CommentStripper
    {
        /// <summary>
        /// If line contains these symbols only it will be removed from out put, otherwise it the symbols will be stripped from line.
        /// </summary>
        public static readonly string[] StripSymbols = { @"/*md", @"md*/", @"//md" };

        public static readonly string CollapsibleSectionCommentBegin = "//md{";
        public static readonly string CollapsibleSectionCommentEnd   = "//md}";
        public static readonly string CollapsibleSectionMarkdownBegin = @"<details><summary><strong>{0}</strong></summary>" + NewLine;
        public static readonly string CollapsibleSectionMarkdownEnd   = @"</details>" + NewLine;

        public static StringBuilder StripMdComments(string[] inputLines, string[] removeLinesStartingWith = null)
        {
            var outputBuilder = new StringBuilder(inputLines.Length * 20);
            var lastLineIndex = inputLines.Length - 1;
            var hasLinesToRemove = removeLinesStartingWith != null && removeLinesStartingWith.Length > 0;

            var isInCollapsibleSection = false; 

            for (var i = 0; i < inputLines.Length; i++)
            {
                var line = inputLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    if (hasLinesToRemove) 
                    {
                        var removeLine = false;
                        for (var j = 0; !removeLine && j < removeLinesStartingWith.Length; j++)
                        {
                            var lineStartingWith = removeLinesStartingWith[j];
                            if (!string.IsNullOrWhiteSpace(lineStartingWith) &&
                                line.StartsWith(lineStartingWith))
                                removeLine = true;
                        }
                        if (removeLine)
                            continue;
                    }

                    if (!isInCollapsibleSection && (isInCollapsibleSection = line.StartsWith(CollapsibleSectionCommentBegin)))
                    {
                        line = string.Format(CollapsibleSectionMarkdownBegin, line.Substring(CollapsibleSectionCommentBegin.Length).Trim());
                    }
                    else if (isInCollapsibleSection && line.StartsWith(CollapsibleSectionCommentEnd))
                    {
                        line = CollapsibleSectionMarkdownEnd;
                        isInCollapsibleSection = false;
                    }
                    else 
                    {
                        var strippedParts = line.Split(StripSymbols, StringSplitOptions.None); // todo: @check use the StringSplitOptions.RemoveEmptyEntries 
                        if (strippedParts.Length != 1)
                            line = strippedParts.Any(s => !string.IsNullOrWhiteSpace(s)) ? string.Concat(strippedParts) : null;
                    }
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