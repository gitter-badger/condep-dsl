$pwd = Split-Path $psake.build_script_file

Invoke-psake $pwd\default.ps1 -properties @{"preString"=""}