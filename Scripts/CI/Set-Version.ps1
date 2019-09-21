<#
Generates the pipeline variables for the product version and set the build number.
#>
param (
    [Parameter(Mandatory)]
    [string]$SourceBranchCounter # should always be set to counter($(Build.SourceBranch), 0)
)

function Set-PipelineVariable {
    Param ([string]$VarName, [string]$VarValue)
    Write-Host "##vso[task.setvariable variable=$VarName;]$VarValue"
}

function Set-BuildNumber {
    Param([string]$BuildNumber)
    Write-Host "##vso[build.updatebuildnumber]$BuildNumber"
}

$SourceBranch = $Env:BUILD_SOURCEBRANCH
$SourceBranchName = $Env:BUILD_SOURCEBRANCHNAME

$ReleaseBranch = 'refs/heads/releases/(?<Year>\d{2})\.(?<Month>\d{2})\.(?<Milestone>\d{3})'
$PullRequest = 'refs/pull/(?<PrNum>\d+)/merge'

$Major = 0
$Minor = 0
$Revision = 0

if ($SourceBranch -match $ReleaseBranch) {
    $Major = $Matches["Year"]
    $Minor = $Matches["Month"]
    $Revision = $Matches["Milestone"]
    Set-BuildNumber "Release-$Major.$Minor.$Revision.$SourceBranchCounter"
    Set-PipelineVariable "SignBuild" 1
} elseif ($SourceBranch -match $PullRequest) {
    $Major = 10
    $Minor = 20
    $Revision = $Matches["PrNum"]
    Set-BuildNumber "PR-$Revision.$SourceBranchCounter"
} else {    # Typical CI scenario. Handles tags and non-release branches.
    $Major = (Get-Date -Format yy)
    $Minor = (Get-Date -Format MM)
    $Revision = (Get-Date -Format dd)
    Set-BuildNumber "CI-$SourceBranchName.$SourceBranchCounter"
}

Set-PipelineVariable "majorAndMinorVersion" "$Major.$Minor"
Set-PipelineVariable "revision" "$Revision.$SourceBranchCounter"
