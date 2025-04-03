using System;
using CsToMd.UnitTests;

var tests = new Tests();

tests.Expand_the_details();
tests.Should_strip_the_line_md_comments();
tests.Should_remove_multiline_comments();

Console.WriteLine("Tests successful.");
