namespace CsToMd.UnitTests;

public class Tests
{
    [Fact]
    public void Keep_nested_md_comment_intact()
    {
        /*
        /*md md*/
        /*md
        //md */
        var result = CommentStripper.StripMdComments(
            """
            /*
            /*md md*/
            /*md
            //md */
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            /*
            /*md md*/
            //md
            """,
            result);
    }

    [Fact]
    public void Remove_empty_comment_at_the_end()
    {
        var result = CommentStripper.StripMdComments(
            """
            x
            //md
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            x
            """,
            result);
    }
}