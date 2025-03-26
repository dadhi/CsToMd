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

        /// <summary>If line contains these symbols, they will be removed from the output</summary>
        public static readonly string[] MdComments = { MdLineComment, MdMultiLineCommentStart, MdMultiLineCommentEnd };

        public static readonly string CollapsibleSectionCommentBegin = "//md{";
        public static readonly string CollapsibleSectionCommentEnd = "//md}";
        public static readonly string CollapsibleSectionMarkdownBegin = @"<details><summary><strong>{0}</strong></summary>" + NewLine;
        public static readonly string CollapsibleSectionMarkdownEnd = @"</details>";

        public static readonly string CodeFenceLang = "code:";
        public static readonly string CodeFence = "```";

        enum Area { Code = 0, LineComment, MultiLineComment }

        public static StringBuilder StripMdComments(string[] inputLines, string[] removeLinesStartingWith = null)
        {
            var output = new StringBuilder(inputLines.Length * 64);
            var outputLine = new StringBuilder(64);
            var area = Area.Code;
            var isMultiLineMdComment = false;

            var isInCollapsibleSection = false;

            var isInCodeFence = false; // tracking that the parsed character is inside of the code fence
            var isAutoInsertCodeFence = false;
            var currentCodeFenceLang = ReadOnlySpan<char>.Empty;

            for (var i = 0; i < inputLines.Length; i++)
            {
                var line = inputLines[i];

                // The new line automatically starts the new code if the previous one was a line comment
                if (area == Area.LineComment)
                    area = Area.Code; // todo: @wip do we actually know that yet??r

                if (string.IsNullOrWhiteSpace(line))
                {
                    output.AppendNewLineAfterContent();
                    continue;
                }

                // Given that the length of the valid comment is at least 2 chars, we don't even look into the shorter lines
                var contentStart = 0;
                int parsedCharCount;
                for (var chi = 0; chi + 1 < line.Length; chi += parsedCharCount)
                {
                    parsedCharCount = 1; // by default step by the single char
                    var stripMdMarker = false;
                    switch (area)
                    {
                        case Area.Code:
                            if (line[chi] == '/')
                            {
                                if (line[chi + 1] == '/')
                                {
                                    // There may be the line comment inside the multiline comment, and it is ignored by C# and us 
                                    area = Area.LineComment;
                                    parsedCharCount = 2;
                                    stripMdMarker = chi + 3 < line.Length && line[chi + 2] == 'm' && line[chi + 3] == 'd';
                                    if (stripMdMarker)
                                    {
                                        parsedCharCount = 4;
                                        if (chi + 4 < line.Length)
                                        {
                                            if (line[chi + 4] == '{')
                                            {
                                                isInCollapsibleSection = true;
                                                outputLine.AppendFormat(CollapsibleSectionMarkdownBegin, line.AsSpan(chi + 5).Trim().ToString());
                                                parsedCharCount = line.Length - chi; // indicate that we done with the rest of the line
                                                stripMdMarker = false; // don't strip anything, we've already formed the new line above
                                            }
                                            else if (line[chi + 4] == '}' & isInCollapsibleSection)
                                            {
                                                isInCollapsibleSection = false;
                                                outputLine.Append(CollapsibleSectionMarkdownEnd).Append(line.AsSpan(chi + 5).Trim().ToString());
                                                parsedCharCount = line.Length - chi; // indicate that we done with the rest of the line
                                                stripMdMarker = false; // don't strip anything, we've already formed the new line above
                                            }
                                        }
                                    }
                                }
                                else if (line[chi + 1] == '*')
                                {
                                    // If there was a code before then we expect the opening comment, because it may be a situation
                                    // like `hey*/*md you md*/` denoting either closing or opening comment depending on the context
                                    area = Area.MultiLineComment;
                                    stripMdMarker = chi + 3 < line.Length && line[chi + 2] == 'm' && line[chi + 3] == 'd';
                                    isMultiLineMdComment = stripMdMarker;
                                    parsedCharCount = stripMdMarker ? 4 : 2; // at least skip the comment
                                }
                            }
                            break;
                        case Area.MultiLineComment:
                            if (isMultiLineMdComment
                                && line[chi] == 'm' && line[chi + 1] == 'd'
                                && chi + 3 < line.Length && line[chi + 2] == '*' && line[chi + 3] == '/')
                            {
                                stripMdMarker = true;
                                isMultiLineMdComment = false;
                                area = Area.Code;
                                parsedCharCount = 4;
                            }
                            else if (line[chi] == '*' & line[chi + 1] == '/')
                            {
                                stripMdMarker = isMultiLineMdComment; // depending on the opening comment it may be an md as well
                                isMultiLineMdComment = false;
                                area = Area.Code;
                                parsedCharCount = 2;
                            }
                            else if (isMultiLineMdComment
                                && line.AsSpan(chi).StartsWith(CodeFenceLang.AsSpan()))
                            {
                                stripMdMarker = true;
                                var langLength = ParseCodeLang(line.AsSpan(chi + CodeFenceLang.Length), ref isAutoInsertCodeFence, ref currentCodeFenceLang);
                                parsedCharCount = CodeFenceLang.Length + langLength;
                            }
                            break;
                        case Area.LineComment:
                            if (line.AsSpan(chi).StartsWith(CodeFenceLang.AsSpan()))
                            {
                                stripMdMarker = true;
                                var langLength = ParseCodeLang(line.AsSpan(chi + CodeFenceLang.Length), ref isAutoInsertCodeFence, ref currentCodeFenceLang);
                                parsedCharCount = CodeFenceLang.Length + langLength;
                            }
                            break;
                    }

                    // Strip the md comment or code lang from the output
                    if (stripMdMarker)
                    {
                        // take into account that j is referring to the start of the comment + 1 at this moment
                        var content = line.AsSpan(contentStart, chi - contentStart);

                        // Trim the leading spaces, with the result of indent being stripped too (see #16),
                        // if you want to preserve the indent, please add the spaces after the starting //md, or /*md comment
                        if (contentStart == 0)
                            content = content.TrimStart();

                        if (content.Length != 0)
                            outputLine.Append(content.ToString());

                        contentStart = chi + parsedCharCount;
                    }
                }

                // Finish the new line by addind the last strip at the end to the output
                if (contentStart > 0 && contentStart < line.Length)
                    outputLine.Append(line.Substring(contentStart));

                // In principle, where to insert the code fences:
                // Opening fence line to be inserted on the boundary of the area of the comment to the current Area.Code
                // But what if the next line is the md comment again? 
                // todo: @wip should we then insert the code fence on the next line, and what happens if the next line is empty?
                // After that it should be marked as `insideCodeFence == true`.
                // Closing fence line to be inserted on the boundary of Area.Code to the area of the comments.
                if (isAutoInsertCodeFence)
                {
                    if (area == Area.Code)
                    {
                        if (!isInCodeFence)
                        {
                            isInCodeFence = true;
                            output.AppendNewLineAfterContent().Append(CodeFence).Append(currentCodeFenceLang.ToString());
                        }
                    }
                    else if (isInCodeFence)
                    {
                        isInCodeFence = false;
                        output.AppendNewLineAfterContent().Append(CodeFence);
                    }
                }
                else if (isInCodeFence)
                {
                    // if the insert code fence mode is switched off but the code fence is not closed yet, let's close it
                    isInCodeFence = false;
                    output.AppendNewLineAfterContent().Append(CodeFence);
                }

                if (outputLine.Length == 0)
                {
                    // Сheck if the line does not consist of the md comment entirely (contentStart > 0 after the md comment),
                    // If so the remaining empty line is removed (is not appended to the output)
                    if (contentStart == 0)
                        output.AppendNewLineAfterContent().Append(line);
                }
                else
                {
                    // Being smart here and removing the first space for the odd number of spaces in the leading indent,
                    // check the IssueTests.Issue16_Ignore_leading_whitespace_before_md_comments for the example
                    if (outputLine.Length > 1 && outputLine[0] == ' ')
                    {
                        var spaces = 1;
                        while (spaces < outputLine.Length && outputLine[spaces] == ' ') ++spaces;
                        if (spaces % 2 == 1)
                            outputLine.Remove(0, 1);
                    }

                    // I think this is an expected cleaning behavior to remove the dangling spaces at the end of the line
                    outputLine.TrimEndTabAndSpaces();
                    if (outputLine.Length != 0)
                        output.AppendNewLineAfterContent().Append(outputLine);
                    outputLine.Clear();
                }
            }

            if (isAutoInsertCodeFence & isInCodeFence)
                output.AppendLine().Append(CodeFence); // insert the closing fence without the lang

            return output;
        }

        private static void TrimEndTabAndSpaces(this StringBuilder sb)
        {
            var i = sb.Length - 1;
            while (i >= 0 && (sb[i] == ' ' | sb[i] == '\t')) --i;
            if (i < sb.Length - 1)
                sb.Length = i + 1;
        }

        private static StringBuilder AppendNewLineAfterContent(this StringBuilder sb) => sb.Length != 0 ? sb.AppendLine() : sb;

        /// <summary>Returns consumed char count, including possible spaces before the lang or stop dashes</summary> 
        private static int ParseCodeLang(ReadOnlySpan<char> lineTail, ref bool insertCodeFence, ref ReadOnlySpan<char> currentCodeFenceLang)
        {
            var langStart = 0;
            var langLength = 0;
            var stopLangFound = false;
            var ch = default(char);
            for (var i = 0; i < lineTail.Length; ++i)
            {
                ch = lineTail[i];

                // at the start or space and not yet saw the lang
                if (langLength == 0)
                {
                    if (char.IsWhiteSpace(ch))
                    {
                        ++langStart;
                        continue; // skip the rest, and check for the lang start again
                    }

                    if (ch == '-')
                        stopLangFound = true; // got stop lang dash, so mark it to eat all dashes and stop
                    else if (!char.IsLetter(ch))
                        break;
                }
                else if (stopLangFound)
                {
                    if (ch != '-')
                        break; // stop eating dashes, when there is a no dash found
                }
                else if (!char.IsLetter(ch))
                    break; // if found non letter, stop the lang lookup altogether

                ++langLength; // in the successful case proceed to count the lang length
            }

            currentCodeFenceLang = stopLangFound ? ReadOnlySpan<char>.Empty : lineTail.Slice(langStart, langLength);
            insertCodeFence = !stopLangFound;

            // remove the dangling last space after the lang
            if (ch == ' ')
                ++langLength;

            return langStart + langLength;
        }

        public static StringBuilder StripMdComments_OLD(string[] inputLines, string[] removeLinesStartingWith = null)
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