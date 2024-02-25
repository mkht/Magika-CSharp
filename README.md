# Magika - CSharp

This is a C# implementation of the [Magika](https://github.com/google/magika). Includes a .NET class library and a single binary CLI tool.

Magika is a novel AI powered file type detection tool that is developed by Google.  
Pleease refer to the original repository for more information.  
https://github.com/google/magika

> [!IMPORTANT]  
> This is a **personal project for learning purpose**.  
> This is not an official implementation of Magika and is not supported by Google.  
> DO NOT use for production use.

----
## Prerequisites

On Windows, you need to install the Visual C++ Redistributable for Visual Studio 2015-2022 from the following link:
 - [Microsoft Visual C++ Redistributable latest supported downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170)

If you want to build from source, you need to install the following tools:
 - .NET 8.0 SDK

----
## Installation

You can download the pre-build binaries per platform from the [Releases](https://github.com/mkht/Magika-CSharp/releases) page.

----
## Usage

### Command line

```
C:\>magika.exe -r .\tests_data\
.\tests_data\basic\code.asm: Assembly (code)
.\tests_data\basic\code.c: C source (code)
.\tests_data\basic\code.css: CSS source (code)
.\tests_data\basic\code.js: JavaScript source (code)
...
.\tests_data\mitra\webp.webp: WebP data (image)
.\tests_data\mitra\webpl.webp: WebP data (image)
.\tests_data\mitra\xz.xz: XZ compressed data (archive)
.\tests_data\mitra\zip.zip: Zip archive data (archive)
.\tests_data\README.md: Markdown document (text)
```

```
C:\>magika.exe .\tests_data\basic\code.py --json
[
  {
    "path": ".\\tests_data\\basic\\code.py",
    "dl": {
      "ct_label": "python",
      "score": 0.9985216,
      "group": "code",
      "mime_type": "text/x-python",
      "magic": "Python script",
      "description": "Python source"
    },
    "output": {
      "ct_label": "python",
      "score": 0.9985216,
      "group": "code",
      "mime_type": "text/x-python",
      "magic": "Python script",
      "description": "Python source"
    }
  }
]
```

```
> magika --help
magika 0.5.0-dev

  -r, --recursive                When passing this option, magika scans every file within directories, instead of
                                 outputting "directory"
  --json                         Output in JSON format.
  --jsonl                        Output in JSONL format.
  -i, --mime-type                Output the MIME type instead of a verbose content type description.
  -l, --label                    Output a simple label instead of a verbose content type description. Use
                                 --list-output-content-types for the list of supported output.
  -c, --compatibility-mode       Compatibility mode: output is as close as possible to `file` and colors are disabled.
  -s, --output-score             Output the prediction's score in addition to the content type.
  -m, --prediction-mode
  --batch-size                   How many files to process in one batch.
  --no-dereference               This option causes symlinks not to be followed. By default, symlinks are dereferenced.
  --no-colors                    Enable/disable use of colors.
  --colors                       Enable/disable use of colors.
  -v, --verbose                  Enable more verbose output.
  --debug                        Enable debug logging.
  --list-output-content-types    Show a list of supported content types.
  --help                         Display this help screen.
  --version                      Display version information.

  FILES (pos. 1)
```

### .NET API (C#)

```csharp
using magika;
var magika = new Magika();

var inputBytes = "# Example\nThis is an example of markdown!"u8;
var res = magika.IdentifyBytes(inputBytes.ToArray());
Console.WriteLine(res.output.ct_label);
// > markdown

var inputFile = "path/to/sample.html";
var res = magika.IdentifyPath(inputFile);
Console.WriteLine(res.output.ct_label);
// > html
```

----
## Build from source

```cmd
git clone https://github.com/mkht/Magika-CSharp.git
cd ./magika

// Library
dotnet restore
dotnet build

// CLI
cd ./cli
dotnet publish -f net8.0 -r win-x64
```

----
## Important Notes
This project is based on the initial release of Magika [v0.5.0](https://github.com/google/magika/releases/tag/v0.5.0) , so it may not follow the changes in subsequent releases.

As this is not a complete port, it may behave differently from the original Magika.

This project is created for personal learning perpose. Therefore, there is no support at all. Continuous development and bug fixes are not guaranteed. It is undecided whether to follow the changes of the original project.

Issue reports and pull requests are welcome, but we may not be able to respond.

----
## License

Licensed under the Apache 2.0 - see the [LICENSE](LICENSE) file for details.  
The original Magika by Google is also licensed under the Apache 2.0.

This project includes the work that is distributed in the Apache 2.0 and MIT License.
