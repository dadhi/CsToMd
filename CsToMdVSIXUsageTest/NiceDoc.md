## Tests are the docs

**How so?**

    As simple as adding 'md' to the normal C# comments,
    with the bonus of solving a hard problem of collapsible sections >:

<details><summary><strong>Collapsed usings ...</strong></summary>

```
using System;
using System.Text;
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

