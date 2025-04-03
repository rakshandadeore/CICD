param (
    [string]$versionName  # Accepts version name as an argument
)

if (-not $versionName) {
    Write-Host "Error: versionName parameter is required."
    exit 1
}

$customizationPath = "Customization\$versionName"
$zipFileName = "Customization\$versionName\$versionName.zip"
 Write-Host "Path   :" $customizationPath



# Wrap the file path in double quotes and check its existence



$xmlFilePath1 = "Customization/$versionName/_project/ProjectMetadata.xml"

$escapedPath = $xmlFilePath1 -replace '\[', '`[' -replace '\]', '`]'
# Check if XML file exists
if (-not (Test-Path $escapedPath)) {
    Write-Host "Error: ProjectMetadata.xml file not found at '$xmlFilePath1'"
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



# Ensure the version folder exists
if (-not (Test-Path -LiteralPath $customizationPath)) {
    Write-Host "Error: Customization folder '$customizationPath' does not exist."
    exit 1
}

# Check if the directory is empty
#$files = Get-ChildItem -Path $customizationPath+ -Recurse -Force
$files = Get-ChildItem -Path "Customization" -Recurse -Force
if (-not $files) {
    Write-Host "Error: Customization files not exist. Not able to generate ZIP......"
    exit 1
}

$cmd = "CustomizationPackageTools\bin\Release\net8.0\CustomizationPackageTools.exe"
&$cmd build --customizationpath $customizationPath --packagefilename $zipFileName --description "$versionName" --level $Level

Write-Host "Customization package created successfully: $zipFileName"
