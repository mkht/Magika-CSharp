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

public record ModelFeatures(List<int> beg, List<int> mid, List<int> end);

record ModelOutput(string ct_label, float score);

public record MagikaResult(string path, ModelOutputFields dl, MagikaOutputFields output);

public record ModelOutputFields(string? ct_label, float? score, string? group, string? mime_type, string? magic, string? description)
{
    public string? magic { get; set; } = magic;
    public string? description { get; set; } = description;
}

public record MagikaOutputFields(string ct_label, float score, string group, string mime_type, string magic, string description)
{
    public string magic { get; set; } = magic;
    public string description { get; set; } = description;
}
