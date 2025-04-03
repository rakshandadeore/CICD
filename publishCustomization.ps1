param (
    [string]$versionName  # Accepts version name as an argument
)

if (-not $versionName) {
    Write-Host "Error: versionName parameter is required."
    exit 1
}

$zipFilePath1 = "Customization\$versionName\$versionName.zip"
$escapedPathzipFilePath = $zipFilePath1 -replace '\[', '`[' -replace '\]', '`]'
#$zipFilePath = "Customization\$versionName.zip"
$packageName = $versionName
 $serverUrl = $env:ACUMATICA_URL
$username = $env:ACUMATICA_USERNAME
 $password = $env:ACUMATICA_PASSWORD
 

# Ensure the ZIP file exists
if (-not (Test-Path $escapedPathzipFilePath)) {
    Write-Host "Error: Customization package '$zipFilePath1' not found. Cannot publish."
    exit 1
}

# Wrap the file path in double quotes and check its existence
$xmlFilePath = "Customization/$versionName/_project/ProjectMetadata.xml"
$escapedPath = $xmlFilePath -replace '\[', '`[' -replace '\]', '`]'
# Check if XML file exists
if (-not (Test-Path $escapedPath)) {
    Write-Host "Error: ProjectMetadata.xml file not found at '$xmlFilePath'"
    exit 1
}

# Load XML and extract project name (version name)

[xml]$xmlContent = Get-Content -Path $escapedPath 
$Level = $xmlContent.project.level
Write-Host "Level of PackG: $Level"
 #end

# Check if $Level is empty
if (-not $Level) {
    $Level = "0"
}

$cmd = "CustomizationPackageTools\bin\Release\net8.0\CustomizationPackageTools.exe"
&$cmd publish --packagefilename $zipFilePath1 --packagename $packageName --url $serverUrl --username $username --password $password --description "$versionName" --level $Level
