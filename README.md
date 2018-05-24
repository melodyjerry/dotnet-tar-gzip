# How to Create, Extract and Update Tar GZIP in C#

## How to Build

```
dotnet publish -c Release -r win10-x64
```

## How to Use
Create a .tar.gz file:

```
targzip c G:\dwt.tar.gz G:\dynamic-web-twain
```

Extract a .tar.gz file:

```
targzip e G:\dwt.tar.gz G:\output
```

Update a .tar.gz file: 

```
targzip u G:\dwt.tar.gz G:\dynamic-web-twain\license.txt
```



## References
* [SharpZipLib](https://github.com/icsharpcode/SharpZipLib)
* [GZip and Tar Samples](https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples) 

