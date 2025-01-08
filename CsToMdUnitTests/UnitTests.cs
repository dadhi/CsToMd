namespace CsToMd.UnitTests;

public class Tests
{
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