namespace CsToMd.UnitTests;

public class Tests
{
    [Fact]
    public void Should_strip_the_line_md_comments()
    {
        var result = CommentStripper.StripMdComments(
            """
            //md foo
            var x = 1;
            var y = 2; //md bar
            // bazz
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            foo
            var x = 1;
            var y = 2; bar
            // bazz
            """,
            result);
    }

    [Fact]
    public void Should_strip_the_line_md_comments_with_new_lines_md_comment_one_after_another()
    {
        var result = CommentStripper.StripMdComments(
            """
            // hey

             
              //md foo
                //md    bar
             //md 
            var x = 1;
            """.Split(Environment.NewLine)).ToString();

        Assert.Equal(
            """
            // hey
            
            
            foo
                bar
            var x = 1;
            """,
            result);
    }

    [Fact]
    public void Should_remove_the_specified_line_and_md_comment_without_space_should_be_ignored()
    {
        var result = CommentStripper.StripMdComments(
            """
            //md hey
             //mdbar
            --rem oveit
            var x = 1;
            """.Split(Environment.NewLine),
            new[] { "--rem" }
            ).ToString();

        Assert.Equal(
            """
            hey
            bar
            var x = 1;
            """,
            result);
    }

    [Fact]
    public void Should_remove_multiline_comments()
    {
        var result = CommentStripper.StripMdComments(
            """
            //md  
            oh/*judy*/ho
            /*md
            xxx
            */
            /*md foo md*/ oof
              /*md bar */ 
              /*md  baz 
              md*/ xyz
              var x = 1;
            /* zzz */
            //md end
            """.Split(Environment.NewLine)
            ).ToString();

        Assert.Equal(
            """
            oh/*judy*/ho
            xxx
            foo oof
            bar
              baz
            xyz
              var x = 1;
            /* zzz */
            end
            """,
            result);
    }

    [Fact]
    public void Keep_nested_md_comment_intact()
    {
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
    public void Should_remove_empty_comment_at_the_end()
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
            //md ## Docs
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

    // [Fact]
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

    // [Fact]
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

    // [Fact]
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