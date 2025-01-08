using System;
using System.Linq;
using System.Text;
using static System.Environment;

namespace CsToMd
{
    public static class CommentStripper
    {
        /// <summary>
        /// If line contains these symbols theu will be removed from the output,
        /// otherwise the symbols will be stripped from line.
        /// </summary>
        public static readonly string[] StripSymbols = { @"/*md", @"md*/", @"//md" };

        public static readonly string CollapsibleSectionCommentBegin = "//md{";
        public static readonly string CollapsibleSectionCommentEnd = "//md}";
        public static readonly string CollapsibleSectionMarkdownBegin = @"<details><summary><strong>{0}</strong></summary>" + NewLine;
        public static readonly string CollapsibleSectionMarkdownEnd = @"</details>" + NewLine;

        public static StringBuilder StripMdComments(string[] inputLines, string[] removeLinesStartingWith = null)
        {
            var outputBuilder = new StringBuilder(inputLines.Length * 64);
            var lastLineIndex = inputLines.Length - 1;
            var hasLinesToRemove = removeLinesStartingWith != null && removeLinesStartingWith.Length > 0;

            var isInCollapsibleSection = false;

            var isPrevLineAppended = false;
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
                        // Strip md comments markers from the line. The result of a single stripped part means the line does not contain the md comments.
                        // If for the some reason we have an empty comment it does not matter and the empty part can be glued back without changing the source line.

                        // Trim the leading spaces, with result of indent being stripped too (see #16),
                        // if you want to preserve the indent, please add the spaces after the starting //md, or /*md comment

                        var strippedParts = line.TrimStart().Split(StripSymbols, StringSplitOptions.None);
                        if (strippedParts.Length != 1)
                        {
                            // By convention, the result empty lines are removed
                            line = strippedParts.All(p => string.IsNullOrWhiteSpace(p)) ? null : string.Concat(strippedParts);
                            if (line != null)
                            {
                                // Being smart and remove a single leading space, as it is unusual to have it as indent,
                                // check the IssueTests.Issue16_Ignore_leading_whitespace_before_md_comments for the example;
                                if (line.Length > 1 && line[0] == ' ' && line[1] != ' ')
                                    line = line.Substring(1);

                                // Trim the end spaces, I think this the expected behavior
                                line = line.TrimEnd();
                            }
                        }
                    }
                }

                if (line != null)
                {
                    // this logic is required to handle the last line,
                    // that may be not the actual last line, but the removed empty comment line,
                    // see IssueTests.Remove_empty_comment_at_the_end
                    if (isPrevLineAppended)
                        outputBuilder.AppendLine();
                    outputBuilder.Append(line);
                    isPrevLineAppended = true;
                }
            }

            return outputBuilder;
        }
    }
}