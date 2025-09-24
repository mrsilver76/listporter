# PowerShell script to build and package a C# project for multiple architectures
# Version 1.0.4 - 19th September 2025

# === User Configurable Section ===

# Hardcoded list of target architectures
$architectures = @("win-x64", "linux-x64", "linux-arm", "linux-arm64", "osx-arm64", "osx-x64")

# === Helper Functions ===

# Function to parse version from the .csproj file and convert to semantic version
function Get-SemanticVersion {
    param(
        [string]$csprojPath
    )
    # Load XML from csproj
    [xml]$xml = Get-Content $csprojPath

    # Extract the <Version> tag content
    $versionRaw = $xml.Project.PropertyGroup.Version
    if (-not $versionRaw) {
        Write-Error "No <Version> tag found in $csprojPath"
        exit 1
    }

    # Split version by '.'
    $parts = $versionRaw.Split('.')
    if ($parts.Length -lt 4) {
        Write-Error "Version string '$versionRaw' does not have 4 parts."
        exit 1
    }

    # Construct semantic version: parts 0,1,3
    $major = $parts[0]
    $minor = $parts[1]
    $build = $parts[2]
    $revision = $parts[3]

    $semVer = "$major.$minor.$revision"
	# If the build number is > 0 then this is an interim release between
	# the last one and the next one, so append "-buildX" to the version
    if ([int]$build -gt 0) {
        $semVer += "-build$build"
    }
    return $semVer
}

# Function to delete all contents of a folder (files and subfolders)
function Clear-Folder {
    param(
        [string]$folderPath
    )
    if (Test-Path $folderPath) {
        Remove-Item "$folderPath\*" -Recurse -Force
    } else {
        # Create folder if it doesn't exist
        New-Item -ItemType Directory -Path $folderPath | Out-Null
    }
}

# Function to rename main executable in a folder, returns the new executable full path
function Rename-Executable {
    param(
        [string]$folderPath,
        [string]$projectName,
        [string]$version,
        [string]$architecture,
        [bool]$multipleArch
    )

    # Determine executable by architecture pattern
	if ($architecture -like "win-*") {
		# Windows executable ends with .exe
		$exeFiles = Get-ChildItem $folderPath -File | Where-Object { $_.Extension -ieq '.exe' }
	} else {
		# For Linux/macOS: pick files with NO extension
		$exeFiles = Get-ChildItem $folderPath -File | Where-Object { [string]::IsNullOrEmpty($_.Extension) }
	}

	if ($exeFiles.Count -eq 0) {
		Write-Error "No executable found in $folderPath for architecture $architecture"
		exit 1
	}

	# If multiple, pick the largest just in case
	$exe = $exeFiles | Sort-Object Length -Descending | Select-Object -First 1

    $baseName = $projectName
    $newName = "$baseName-$version"
    if ($multipleArch) {
        $newName += "-$architecture"
    }

    # Preserve extension (e.g. .exe)
    $newName += $exe.Extension

    $newPath = Join-Path $folderPath $newName

    # Rename
    Move-Item -Path $exe.FullName -Destination $newPath -Force

    return $newPath
}

# Function to archive (using zip) a folder excluding .pdb files
function ArchiveFolderExcludingPdb {
    param(
        [string]$sourceFolder,
        [string]$destinationZip
    )

    # Create a temp folder to copy filtered files
    $tempFolder = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Path $tempFolder | Out-Null

    # Copy all except .pdb files to temp folder preserving folder structure (flat here, so just files)
    Get-ChildItem $sourceFolder -Recurse -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
        $targetPath = Join-Path $tempFolder $_.Name
        Copy-Item $_.FullName $targetPath
    }

    # Compress the temp folder contents to zip
    Compress-Archive -Path "$tempFolder\*" -DestinationPath "$destinationZip" -Force

    # Remove temp folder
    Remove-Item -Recurse -Force $tempFolder
}

# === Main Script ===

# Find the .csproj file in current directory (assuming exactly one)
$csproj = Get-ChildItem -Filter *.csproj | Select-Object -First 1
if (-not $csproj) {
    Write-Error "No .csproj file found in current directory."
    exit 1
}

# Extract project name (filename without extension)
$projectName = [System.IO.Path]::GetFileNameWithoutExtension($csproj.Name)

# Get semantic version
$version = Get-SemanticVersion -csprojPath $csproj.FullName
Write-Host "Project version detected: $version"

$multipleArch = $architectures.Count -gt 1

# Create top-level Publish folder if missing
$publishRoot = Join-Path (Get-Location) "Publish"
if (Test-Path $publishRoot) {
    # Remove all contents (files and subdirectories), but not the folder itself
    Get-ChildItem -Path $publishRoot -Recurse -Force | Remove-Item -Recurse -Force
} else {
    # Create the folder if it doesn't exist
    New-Item -ItemType Directory -Path $publishRoot | Out-Null
}

foreach ($arch in $architectures) {

    Write-Host
    Write-Host "---------------------------------------------------------------------------"
    Write-Host "Building for architecture: $arch"
    Write-Host "---------------------------------------------------------------------------"
    Write-Host

    $publishFolder = Join-Path $publishRoot $arch

    # Clean publish folder
    Clear-Folder -folderPath $publishFolder

	& dotnet publish $csproj.FullName `
		-c Release `
		-r $arch `
		--self-contained false `
		/p:PublishSingleFile=true `
		/p:PublishTrimmed=false `
		/p:IncludeNativeLibrariesForSelfExtract=false `
		-o $publishFolder

	# Check exit code
	if ($LASTEXITCODE -ne 0) {
		Write-Error "dotnet publish failed for $arch with exit code $LASTEXITCODE"
		exit 1
	}

    # Rename executable
    Rename-Executable -folderPath $publishFolder -projectName $projectName -version $version -architecture $arch -multipleArch $multipleArch

    # Gather all files excluding .pdb
    $files = Get-ChildItem $publishFolder -File | Where-Object { $_.Extension -ne '.pdb' }

    if ($files.Count -gt 1) {
        # Zip everything except pdb files
        $zipName = "$projectName-$version-$arch.zip"
        $zipPath = Join-Path $publishRoot $zipName
        Write-Host "Creating zip archive: $zipName"
        ArchiveFolderExcludingPdb -sourceFolder $publishFolder -destinationZip $zipPath
    } elseif ($files.Count -eq 1) {
        # Copy the single file to Publish folder
        Write-Host "Copying single executable to Publish folder"
        Copy-Item $files[0].FullName -Destination $publishRoot -Force
    } else {
        Write-Warning "No files found to package for $arch"
    }
}

Write-Host
Write-Host "---------------------------------------------------------------------------"
Write-Host "Build and packaging completed."
Write-Host "---------------------------------------------------------------------------"