﻿using System;
using System.Linq;
using System.Text;

namespace CsToMd
{
    public static class CommentStripper
    {
        /// <summary>
        /// If line contains these symbols only it will be removed from out put, otherwise it the symbols will be stripped from line.
        /// </summary>
        public static readonly string[] StripSymbols = { @"/*md", @"md*/" };

        public static StringBuilder StripMdComments(string[] inputLines)
        {
            var outputBuilder = new StringBuilder(inputLines.Length * 20);
            var lastLineIndex = inputLines.Length - 1;
            for (var i = 0; i < inputLines.Length; i++)
            {
                var line = inputLines[i];
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(StripSymbols, StringSplitOptions.None);
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