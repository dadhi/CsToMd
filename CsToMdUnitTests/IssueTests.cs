namespace CsToMd.UnitTests;

public class IssueTests
{
    [Fact]
    public void Issue16_Ignore_leading_whitespace_before_md_comments()
    {
        var result = CommentStripper.StripMdComments(
            """
                //md    Header
                //md blah
                /*md foo bar md*/
            /*md
                md*/
            //md
            xxx
            /*md

            yyy

            md*/
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
                Header
            blah
            foo bar
            xxx

            yyy

            """,
            result);
    }

    [Fact]
    public void Issue15_Simplified_case_from_the_issue()
    {
        var result = CommentStripper.StripMdComments(
            """
            //md code:cs
            var x = 3;
            //md code:-- stop the cs fences
            //md```js
            const y = function() {};
            //md```
            //md code:cs back to cs, space after colon
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            ```cs
            var x = 3;
            ```
            stop the cs fences
            ```js
            const y = function() {};
            ```
            back to cs, space after colon
            """,
            result);
    }

    [Fact]
    public void Issue15_Automatically_wrap_code_in_code_fence_with_the_specified_lang()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md
            code:js
            code:---
            Explicit code fence
            ```cs
            // hey md*/
            var x = 42;
            /*md
            ```
            code:cs
            Implicit code fence will be inserted here with the lang specified on the line above
            md*/
            var y = 43;   
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            Explicit code fence
            ```cs
            // hey
            var x = 42;
            ```
            Implicit code fence will be inserted here with the lang specified on the line above
            ```cs
            var y = 43;
            ```

            """,
            result);
    }

    [Fact]
    public void Issue15_Simplest_test()
    {
        var result = CommentStripper.StripMdComments(
            """
            //md code:
            var x = 42;
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            ```
            var x = 42;
            ```

            """,
            result);
    }

    [Fact]
    public void Issue20_collapsible_section_with_multiline_comments()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md{ Foo
            
            # Bar
            
            }*/
            /*md{ mismatched collapsible is on you  }*/
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            <details><summary>Foo</summary>
            

            # Bar
            
            </details>
            <details><summary>mismatched collapsible is on you  }*/</summary>
            
            """,
            result);
    }
}