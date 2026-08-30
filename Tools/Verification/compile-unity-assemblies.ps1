$ErrorActionPreference = 'Stop'
$editorData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data'
$dotnet = Join-Path $editorData 'DotNetSdk\dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll'
$frameworkRoot = Join-Path $editorData 'DotNetSdk\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1'
$templateRoot = Join-Path $editorData 'Resources\PackageManager\ProjectTemplates\libcache\com.unity.template.2d-cross-platform-2d-6.1.5\ScriptAssemblies'
$nunit = Join-Path $editorData 'Resources\PackageManager\BuiltInPackages\com.unity.ext.nunit\net472\unity-custom\nunit.framework.dll'
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('immortal-unity-compile-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null

function Quote([string]$value) { return '"' + $value.Replace('"', '\"') + '"' }

function Invoke-Compilation([string]$name, [string[]]$sources, [string[]]$references, [string[]]$defines) {
    $output = Join-Path $tempRoot ($name + '.dll')
    $response = Join-Path $tempRoot ($name + '.rsp')
    $lines = @('/nologo', '/warn:0', '/nostdlib', '/target:library', '/langversion:latest', ('/out:' + (Quote $output)))
    if ($defines.Count -gt 0) { $lines += '/define:' + ($defines -join ';') }
    foreach ($reference in ($references | Sort-Object -Unique)) { $lines += '/reference:' + (Quote $reference) }
    foreach ($source in $sources) { $lines += Quote $source }
    [IO.File]::WriteAllLines($response, $lines, [Text.UTF8Encoding]::new($false))
    $compilerOutput = @(& $dotnet $compiler ('@' + $response) 2>&1)
    $compilerExitCode = $LASTEXITCODE
    foreach ($line in $compilerOutput) { Write-Host $line }
    if ($compilerExitCode -ne 0) { throw "$name compilation failed with exit code $compilerExitCode" }
    return $output
}

try {
    $frameworkReferences = Get-ChildItem $frameworkRoot -Filter '*.dll' | ForEach-Object FullName
    $unityReferences = Get-ChildItem (Join-Path $editorData 'Managed\UnityEngine') -Filter '*.dll' | ForEach-Object FullName
    $ui = Join-Path $templateRoot 'UnityEngine.UI.dll'
    $runner = Join-Path $templateRoot 'UnityEngine.TestRunner.dll'
    $baseReferences = @($frameworkReferences) + @($unityReferences) + @($ui)
    $runtime = Invoke-Compilation 'ImmortalLoot.Runtime' (Get-ChildItem 'Assets\Game\Scripts' -Recurse -Filter '*.cs' | ForEach-Object FullName) $baseReferences @('UNITY_INCLUDE_TESTS')

    $editorReferences = @($frameworkReferences) + @(Get-ChildItem (Join-Path $editorData 'Managed') -Recurse -Filter '*.dll' | ForEach-Object FullName) + @($ui, $runtime)
    $editor = Invoke-Compilation 'ImmortalLoot.Editor' (Get-ChildItem 'Assets\Game\Editor' -Filter '*.cs' | ForEach-Object FullName) $editorReferences @()

    $editTestReferences = $editorReferences + @($editor, $runner, $nunit)
    Invoke-Compilation 'ImmortalLoot.Tests' (Get-ChildItem 'Assets\Game\Tests\EditMode' -Filter '*.cs' | ForEach-Object FullName) $editTestReferences @('UNITY_INCLUDE_TESTS') | Out-Null

    $playTestReferences = $baseReferences + @($runtime, $runner, $nunit)
    Invoke-Compilation 'ImmortalLoot.PlayModeTests' (Get-ChildItem 'Assets\Game\Tests\PlayMode' -Filter '*.cs' | ForEach-Object FullName) $playTestReferences @('UNITY_INCLUDE_TESTS') | Out-Null
    Write-Output 'PASS: Runtime, Editor, EditMode and PlayMode assemblies compiled against Unity netstandard 2.1 references.'
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
