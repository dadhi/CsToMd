using System;
using System.Diagnostics;
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

        public static readonly string CodeFenceLangMarker = "code:";
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
            // Clamp the upper bound of the lines we check to defense against the DoS attack, because we will do the check for the each line, so just in case...
            var linesToRemoveCount = Math.Min(removeLinesStartingWith?.Length ?? 0, MaxLineToRemoveCount);

            var output = new StringBuilder(inputLines.Length * 64);
            var outputForLine = new StringBuilder(64); // the reused buffer for the output produced by parsing the input line

            var codeFenceLangMarker = CodeFenceLangMarker.AsSpan();
            var currentCodeFenceLang = ReadOnlySpan<char>.Empty;
            var shouldInsertCodeFence = false;
            var insideTheCodeFence = false;

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

                prevLineScope = scope;
                // Reset the new line scope to the Code if the previous line was a LineComment
                if (scope == Scope.LineComment | scope == Scope.LineCommentMd)
                    scope = Scope.Code;

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

                                    if (parserAt - outputAt > 0)
                                    {
                                        var spanBeforeMdComment = line.AsSpan(outputAt, parserAt - outputAt);
                                        if (!spanBeforeMdComment.IsWhiteSpace())
                                            outputForLine.Append(spanBeforeMdComment);
                                    }

                                    if (shouldInsertCodeFence & insideTheCodeFence & prevLineScope == Scope.Code)
                                    {
                                        insideTheCodeFence = false;
                                        outputForLine.AppendLineToNonEmpty().AppendLine(CodeFence);
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
                                    else
                                    {
                                        // Parse the code fence marker immediately after the md comment
                                        var tail = line.AsSpan(parserAt);
                                        var tailWoSpace = tail.TrimStart();
                                        if (tailWoSpace.StartsWith(codeFenceLangMarker))
                                        {
                                            var langLen = ParseCodeLang(tailWoSpace.Slice(codeFenceLangMarker.Length),
                                                ref shouldInsertCodeFence, ref currentCodeFenceLang);

                                            // Skip the `code: lang` and stop immediatly after the lang
                                            var spaceLen = tail.Length - tailWoSpace.Length;
                                            parserAt += spaceLen + codeFenceLangMarker.Length + langLen;
                                            outputAt = parserAt;
                                        }

                                        // Skip a single leading space after the md comment, e.g. in `//md foo` output `foo`
                                        if (parserAt + 1 < lineLen && line[parserAt] == ' ' & line[parserAt + 1] != ' ')
                                        {
                                            ++parserAt;
                                            ++outputAt;
                                        }
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

                                    if (shouldInsertCodeFence & insideTheCodeFence & prevLineScope == Scope.Code)
                                    {
                                        insideTheCodeFence = false;
                                        outputForLine.AppendLineToNonEmpty().AppendLine(CodeFence);
                                    }

                                    parserAt += 4; // skip over the opening md comment `/*md`
                                    outputAt = parserAt;

                                    // Parse the code fence marker immediately after the md comment
                                    var tail = line.AsSpan(parserAt);
                                    var tailWoSpace = tail.TrimStart();
                                    if (tailWoSpace.StartsWith(codeFenceLangMarker))
                                    {
                                        var langLen = ParseCodeLang(tailWoSpace.Slice(codeFenceLangMarker.Length),
                                            ref shouldInsertCodeFence, ref currentCodeFenceLang);

                                        // Skip the `code: lang` and stop immediatly after the lang
                                        var spaceLen = tail.Length - tailWoSpace.Length;
                                        parserAt += spaceLen + codeFenceLangMarker.Length + langLen;
                                        outputAt = parserAt;
                                    }

                                    // Skip a single leading space after the md comment, e.g. in `/*md foo` output `foo`
                                    if (parserAt + 1 < lineLen && line[parserAt] == ' ' & line[parserAt + 1] != ' ')
                                    {
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
                                // Add the code fence if it is defined and there was not md comment previously
                                if (shouldInsertCodeFence & !insideTheCodeFence)
                                {
                                    insideTheCodeFence = true;
                                    output.AppendLineToNonEmpty().Append(CodeFence).Append(currentCodeFenceLang);
                                }

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
                                if (parserAt == 0)
                                {
                                    // Parse the code fence marker immediately after the md comment
                                    var tail = line.AsSpan();
                                    var tailWoSpace = tail.TrimStart();
                                    if (tailWoSpace.StartsWith(codeFenceLangMarker))
                                    {
                                        var langLen = ParseCodeLang(tailWoSpace.Slice(codeFenceLangMarker.Length), ref shouldInsertCodeFence, ref currentCodeFenceLang);

                                        // Skip the `code: lang` and stop immediatly after the lang
                                        var spaceLen = tail.Length - tailWoSpace.Length;
                                        parserAt += spaceLen + codeFenceLangMarker.Length + langLen;
                                        outputAt = parserAt;
                                    }
                                    else
                                    {
                                        // If the code fence is not found we still may fast skip the whitespaces, and avoid looking for the code fence multiple times
                                        parserAt = tail.Length - tailWoSpace.Length + 1;
                                    }
                                }
                                else
                                {
                                    ++parserAt;
                                }
                            }
                            break;

                        default:
                            lineDone = true;
                            break;
                    }
                }

                // If the output kept at 0, parser did not the md comments, so adding the whole input line
                if (outputAt == 0)
                {
                    output.AppendLineToNonEmpty().Append(line.TrimEnd()); // remove the trailing spaces from the line
                }
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

#if !NET5_OR_GREATER
        // Polyfill for the .NET Framework
        private static StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> fragment) =>
            sb.Append(fragment.ToString());
#endif

        private static StringBuilder AppendLineToNonEmpty(this StringBuilder sb) => sb.Length != 0 ? sb.AppendLine() : sb;

        /// <summary>The code lang should start immediately without spaces before it and consist and should end with the white space.
        /// Returns consumed char count as the length of the lang + the space (if the lang is ended with space).</summary> 
        private static int ParseCodeLang(ReadOnlySpan<char> lineTail, ref bool insertCodeFence, ref ReadOnlySpan<char> currentCodeFenceLang)
        {
            // The defaults:
            insertCodeFence = true;
            currentCodeFenceLang = ReadOnlySpan<char>.Empty;

            // Insert the code fence but without the lang label
            if (lineTail.Length == 0)
                return 0;

            if (char.IsWhiteSpace(lineTail[0]))
                return 1;

            // Found stop lang dash, now comsume the remaining dashes until non-dash
            if (lineTail[0] == '-')
            {
                var i = 1;
                while (i < lineTail.Length && lineTail[i] == '-')
                    ++i;
                insertCodeFence = false;
                return i < lineTail.Length && char.IsWhiteSpace(lineTail[i]) ? i + 1 : i; // Count the ending space
            }

            // Found the lang, now consume it until the first space or end of the line
            var j = 1;
            while (j < lineTail.Length && !char.IsWhiteSpace(lineTail[j]))
                ++j;
            currentCodeFenceLang = lineTail.Slice(0, j);
            return j < lineTail.Length ? j + 1 : j; // Count the ending space
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