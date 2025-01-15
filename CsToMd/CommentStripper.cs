using System;
using System.Linq;
using System.Text;
using static System.Environment;

namespace CsToMd
{
    public static class CommentStripper
    {
        public static readonly string MdCommentLabel = "md";

        public static readonly string MdLineComment = "//" + MdCommentLabel;
        public static readonly string MdMultiLineCommentStart = "/*" + MdCommentLabel;
        public static readonly string MdMultiLineCommentEnd = MdCommentLabel + "*/";

        /// <summary>
        /// If line contains these symbols, they will be removed from the output
        /// </summary>
        public static readonly string[] MdComments = { MdLineComment, MdMultiLineCommentStart, MdMultiLineCommentEnd };

        public static readonly string CollapsibleSectionCommentBegin = "//md{";
        public static readonly string CollapsibleSectionCommentEnd = "//md}";
        public static readonly string CollapsibleSectionMarkdownBegin = @"<details><summary><strong>{0}</strong></summary>" + NewLine;
        public static readonly string CollapsibleSectionMarkdownEnd = @"</details>" + NewLine;

        public static readonly string CodeFenceLang = "code:";
        public static readonly string CodeFence = "```";

        enum Area { Code = 0, LineComment, MultiLineComment }

        public static StringBuilder StripMdComments(string[] inputLines, string[] removeLinesStartingWith = null)
        {
            var outputBuilder = new StringBuilder(inputLines.Length * 64);
            var newLineBuilder = new StringBuilder(64);
            var area = Area.Code;
            var isMultiLineMdComment = false;
            for (var i = 0; i < inputLines.Length; i++)
            {
                var line = inputLines[i];

                // The new line automatically starts the new code if the previous one was a line comment
                if (area == Area.LineComment)
                    area = Area.Code;

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (outputBuilder.Length != 0) // append the newline after the prev output
                        outputBuilder.AppendLine();
                    continue;
                }

                // Given that the length of the valid comment is at least 2 chars, we don't even look into the shorter lines
                var contentStart = 0;
                int chars;
                for (var chi = 0; chi + 1 < line.Length; chi += chars)
                {
                    chars = 1; // by default step by the single char
                    var mdCommentInLine = false;
                    switch (area)
                    {
                        case Area.Code:
                            if (line[chi] == '/')
                            {
                                if (line[chi + 1] == '/')
                                {
                                    // There may be the line comment inside the multiline comment, and it is ignored by C# and us 
                                    area = Area.LineComment;
                                    mdCommentInLine = chi + 3 < line.Length && line[chi + 2] == 'm' && line[chi + 3] == 'd';
                                    chars = mdCommentInLine ? 4 : 2; // at least skip the comment
                                }
                                else if (line[chi + 1] == '*')
                                {
                                    // If there was a code before then we expect the opening comment, because it may be a situation
                                    // like `hey*/*md you md*/` denoting either closing or opening comment depending on the context
                                    area = Area.MultiLineComment;
                                    mdCommentInLine = chi + 3 < line.Length && line[chi + 2] == 'm' && line[chi + 3] == 'd';
                                    isMultiLineMdComment = mdCommentInLine;
                                    chars = mdCommentInLine ? 4 : 2; // at least skip the comment
                                }
                            }
                            break;
                        case Area.MultiLineComment:
                            if (isMultiLineMdComment
                                && line[chi] == 'm' && line[chi + 1] == 'd'
                                && chi + 3 < line.Length && line[chi + 2] == '*' && line[chi + 3] == '/')
                            {
                                mdCommentInLine = true;
                                isMultiLineMdComment = false;
                                area = Area.Code;
                                chars = 4;
                            }
                            else if (line[chi] == '*' & line[chi + 1] == '/')
                            {
                                mdCommentInLine = isMultiLineMdComment; // depending on the opening comment it may be an md as well
                                isMultiLineMdComment = false;
                                area = Area.Code;
                                chars = 2;
                            }
                            break;
                        case Area.LineComment:
                            // ignore everything inside the line comment
                            break;
                    }

                    // Strip the md comment from the output
                    if (mdCommentInLine)
                    {
                        // take into account that j is referring to the start of the comment + 1 at this moment
                        var stripped = line.Substring(contentStart, chi - contentStart);

                        // Trim the leading spaces, with result of indent being stripped too (see #16),
                        // if you want to preserve the indent, please add the spaces after the starting //md, or /*md comment
                        if (contentStart == 0)
                            stripped = stripped.TrimStart();

                        if (stripped.Length != 0)
                            newLineBuilder.Append(stripped);

                        contentStart = chi + chars;
                    }
                }

                // add the strip at the end to the output
                if (contentStart > 0 && contentStart < line.Length)
                    newLineBuilder.Append(line.Substring(contentStart));

                if (newLineBuilder.Length == 0)
                {
                    // Сheck if the line is not consist of the md comment entirely,
                    // If so the remaining empty line is removed (is not appended to the output)
                    if (contentStart == 0)
                        (outputBuilder.Length != 0 ? outputBuilder.AppendLine() : outputBuilder).Append(line);
                }
                else
                {
                    // Being smart here and removing the first space for the odd number of spaces in the leading indent,
                    // check the IssueTests.Issue16_Ignore_leading_whitespace_before_md_comments for the example
                    if (newLineBuilder.Length > 1 && newLineBuilder[0] == ' ')
                    {
                        var spaces = 1;
                        while (spaces < newLineBuilder.Length && newLineBuilder[spaces] == ' ') ++spaces;
                        if (spaces % 2 == 1)
                            newLineBuilder.Remove(0, 1);
                    }

                    // I think this is an expected cleaning behavior to remove the dangling spaces at the end of the line
                    var newLine = newLineBuilder.ToString().TrimEnd();
                    if (newLine.Length != 0)
                        (outputBuilder.Length != 0 ? outputBuilder.AppendLine() : outputBuilder).Append(newLine);
                    newLineBuilder.Clear();
                }
            }

            return outputBuilder;
        }

        public static StringBuilder StripMdComments2(string[] inputLines, string[] removeLinesStartingWith = null)
        {
            var outputBuilder = new StringBuilder(inputLines.Length * 64);
            var lastLineIndex = inputLines.Length - 1;
            var hasLinesToRemove = removeLinesStartingWith != null && removeLinesStartingWith.Length > 0;

            var isInCollapsibleSection = false;
            string codeFenceLang = null;
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

                        var noLeadingSpaceLine = line.TrimStart();
                        if (noLeadingSpaceLine.StartsWith(CodeFenceLang))
                        {
                            codeFenceLang = noLeadingSpaceLine.Substring(CodeFenceLang.Length).Trim();
                            line = null; // means to remove the line with code fence from the output
                        }

                        var partsAroundComments = noLeadingSpaceLine.Split(MdComments, StringSplitOptions.None);
                        if (partsAroundComments.Length != 1)
                        {
                            // By convention, the result empty lines are removed
                            line = partsAroundComments.All(p => string.IsNullOrWhiteSpace(p)) ? null : string.Concat(partsAroundComments);
                            if (line != null)
                            {
                                // Trim the end spaces, I think this the expected behavior
                                line = line.TrimEnd();

                                // Being smart here and remove the first space for the odd number of spaces in the leading indent,
                                // check the IssueTests.Issue16_Ignore_leading_whitespace_before_md_comments for the example;
                                if (line.Length > 1 && line[0] == ' ')
                                {
                                    var spaces = 1;
                                    while (spaces < line.Length && line[spaces] == ' ') ++spaces;
                                    if (spaces % 2 == 1)
                                        line = line.Substring(1);
                                }
                            }
                        }
                    }
                }

                if (line != null)
                {
                    // this logic is required to handle the last line,
                    // that may be not the actual last line, but the removed empty comment line,
                    // see IssueTests.Remove_empty_comment_at_the_end
                    if (outputBuilder.Length > 0)
                        outputBuilder.AppendLine();
                    outputBuilder.Append(line);
                }
            }

            return outputBuilder;
        }
    }
}