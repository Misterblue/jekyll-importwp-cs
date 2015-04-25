# Wordpress Jekyll converter

I wanted to give Jekyll a try so I set to converting my WordPress blog to Jekyll.
I spent several hours trying to get enough Ruby running (I'm not a Ruby guy so I
don't have a environment set up) to run Jekyll but when I tried using the conversion
modules, I found package incompatibility heck. After several hours trying to get
things to compile and/or compatible versions of libraries to be installed, I figured
I could just spend the comparable time to rewrite the short and straight forward
conversion routine in a language I'm familiar with.

I just happened to have a C# environment available so, what you have here is
a C# version of the WordPress converted for Jekyll.
This version doesn't have all the parameters as the original one but most
everything defaults to reasonable values.

This reads from the WordPress database files and creates the Jekyll directories.

The invocation is:

```
ImportWP
        -H|--dbHost databaseHost
        -D|--dbName databaseName
        -U|--dbUser databaseUser
        -P|--dbPass databaseUserPassword
        -o|--output outputFilename
        --tablePrefix prefix
        --cleanEntities
        --verbose
```

Good luck converting.

