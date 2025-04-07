﻿using System;
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

        public const int MaxLineToRemoveCount = 64;

        // public static readonly string CollapsibleSectionCommentBegin = "//md{";
        // public static readonly string CollapsibleSectionCommentEnd = "//md}";
        // public static readonly string CollapsibleSectionMarkdownBegin = @"<details><summary><strong>{0}</strong></summary>" + NewLine;
        // public static readonly string CollapsibleSectionMarkdownEnd = @"</details>";

        public static readonly string CodeFenceLang = "code:";
        public static readonly string CodeFence = "```";

        enum Scope
        {
            Code = 0,
            LineComment,
            LineCommentMd,
            MultiLineComment,
            MultiLineCommentMd,
        }

        public static StringBuilder StripMdComments(string[] inputLines, string[] removeLinesStartingWith = null)
        {
            // clamp the upper bound of the lines we check to defense against the DoS attack, because we will do the check for the each line, so just in case...
            var linesToRemoveCount = Math.Min(removeLinesStartingWith?.Length ?? 0, MaxLineToRemoveCount);

            var output = new StringBuilder(inputLines.Length * 64);
            var outputForLine = new StringBuilder(64); // the reused buffer for the output produced by parsing the input line

            // var codeFenceLang = CodeFenceLang.AsSpan();
            // var currentCodeFenceLang = ReadOnlySpan<char>.Empty;

            // Tracks the current level of nesting of the details expansion, where 0 means there is no expansion.
            var inDetailsLevel = 0;

            // Let's start from the Code as default scope of the parser
            var prevLineScope = Scope.Code;
            var scope = Scope.Code;
            for (var i = 0; i < inputLines.Length; i++)
            {
                var line = inputLines[i];
                var lineLen = line.Length;

                // Trim the empty line and append it to the output right away.
                if (lineLen == 0 || string.IsNullOrWhiteSpace(line))
                {
                    output.AppendLineToNonEmpty();
                    continue;
                }

                // Remove completely the lines that are started with the lines provided by the user
                if (linesToRemoveCount != 0)
                {
                    var removeLine = false;
                    for (var j = 0; !removeLine && j < linesToRemoveCount; j++)
                    {
                        var lineStartingWith = removeLinesStartingWith[j];
                        removeLine = !string.IsNullOrWhiteSpace(lineStartingWith) && line.StartsWith(lineStartingWith);
                    }
                    if (removeLine)
                        continue; // if line should be removed, just skip it and go to the next line
                }

                // We are interested in the lines that may contain the comments or md comments, which at least of 2 chars long.
                if (lineLen == 1)
                {
                    output.AppendLineToNonEmpty().Append(line);
                    continue;
                }

                // Reset the new line scope to the Code if it was the line comment before, but keep track of this decision 
                if (scope == Scope.LineComment | scope == Scope.LineCommentMd)
                {
                    prevLineScope = scope;
                    scope = Scope.Code;
                }

                var outputAt = 0; // The position to track the start of the line span that we should copy to the output line
                var parserAt = 0;
                var lineDone = false;

                // It is fine to finish before the last char, because the comments are at least 2 char length, 
                // so it need to read the next char without the check for line length.
                while (!lineDone & parserAt < lineLen - 1)
                {
                    switch (scope)
                    {
                        case Scope.Code:
                            if (line[parserAt] == '/' & line[parserAt + 1] == '/')
                            {
                                scope = Scope.LineComment;

                                var isLineMdComment = parserAt + 3 < lineLen && line[parserAt + 2] == 'm' & line[parserAt + 3] == 'd';

                                // Check the next char after md comment to validate it
                                var charAfterMd = 0;
                                if (isLineMdComment & parserAt + 4 < lineLen)
                                {
                                    charAfterMd = line[parserAt + 4];
                                    // Found the unexpected not matching details end `//md}`
                                    isLineMdComment = !(charAfterMd == '}' & inDetailsLevel <= 0);
                                }

                                if (isLineMdComment)
                                {
                                    scope = Scope.LineCommentMd;

                                    // Skip a single space after the comment, but keep multiple as it may be a User intent, e.g. for `//md foo` output `foo`
                                    if (parserAt - outputAt > 0)
                                    {
                                        var spanBeforeMdComment = line.AsSpan(outputAt, parserAt - outputAt);
                                        if (!spanBeforeMdComment.IsWhiteSpace())
                                            outputForLine.Append(spanBeforeMdComment);
                                    }

                                    parserAt += 4;
                                    outputAt = parserAt;

                                    // Process the expansion of the `//md{ foo\nbar\n//md} into the `<details><summary><strong>foo</strong></summary>\n\nbar\n</details>`
                                    if (charAfterMd == '{')
                                    {
                                        ++inDetailsLevel;

                                        var summary = line.AsSpan(outputAt + 1).Trim();
                                        outputForLine.Append("<details><summary><strong>").Append(summary);
                                        // The additional new line is required here for the markdown processing of summary
                                        outputForLine.AppendLine("</strong></summary>");

                                        outputAt = lineLen; // the whole line is processed
                                        lineDone = true;
                                    }
                                    else if (charAfterMd == '}')
                                    {
                                        // No need to check if the ending tag has a matching closing tag, because we deed it above for the `isLineMdComment`
                                        --inDetailsLevel;

                                        outputForLine.Append("</details>");

                                        ++outputAt; // skip the `//md}` to consume the tail of the line below
                                        lineDone = true;
                                    }
                                    else if (parserAt + 1 < lineLen && line[parserAt] == ' ' & line[parserAt + 1] != ' ')
                                    {
                                        // Skip a single leading space after the md comment, e.g. in `//md foo` output `foo`
                                        ++parserAt;
                                        ++outputAt;
                                    }
                                }
                                else
                                {
                                    parserAt += 2; // skip over the line comment
                                }
                            }
                            else if (line[parserAt] == '/' & line[parserAt + 1] == '*')
                            {
                                scope = Scope.MultiLineComment;
                                var isMultiLineMdComment = parserAt + 3 < lineLen && line[parserAt + 2] == 'm' & line[parserAt + 3] == 'd';
                                if (isMultiLineMdComment)
                                {
                                    scope = Scope.MultiLineCommentMd;

                                    if (parserAt - outputAt > 0)
                                    {
                                        // Ignore the spaces-only span before the start of the multiline comment
                                        var spanBeforeMdComment = line.AsSpan(outputAt, parserAt - outputAt);
                                        if (!spanBeforeMdComment.IsWhiteSpace())
                                            outputForLine.Append(spanBeforeMdComment);
                                    }

                                    parserAt += 4; // skip over the opening md comment `/*md`
                                    outputAt = parserAt;

                                    if (parserAt + 1 < lineLen && line[parserAt] == ' ' & line[parserAt + 1] != ' ')
                                    {
                                        // Skip a single leading space after the md comment, e.g. in `/*md foo` output `foo`
                                        ++parserAt;
                                        ++outputAt;
                                    }
                                }
                                else
                                {
                                    parserAt += 2; // skip over the opening comment `/*`
                                }
                            }
                            else
                            {
                                ++parserAt; // by default parse the Code by one char
                            }

                            break;

                        case Scope.MultiLineComment:
                            if (line[parserAt] == '*' & line[parserAt + 1] == '/')
                            {
                                scope = Scope.Code;
                                parserAt += 2; // skip over the closing comment
                            }
                            else
                            {
                                ++parserAt;
                            }
                            break;

                        case Scope.MultiLineCommentMd:
                            // The end of the both normal and md multiline comment
                            // Note: in this sample `/* */ */` the last closing comment is **invalid** C#,
                            // so we may ignore the closing md comment not inside the `Scope.MultiLineCommentMd`
                            if (line[parserAt] == '*' & line[parserAt + 1] == '/')
                            {
                                scope = Scope.Code;

                                // Ignore the preceding `md` in `md*/` if they found
                                var prevSpanLen = parserAt - outputAt;
                                if (prevSpanLen >= 2 & parserAt >= 2 &&
                                    (line[parserAt - 2] == 'm' & line[parserAt - 1] == 'd'))
                                    prevSpanLen -= 2;

                                ReadOnlySpan<char> spanBeforeClosingMdComment = default;
                                if (prevSpanLen > 0)
                                {
                                    // Trim the trailing spaces between the content and closing comment
                                    spanBeforeClosingMdComment = line.AsSpan(outputAt, prevSpanLen).TrimEnd();
                                    if (spanBeforeClosingMdComment.Length != 0)
                                        outputForLine.Append(spanBeforeClosingMdComment);
                                }

                                parserAt += 2; // skip over the closing comment
                                outputAt = parserAt;

                                // Skip over a single space after the comment, but keep multiple as it may be a User intent, e.g. for `md*/ foo` output `foo`
                                // but only at the start of the line because otherwise it is already trimmed before the comment end, e.g. `x md*/ y` should produce `x y` but not `xy`
                                if (spanBeforeClosingMdComment.Length == 0 &&
                                    parserAt + 1 < lineLen && line[parserAt] == ' ' & line[parserAt + 1] != ' ')
                                {
                                    ++outputAt;
                                    ++parserAt;
                                }
                            }
                            else
                            {
                                ++parserAt;
                            }
                            break;

                        default:
                            lineDone = true;
                            break;
                    }
                }

                // If the output kept at 0, parser did not found anything interesting (md comments), so adding the whole input line
                if (outputAt == 0)
                    output.AppendLineToNonEmpty().Append(line.TrimEnd()); // remove the trailing spaces from the line
                else
                {
                    // Add the remaining tail of the line, e.g. `md*/ foo` or `/*md baz   `
                    if (outputAt < lineLen)
                    {
                        var lineTail = line.AsSpan(outputAt, lineLen - outputAt).TrimEnd();
                        if (lineTail.Length != 0)
                            outputForLine.Append(lineTail);
                    }

                    // Append the result output line to the output and clear the output line for the next line cycle.
                    if (outputForLine.Length != 0)
                    {
                        output.AppendLineToNonEmpty().Append(outputForLine);
                        outputForLine.Clear();
                    }
                }
            }

            return output;
        }

#if NETFRAMEWORK
        // Polyfill for the .NET Framework
        private static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> fragment) =>
            sb.Append(fragment.ToString());
#endif

        private static void TrimEndTabAndSpaces(this StringBuilder sb)
        {
            var i = sb.Length - 1;
            while (i >= 0 && (sb[i] == ' ' | sb[i] == '\t')) --i;
            if (i < sb.Length - 1)
                sb.Length = i + 1;
        }

        private static StringBuilder AppendLineToNonEmpty(this StringBuilder sb) => sb.Length != 0 ? sb.AppendLine() : sb;

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

        // public static StringBuilder StripMdComments_OLD(string[] inputLines, string[] removeLinesStartingWith = null)
        // {
        //     var outputBuilder = new StringBuilder(inputLines.Length * 64);
        //     var lastLineIndex = inputLines.Length - 1;
        //     var hasLinesToRemove = removeLinesStartingWith != null && removeLinesStartingWith.Length > 0;

        //     var isInCollapsibleSection = false;
        //     string codeFenceLang = null;
        //     for (var i = 0; i < inputLines.Length; i++)
        //     {
        //         var line = inputLines[i];
        //         if (!string.IsNullOrWhiteSpace(line))
        //         {
        //             if (hasLinesToRemove)
        //             {
        //                 var removeLine = false;
        //                 for (var j = 0; !removeLine && j < removeLinesStartingWith.Length; j++)
        //                 {
        //                     var lineStartingWith = removeLinesStartingWith[j];
        //                     if (!string.IsNullOrWhiteSpace(lineStartingWith) &&
        //                         line.StartsWith(lineStartingWith))
        //                         removeLine = true;
        //                 }
        //                 if (removeLine)
        //                     continue;
        //             }

        //             if (!isInCollapsibleSection && (isInCollapsibleSection = line.StartsWith(CollapsibleSectionCommentBegin)))
        //             {
        //                 line = string.Format(CollapsibleSectionMarkdownBegin, line.Substring(CollapsibleSectionCommentBegin.Length).Trim());
        //             }
        //             else if (isInCollapsibleSection && line.StartsWith(CollapsibleSectionCommentEnd))
        //             {
        //                 line = CollapsibleSectionMarkdownEnd;
        //                 isInCollapsibleSection = false;
        //             }
        //             else
        //             {
        //                 // Strip md comments markers from the line. The result of a single stripped part means the line does not contain the md comments.
        //                 // If for the some reason we have an empty comment it does not matter and the empty part can be glued back without changing the source line.

        //                 // Trim the leading spaces, with result of indent being stripped too (see #16),
        //                 // if you want to preserve the indent, please add the spaces after the starting //md, or /*md comment

        //                 var noLeadingSpaceLine = line.TrimStart();
        //                 if (noLeadingSpaceLine.StartsWith(CodeFenceLang))
        //                 {
        //                     codeFenceLang = noLeadingSpaceLine.Substring(CodeFenceLang.Length).Trim();
        //                     line = null; // means to remove the line with code fence from the output
        //                 }

        //                 var partsAroundComments = noLeadingSpaceLine.Split(MdComments, StringSplitOptions.None);
        //                 if (partsAroundComments.Length != 1)
        //                 {
        //                     // By convention, the result empty lines are removed
        //                     line = partsAroundComments.All(p => string.IsNullOrWhiteSpace(p)) ? null : string.Concat(partsAroundComments);
        //                     if (line != null)
        //                     {
        //                         // Trim the end spaces, I think this the expected behavior
        //                         line = line.TrimEnd();

        //                         // Being smart here and remove the first space for the odd number of spaces in the leading indent,
        //                         // check the IssueTests.Issue16_Ignore_leading_whitespace_before_md_comments for the example;
        //                         if (line.Length > 1 && line[0] == ' ')
        //                         {
        //                             var spaces = 1;
        //                             while (spaces < line.Length && line[spaces] == ' ') ++spaces;
        //                             if (spaces % 2 == 1)
        //                                 line = line.Substring(1);
        //                         }
        //                     }
        //                 }
        //             }
        //         }

        //         if (line != null)
        //         {
        //             // this logic is required to handle the last line,
        //             // that may be not the actual last line, but the removed empty comment line,
        //             // see IssueTests.Remove_empty_comment_at_the_end
        //             if (outputBuilder.Length > 0)
        //                 outputBuilder.AppendLine();
        //             outputBuilder.Append(line);
        //         }
        //     }

        //     return outputBuilder;
        // }
    }
}