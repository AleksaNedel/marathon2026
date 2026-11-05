$ErrorActionPreference = 'Stop'

$region = 'us-west-2'
$seedDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$seedFiles = @(
    @{ Path = Join-Path $seedDirectory 'locations.json'; TableName = 'Locations' },
    @{ Path = Join-Path $seedDirectory 'tables.json'; TableName = 'Tables' },
    @{ Path = Join-Path $seedDirectory 'dishes.json'; TableName = 'Dishes' }
)
$allowedTables = @('Dishes', 'Locations', 'Tables')
$temporaryFiles = New-Object 'System.Collections.Generic.List[string]'
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)

function Write-CliInputFile {
    param([Parameter(Mandatory = $true)]$Value)

    $path = Join-Path $env:TEMP ("restaurant-seed-" + [guid]::NewGuid().ToString('N') + '.json')
    $json = ConvertTo-Json -InputObject $Value -Depth 100 -Compress
    [System.IO.File]::WriteAllText($path, $json, $utf8WithoutBom)
    $temporaryFiles.Add($path)
    return $path
}

function Invoke-DynamoDbRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Operation,
        [Parameter(Mandatory = $true)][string]$InputPath
    )

    $output = & aws dynamodb $Operation --cli-input-json "file://$InputPath" --region $region --output json
    if ($LASTEXITCODE -ne 0) {
        throw "aws dynamodb $Operation failed for input file '$InputPath' (exit code $LASTEXITCODE)."
    }

    return ($output | ConvertFrom-Json)
}

try {
    $loadedBatches = @()
    foreach ($seedFile in $seedFiles) {
        if (-not (Test-Path -LiteralPath $seedFile.Path -PathType Leaf)) {
            throw "Seed file not found: $($seedFile.Path)"
        }

        $batch = Get-Content -LiteralPath $seedFile.Path -Raw | ConvertFrom-Json
        $requestTableNames = @($batch.RequestItems.PSObject.Properties.Name)
        if ($requestTableNames.Count -ne 1 -or $requestTableNames[0] -ne $seedFile.TableName) {
            throw "Unexpected RequestItems table in '$($seedFile.Path)'."
        }
        if ($seedFile.TableName -notin $allowedTables) {
            throw "Refusing to access an unapproved table: $($seedFile.TableName)"
        }

        $requests = @($batch.RequestItems.($seedFile.TableName))
        if ($requests.Count -gt 25) {
            throw "Batch in '$($seedFile.Path)' exceeds DynamoDB's 25-item limit."
        }

        $seenIds = @{}
        foreach ($request in $requests) {
            if ($null -eq $request.PutRequest -or $request.PSObject.Properties.Name -contains 'DeleteRequest') {
                throw "Only PutRequest entries are allowed in '$($seedFile.Path)'."
            }

            $item = $request.PutRequest.Item
            $idProperty = $item.PSObject.Properties['id']
            if ($null -eq $idProperty -or [string]::IsNullOrWhiteSpace($idProperty.Value.S)) {
                throw "An item in '$($seedFile.Path)' is missing its string id key."
            }

            $id = $idProperty.Value.S
            if ($seenIds.ContainsKey($id)) {
                throw "Duplicate seed key '$id' in '$($seedFile.Path)'."
            }
            $seenIds[$id] = $true
        }

        $loadedBatches += [pscustomobject]@{
            TableName = $seedFile.TableName
            Requests = $requests
        }
    }

    foreach ($batch in $loadedBatches) {
        foreach ($request in $batch.Requests) {
            $id = $request.PutRequest.Item.id.S
            $getInput = Write-CliInputFile @{
                TableName = $batch.TableName
                Key = @{ id = @{ S = $id } }
                ConsistentRead = $true
                ProjectionExpression = 'id'
            }
            $existingItem = Invoke-DynamoDbRequest -Operation 'get-item' -InputPath $getInput
            if ($null -ne $existingItem.Item) {
                throw "Refusing to write: $($batch.TableName) already contains seed key '$id'. No seed writes have started."
            }
        }
    }

    foreach ($batch in $loadedBatches) {
        foreach ($request in $batch.Requests) {
            $putInput = Write-CliInputFile @{
                TableName = $batch.TableName
                Item = $request.PutRequest.Item
                ConditionExpression = 'attribute_not_exists(id)'
            }
            $null = Invoke-DynamoDbRequest -Operation 'put-item' -InputPath $putInput
            Write-Host "Wrote $($batch.TableName) item $($request.PutRequest.Item.id.S)"
        }
    }
}
finally {
    foreach ($temporaryFile in $temporaryFiles) {
        if (Test-Path -LiteralPath $temporaryFile -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}
