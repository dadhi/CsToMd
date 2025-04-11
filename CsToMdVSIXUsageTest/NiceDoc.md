## The tests are the docs

**How so?**

    As simple as adding 'md' to the normal C# comments,
    with the bonus of solving a hard problem of collapsible sections >:

<details><summary>Collapsed usings ...</summary>

```cs
using System;
using System.Text;
using System.Collections.Generic;
```
</details>


```cs
public class Foo
{
    public void Bar() { }
}
```

//md <- 'md' is kept when nested inside the md comment

*no need for the closing md comment to include 'md'*
