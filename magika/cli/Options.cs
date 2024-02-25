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

using CommandLine;

class Options
{
    [Option('r', "recursive", Required = false, HelpText = "When passing this option, magika scans every file within directories, instead of outputting \"directory\"")]
    public bool Recursive { get; set; } = false;

    [Option("json", Required = false, HelpText = "Output in JSON format.")]
    public bool JsonOutput { get; set; } = false;

    [Option("jsonl", Required = false, HelpText = "Output in JSONL format.")]
    public bool JsonLOutput { get; set; } = false;

    [Option('i', "mime-type", Required = false, HelpText = "Output the MIME type instead of a verbose content type description.")]
    public bool MimeOutput { get; set; } = false;

    [Option('l', "label", Required = false, HelpText = "Output a simple label instead of a verbose content type description. Use --list-output-content-types for the list of supported output.")]
    public bool LabelOutput { get; set; } = false;

    [Option('c', "compatibility-mode", Required = false, HelpText = "Compatibility mode: output is as close as possible to `file` and colors are disabled.")]
    public bool CompatibilityMode { get; set; } = false;

    [Option('s', "output-score", Required = false, HelpText = "Output the prediction's score in addition to the content type.")]
    public bool ScoreOutput { get; set; } = false;

    [Option('m', "prediction-mode", Required = false)]
    public string PredictionModeStr { get; set; } = "high-confidence";

    [Option("batch-size", Required = false, HelpText ="How many files to process in one batch.")]
    public int BatchSize { get; set; } = 32;

    [Option("no-dereference", Required = false, HelpText = "This option causes symlinks not to be followed. By default, symlinks are dereferenced.")]
    public bool NoDereference { get; set; } = false;

    [Option("no-colors", Required = false, HelpText = "Enable/disable use of colors.")]
    public bool NoColors { get; set; } = false;

    [Option("colors", Required = false, HelpText = "Enable/disable use of colors.")]
    public bool Colors { get; set; } = true;

    [Option('v', "verbose", Required = false, HelpText = "Enable more verbose output.")]
    public bool Verbose { get; set; } = false;

    [Option("debug", Required = false, HelpText = "Enable debug logging.")]
    public bool Debug { get; set; } = false;

    [Option("list-output-content-types", Required = false, HelpText = "Show a list of supported content types.")]
    public bool ListOutputContentTypes { get; set; } = false;
   
    // Others
    [Value(1, MetaName = "FILES")]
    public IEnumerable<string> Files { get; set; } = [];
}
