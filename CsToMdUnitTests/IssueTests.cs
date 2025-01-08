namespace CsToMd.UnitTests;

public class IssueTests
{
    [Fact]
    public void Issue16_Ignore_leading_whitespace_before_md_comments()
    {
        var result = CommentStripper.StripMdComments(
            """
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
            blah
            foo bar
            xxx

            yyy

            """,
            result);
    }
}