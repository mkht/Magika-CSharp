/*
* This code is a modified version of the original code, which is licensed under the Apache 2.0 License.
* The original code can be found at: https://github.com/google/magika/
* And the original license is as follows:
*
* Copyright 2024 Google LLC
* 
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
* 
*     http://www.apache.org/licenses/LICENSE-2.0
* 
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using CommandLine;
using ConsoleTables;
using magika;

class Program
{
    static readonly HashSet<string> PredictionModeStr =
    [
        "best-guess",
        "medium-confidence",
        "high-confidence"
    ];

    static readonly EnumerationOptions enumerationOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.Offline | FileAttributes.Device
    };

    static SimpleLogger _logger;
    static JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    static void Main(string[] args)
    {
        // fallback to --help or --version
        if (args.Length == 0)
        {
            args = ["--help"];
        }
        else if (args.Length == 1 && args[0] == "-h")
        {
            args = ["--help"];
        }
        else if (args.Length == 1 && args[0] == "-v")
        {
            args = ["--version"];
        }

        var parseResult = Parser.Default.ParseArguments<Options>(args);

        switch (parseResult.Tag)
        {
            case ParserResultType.Parsed:
                var parsed = parseResult as Parsed<Options>;
                Options opt = parsed.Value;

                // defaults color is on, so if --no-colors is passed, we set it to off.
                // --no-colors precedes --colors
                bool UseColors = true;
                if (opt.Colors)
                {
                    UseColors = true;
                }
                if (opt.NoColors)
                {
                    UseColors = false;
                }
                if (opt.CompatibilityMode)
                {
                    UseColors = false;
                }

                if (UseColors)
                {
                    // Enable ANSI colors on legacy Windows consoles
                    EnableVirtualTerminalProcessing();
                }

                // Initialize logger
                _logger = new SimpleLogger(LogLevel.Error, UseColors);
                if (opt.Verbose)
                {
                    _logger.LogLevel = LogLevel.Information;
                }
                if (opt.Debug)
                {
                    _logger.LogLevel = LogLevel.Debug;
                }

                // Check CLI arguments and options
                if (opt.ListOutputContentTypes)
                {
                    if (opt.Files.Any())
                    {
                        _logger.Error("You cannot pass any path when using the -l / --list option.");
                        Environment.Exit(1);
                    }
                    PrintOutputContentTypesList();
                    Environment.Exit(0);
                }

                PredictionMode predictionMode = PredictionMode.MEDIUM_CONFIDENCE;
                if (string.IsNullOrWhiteSpace(opt.PredictionModeStr))
                {
                    if (!PredictionModeStr.Contains(opt.PredictionModeStr, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.Error($"Invalid value for --prediction-mode: {opt.PredictionModeStr}");
                        Environment.Exit(1);
                    }
                    else
                    {
                        predictionMode = (PredictionMode)Enum.Parse(typeof(PredictionMode), opt.PredictionModeStr.ToUpper().Replace("-", "_"));
                    }
                }

                if (!opt.Files.Any())
                {
                    _logger.Error("You need to pass at least one path, or - to read from stdin.");
                    Environment.Exit(1);
                }

                List<string> filePaths = [];
                bool readFromStdIn = false;
                foreach (var file in opt.Files)
                {
                    if (file == "-")
                    {
                        readFromStdIn = true;
                        filePaths.Add("-");
                    }
                    else if (!File.Exists(file) && !Directory.Exists(file))
                    {
                        _logger.Error($"File or directory \"{file}\" does not exist.");
                        Environment.Exit(1);
                    }
                    else
                    {
                        filePaths.Add(file);
                    }
                }

                if (readFromStdIn)
                {
                    if (filePaths.Count > 1)
                    {
                        _logger.Error("If you pass \"-\", you cannot pass anything else.");
                        Environment.Exit(1);
                    }
                    if (opt.Recursive)
                    {
                        _logger.Error("If you pass \"-\", recursive scan is not meaningful.");
                        Environment.Exit(1);
                    }
                }

                if (opt.BatchSize is <= 0 or > 512)
                {
                    _logger.Error("Batch size needs to be greater than 0 and less or equal than 512.");
                    Environment.Exit(1);
                }

                if (opt.JsonOutput && opt.JsonLOutput)
                {
                    _logger.Error("You should use either --json or --jsonl, not both.");
                    Environment.Exit(1);
                }
                else if (opt.JsonOutput)
                {
                    _jsonOptions.WriteIndented = true;
                }
                else if (opt.JsonLOutput)
                {
                    _jsonOptions.WriteIndented = false;
                }

                if ((opt.MimeOutput && opt.LabelOutput) || (opt.MimeOutput && opt.CompatibilityMode) || (opt.LabelOutput && opt.CompatibilityMode))
                {
                    _logger.Error("You should use only one of --mime, --label, --compatibility-mode.");
                    Environment.Exit(1);
                }

                if (opt.Recursive)
                {
                    try
                    {
                        // List<string> actualFilePaths = filePaths.Where(Directory.Exists).SelectMany(e => Directory.EnumerateFiles(e, "*", enumerationOptions)).Where(e => !Directory.Exists(e)).ToList();
                        List<string> expandedFilePaths = [];
                        foreach (var file in filePaths)
                        {
                            if (File.Exists(file))
                            {
                                expandedFilePaths.Add(file);
                                continue;
                            }
                            if (Directory.Exists(file))
                            {
                                expandedFilePaths.AddRange(GetFilesFromDirectory(file, opt.NoDereference));
                            }
                            else if (file == "-")
                            {
                                continue;
                            }
                            else
                            {
                                _logger.Error($"File or directory \"{file}\" does not exist.");
                                Environment.Exit(1);
                            }
                        }
                        filePaths = expandedFilePaths;
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"Error while scanning directories: {e.Message}");
                        Environment.Exit(1);
                    }
                }

                _logger.Info($"Considering {filePaths.Count} files");
                _logger.Debug($"Files: {string.Join(",", filePaths)}");

                // Call Magika
                Magika magikaObj = null;
                try
                {
                    magikaObj = new(
                        prediction_mode: predictionMode,
                        no_dereference: opt.NoDereference,
                        verbose: opt.Verbose,
                        debug: opt.Debug,
                        use_colors: UseColors
                    );


                    string start_color = "";
                    string end_color = "";
                    Dictionary<string, string> color_by_group = new()
                    {
                        ["document"] = Colors.LIGHT_PURPLE,
                        ["executable"] = Colors.LIGHT_GREEN,
                        ["archive"] = Colors.LIGHT_RED,
                        ["audio"] = Colors.YELLOW,
                        ["image"] = Colors.YELLOW,
                        ["video"] = Colors.YELLOW,
                        ["code"] = Colors.LIGHT_BLUE
                    };

                    // updated only when we need to output in JSON format
                    List<MagikaResult> all_predictions = [];

                    var batches_num = filePaths.Count / opt.BatchSize;
                    if (filePaths.Count % opt.BatchSize != 0)
                    {
                        batches_num += 1;
                    }

                    List<MagikaResult> batch_predictions = [];
                    for (int batch_idx = 0; batch_idx < batches_num; batch_idx++)
                    {
                        var files_ = filePaths.Skip(batch_idx * opt.BatchSize).Take(opt.BatchSize).ToArray();
                        if (ShouldReadFromStdin(files_))
                        {
                            batch_predictions = [GetMagikaResultFromStdIn(magikaObj)];
                        }
                        else
                        {
                            batch_predictions = magikaObj.IdentifyPaths(files_);
                        }

                        if (opt.JsonOutput)
                        {
                            all_predictions.AddRange(batch_predictions);
                        }
                        else if (opt.JsonLOutput)
                        {
                            foreach (var magika_result in batch_predictions)
                            {
                                Console.WriteLine(JsonSerializer.Serialize(magika_result, _jsonOptions));
                            }
                        }
                        else
                        {
                            foreach (var magika_result in batch_predictions)
                            {
                                string output;
                                var path = magika_result.path;
                                var output_ct_label = magika_result.output.ct_label;
                                var output_ct_description = magika_result.output.description;
                                var output_ct_group = magika_result.output.group;

                                if (opt.MimeOutput)
                                {
                                    output = magika_result.output.mime_type;
                                }
                                else if (opt.LabelOutput)
                                {
                                    output = output_ct_label;
                                }
                                else if (opt.CompatibilityMode)
                                {
                                    output = magika_result.output.magic;
                                }
                                else
                                {
                                    var dl_ct_label = magika_result.dl.ct_label;
                                    output = $"{output_ct_description} ({output_ct_group})";

                                    if (!string.IsNullOrEmpty(dl_ct_label) && dl_ct_label != output_ct_label)
                                    {
                                        var dl_description = magika_result.dl.description;
                                        var dl_group = magika_result.dl.group;
                                        var dl_score = (int)(magika_result.dl.score.Value * 100);
                                        output += $" [Low-confidence model best-guess: {dl_description} ({dl_group}), score={dl_score}]";
                                    }
                                }

                                if (UseColors)
                                {
                                    start_color = color_by_group.GetValueOrDefault(output_ct_group, Colors.WHITE);
                                    end_color = Colors.RESET;
                                }

                                if (opt.ScoreOutput)
                                {
                                    var score = (int)(magika_result.output.score * 100);
                                    Console.WriteLine($"{start_color}{path}: {output} {score}%{end_color}");
                                }
                                else
                                {
                                    Console.WriteLine($"{start_color}{path}: {output}{end_color}");
                                }
                            }
                        }
                    }

                    if (opt.JsonOutput)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(all_predictions, _jsonOptions));
                    }
                }
                finally
                {
                    magikaObj?.Dispose();
                }

                break;

            case ParserResultType.NotParsed:
                var notParsed = parseResult as NotParsed<Options>;

                break;
        }
    }

    static bool ShouldReadFromStdin(IEnumerable<string> files)
    {
        return files.Count() == 1 && files.First() == "-";
    }

    static MagikaResult GetMagikaResultFromStdIn(Magika magika)
    {
        byte[] buffer = new byte[4096];
        using Stream stdin = Console.OpenStandardInput();
        using MemoryStream ms = new();
        int bytesRead;
        while ((bytesRead = stdin.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, bytesRead);
        }
        return magika.IdentifyBytes(ms.ToArray());
    }

    static void PrintOutputContentTypesList()
    {
        var ctm = new ContentTypesManager();
        var contentTypes = ctm.GetOutputContentTypes();
        var table = new ConsoleTable(["#", "Content Type Label", "Description"]);
        var rows = new List<string>();
        foreach (var (ct, ct_idx) in contentTypes.Select((item, index) => (item, index)))
        {
            string description = string.IsNullOrWhiteSpace(ct.description) ? "" : ct.description;
            table.AddRow(ct_idx + 1, ct.name, description);
        }
        table.Write(Format.Minimal);
    }

    static List<string> GetFilesFromDirectory(string directory, bool noDereference)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var output = new List<string>();
        if (noDereference && File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint))
        {
            output.Add(directory);
        }
        else
        {
            var files = Directory.EnumerateFileSystemEntries(directory, "*", enumerationOptions);
            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    output.Add(file);
                    continue;
                }
                if (Directory.Exists(file))
                {
                    if (noDereference && File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                    {
                        output.Add(file);
                        continue;
                    }
                    else
                    {
                        output.AddRange(GetFilesFromDirectory(file, noDereference));
                    }
                }
            }
        }
        return output;
    }

    static void EnableVirtualTerminalProcessing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            IntPtr handle = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(handle, out uint mode);
            if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) == 0)
            {
                mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                SetConsoleMode(handle, mode);
            }
        }
        catch
        {
            // ignore any error
        }
    }
}