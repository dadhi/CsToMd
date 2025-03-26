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

    [Fact]
    public void Expand_the_details()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md
            ## Docs
            md*/
            //md{ usings ...  
            //md```cs
            namespace DryIoc.Docs;
            //md```
            //md}
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            ## Docs
            <details><summary><strong>usings ...</strong></summary>

            ```cs
            namespace DryIoc.Docs;
            ```
            </details>
            """,
            result);
    }

    [Fact]
    public void Add_code_fence_with_the_next_md_comment_again()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md
            code:cs
            ## Docs
            md*/
            //md foo  
            //md bar
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            ## Docs
            foo
            bar
            """,
            result);
    }

    [Fact]
    public void Add_code_fence_with_the_space_and_the_next_md_comment_again()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md
            code:cs
            ## Docs
            md*/

            //md foo  
            //md bar
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            ## Docs

            foo
            bar
            """,
            result);
    }

    [Fact]
    public void Expand_the_details_and_add_code_fence()
    {
        var result = CommentStripper.StripMdComments(
            """
            /*md
            code:cs
            ## Docs
            md*/
            //md{ usings ...  
            namespace DryIoc.Docs;
            //md}
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            ## Docs
            <details><summary><strong>usings ...</strong></summary>

            ```cs
            namespace DryIoc.Docs;
            ```
            </details>
            """,
            result);
    }
}