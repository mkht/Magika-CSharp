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

namespace magika;

enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6
}

public static class Colors
{
    public const string BLACK = "\x1b[0;30m";
    public const string RED = "\x1b[0;31m";
    public const string GREEN = "\x1b[0;32m";
    public const string YELLOW = "\x1b[0;33m";
    public const string BLUE = "\x1b[0;34m";
    public const string PURPLE = "\x1b[0;35m";
    public const string CYAN = "\x1b[0;36m";
    public const string LIGHT_GRAY = "\x1b[0;37m";

    public const string DARK_GRAY = "\x1b[1;30m";
    public const string LIGHT_RED = "\x1b[1;31m";
    public const string LIGHT_GREEN = "\x1b[1;32m";
    public const string LIGHT_YELLOW = "\x1b[1;33m";
    public const string LIGHT_BLUE = "\x1b[1;34m";
    public const string LIGHT_PURPLE = "\x1b[1;35m";
    public const string LIGHT_CYAN = "\x1b[1;36m";
    public const string WHITE = "\x1b[1;37m";

    public const string RESET = "\x1b[0;39m";
}

class SimpleLogger(LogLevel logLevel = LogLevel.None, bool useColors = true)
{
    internal LogLevel LogLevel { get; set; } = logLevel;
    readonly bool useColors = useColors;

    const string DEBUG_MSG = "DEBUG: {0}";
    const string DEBUG_MSG_COLOR = $"{Colors.GREEN}DEBUG: {{0}}{Colors.RESET}";

    const string INFO_MSG = "INFO: {0}";

    const string WARN_MSG = "WARN: {0}";
    const string WARN_MSG_COLOR = $"{Colors.YELLOW}WARN: {{0}}{Colors.RESET}";

    const string ERROR_MSG = "ERROR: {0}";
    const string ERROR_MSG_COLOR = $"{Colors.RED}ERROR: {{0}}{Colors.RESET}";

    internal void Debug(string message)
    {
        if (this.LogLevel <= LogLevel.Debug)
        {
            if (this.useColors)
            {
                Console.WriteLine(DEBUG_MSG_COLOR, message);
            }
            else
            {
                Console.WriteLine(DEBUG_MSG, message);
            }
        }
    }

    internal void Info(string message)
    {
        if (this.LogLevel <= LogLevel.Information)
        {
            Console.WriteLine(INFO_MSG, message);
        }
    }

    internal void Warning(string message)
    {
        if (this.LogLevel <= LogLevel.Warning)
        {
            if (this.useColors)
            {
                Console.WriteLine(WARN_MSG_COLOR, message);
            }
            else
            {
                Console.WriteLine(WARN_MSG, message);
            }
        }
    }

    internal void Error(string message)
    {
        if (this.LogLevel <= LogLevel.Error)
        {
            if (this.useColors)
            {
                Console.WriteLine(ERROR_MSG_COLOR, message);
            }
            else
            {
                Console.WriteLine(ERROR_MSG, message);
            }
        }
    }
}
