namespace CsToMd.UnitTests;

public class IssueTests
{
    [Fact]
    public void Issue16_Ignore_leading_whitespace_before_md_comments()
    {
        var result = CommentStripper.StripMdComments(
            """
                //md   Header
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

    // todo: @wip feature
    //[Fact]
    public void Issue15_Automatically_wrap_code_in_code_fence_with_the_specified_lang()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md
            Explicit code fence
            ```cs md*/
            var x = 42;
            /*md
            ```
            code:cs
            Implicit code fence
            md*/
            var y = 43;
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            Explicit code fence
            ```cs
            var x = 42;
            ```
            Implicit code fence
            ```cs
            var y = 43;
            ```
            """,
            result);
    }
}