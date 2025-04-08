using System;
using CsToMd.UnitTests;

var tests = new Tests();

tests.Add_code_fence_with_the_next_md_comment_again();
tests.Expand_the_details();
tests.Should_strip_the_line_md_comments();
tests.Should_parse_multiline_comments();

Console.WriteLine("Tests successful.");
