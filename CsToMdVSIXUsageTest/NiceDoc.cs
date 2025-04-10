/*md code:cs
## Tests are the docs

**How so?**

    As simple as adding 'md' to the normal C# comments,
    with the bonus of solving a hard problem of collapsible sections >:

md*/
//md{ Collapsed usings ...
using System;
using System.Text;
using System.Collections.Generic;
//md}

//- line to be removed

public class Foo
{
    public void Bar() { }
}
/*md
//md <- 'md' is kept when nested inside the md comment

*/ //md*no need for the closing md comment to include 'md'*
