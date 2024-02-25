Push-Location $PSScriptRoot

$json = Get-Content "content_types_config.json" -raw |`
    ConvertFrom-Json -AsHashtable

$ret = @()
foreach ($key in $json.Keys) {
    $a = @()
    $a += "[`"{0}`"] = new (" -f $key
    $item = $json.$key
    foreach ($k in $item.Keys) {
        if ($k -eq "in_scope_for_training") {
            continue
        }
        $strobj = ConvertTo-Json $item.$k -Compress
        if ($k -eq "in_scope_for_output_content_type") {
            $a += "{0}: {1}" -f $k, $strobj
        }
        else{
            $a += "{0}: {1}," -f $k, $strobj
        }
    }
    $a += "),"
    $ret += ($a -join [System.Environment]::NewLine)
}
