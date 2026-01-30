#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs .NET 10, Claude Code, clones mcp-azure-devops repository, and configures Claude MCP.

.DESCRIPTION
    This script performs the following:
    1. Installs .NET 10 SDK using winget
    2. Installs Node.js if not present
    3. Installs Claude Code CLI if not present
    4. Clones the mcp-azure-devops repository
    5. Configures appsettings.json with Azure DevOps credentials
    6. Restores .NET dependencies
    7. Configures Claude Code MCP via HTTP transport (http://localhost:5000)

.PARAMETER InstallPath
    The path where the repository will be cloned. Default: $env:USERPROFILE\mcp-azure-devops

.PARAMETER OrganizationUrl
    Your Azure DevOps organization URL (e.g., https://dev.azure.com/your-org)

.PARAMETER PersonalAccessToken
    Your Azure DevOps Personal Access Token

.PARAMETER DefaultProject
    Your default Azure DevOps project name (optional)

.EXAMPLE
    .\install-mcp-azure-devops.ps1 -OrganizationUrl "https://dev.azure.com/myorg" -PersonalAccessToken "my-pat"
#>

param(
    [string]$InstallPath = "$env:USERPROFILE\mcp-azure-devops",

    [Parameter(Mandatory = $true)]
    [string]$OrganizationUrl,

    [Parameter(Mandatory = $true)]
    [string]$PersonalAccessToken,

    [string]$DefaultProject = ""
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n>> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

# Step 1: Check and install .NET 10
Write-Step "Checking .NET SDK installation..."

$dotnetVersion = $null
try {
    $dotnetVersion = dotnet --version 2>$null
} catch {
    $dotnetVersion = $null
}

if ($dotnetVersion -and $dotnetVersion -match "^10\.") {
    Write-Success ".NET 10 is already installed (version: $dotnetVersion)"
} else {
    Write-Step "Installing .NET 10 SDK..."

    # Check if winget is available
    $wingetAvailable = Get-Command winget -ErrorAction SilentlyContinue

    if ($wingetAvailable) {
        Write-Host "Using winget to install .NET 10 SDK..."
        winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "winget installation may have failed. Trying alternative method..."
            # Download and run the official installer script
            $installScript = "$env:TEMP\dotnet-install.ps1"
            Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
            & $installScript -Channel 10.0 -InstallDir "$env:ProgramFiles\dotnet"
        }
    } else {
        Write-Host "winget not available. Using official installer script..."
        $installScript = "$env:TEMP\dotnet-install.ps1"
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
        & $installScript -Channel 10.0 -InstallDir "$env:ProgramFiles\dotnet"
    }

    # Refresh PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

    # Verify installation
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK installed successfully (version: $dotnetVersion)"
}

# Step 2: Check and install Node.js
Write-Step "Checking Node.js installation..."

$nodeVersion = $null
try {
    $nodeVersion = node --version 2>$null
} catch {
    $nodeVersion = $null
}

if ($nodeVersion) {
    Write-Success "Node.js is already installed (version: $nodeVersion)"
} else {
    Write-Step "Installing Node.js..."

    $wingetAvailable = Get-Command winget -ErrorAction SilentlyContinue

    if ($wingetAvailable) {
        Write-Host "Using winget to install Node.js LTS..."
        winget install OpenJS.NodeJS.LTS --accept-source-agreements --accept-package-agreements

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install Node.js. Please install it manually from https://nodejs.org/"
        }
    } else {
        throw "winget is required to install Node.js. Please install Node.js manually from https://nodejs.org/"
    }

    # Refresh PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

    $nodeVersion = node --version
    Write-Success "Node.js installed successfully (version: $nodeVersion)"
}

# Step 3: Check and install Claude Code
Write-Step "Checking Claude Code installation..."

$claudeInstalled = $null
try {
    $claudeInstalled = Get-Command claude -ErrorAction SilentlyContinue
} catch {
    $claudeInstalled = $null
}

if ($claudeInstalled) {
    Write-Success "Claude Code is already installed"
} else {
    Write-Step "Installing Claude Code..."

    npm install -g @anthropic-ai/claude-code

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install Claude Code. Please run: npm install -g @anthropic-ai/claude-code"
    }

    # Refresh PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

    Write-Success "Claude Code installed successfully"
}

# Step 4: Clone the repository
Write-Step "Cloning mcp-azure-devops repository..."

if (Test-Path $InstallPath) {
    Write-Warning "Directory already exists: $InstallPath"
    $response = Read-Host "Do you want to remove it and clone fresh? (y/N)"
    if ($response -eq "y" -or $response -eq "Y") {
        Remove-Item -Recurse -Force $InstallPath
        git clone https://github.com/viamus/mcp-azure-devops.git $InstallPath
        Write-Success "Repository cloned to: $InstallPath"
    } else {
        Write-Host "Using existing directory..."
    }
} else {
    git clone https://github.com/viamus/mcp-azure-devops.git $InstallPath
    Write-Success "Repository cloned to: $InstallPath"
}

# Step 5: Configure appsettings.json
Write-Step "Configuring Azure DevOps credentials..."

$appSettingsPath = Join-Path $InstallPath "src\Viamus.Azure.Devops.Mcp.Server\appsettings.json"

$appSettings = @{
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    AllowedHosts = "*"
    AzureDevOps = @{
        OrganizationUrl = $OrganizationUrl
        PersonalAccessToken = $PersonalAccessToken
        DefaultProject = $DefaultProject
    }
}

$appSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $appSettingsPath -Encoding UTF8
Write-Success "appsettings.json configured successfully"

# Step 6: Restore dependencies
Write-Step "Restoring .NET dependencies..."
Push-Location $InstallPath
dotnet restore
Pop-Location
Write-Success "Dependencies restored"

# Step 7: Configure Claude Code MCP
Write-Step "Configuring Claude Code MCP (HTTP transport)..."

# Add azure-devops MCP server using claude CLI
claude mcp add azure-devops --transport http http://localhost:5000

if ($LASTEXITCODE -eq 0) {
    Write-Success "Claude Code MCP configured successfully"
} else {
    Write-Warning "Could not add MCP automatically. You can add it manually with:"
    Write-Host "  claude mcp add azure-devops --transport http http://localhost:5000"
}

# Summary
Write-Host "`n" -NoNewline
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Repository location: $InstallPath"
Write-Host "MCP Server URL: http://localhost:5000"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Start the MCP server:"
Write-Host "   cd $InstallPath"
Write-Host "   dotnet run --project src/Viamus.Azure.Devops.Mcp.Server"
Write-Host ""
Write-Host "2. The server will be available at http://localhost:5000"
Write-Host ""
Write-Host "3. Use Claude Code with the Azure DevOps MCP tools"
Write-Host ""
Write-Host "To verify the MCP configuration:" -ForegroundColor Yellow
Write-Host "  claude mcp list"
Write-Host ""
