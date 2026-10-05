param(
    [Parameter(Mandatory=$true)][string]$DotnetRoot,
    [ValidateSet('Windows','Mac')][string]$Platform='Windows',
    [switch]$Preview
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskSdk=Get-ChildItem -LiteralPath (Join-Path $DotnetRoot 'sdk') -Directory | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
foreach($taskAssembly in @('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $taskSdk.FullName "Roslyn/bincore/$taskAssembly"))
}
$taskParse=[Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default
$taskTrees=[Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
Push-Location $taskRoot
try {
    if($Platform -eq 'Mac') {
        $taskParse=$taskParse.WithPreprocessorSymbols([string[]]@('CROSS_PLATFORM'))
        $taskSourcePaths=@(rg --files Mac AI Character Content Economy Habitat Inventory Persistence Simulation Input Updates -g '*.cs' -g '!Mac/bin/**' -g '!Mac/obj/**')
        $taskTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText('global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Net.Http; global using System.Threading; global using System.Threading.Tasks;', $taskParse))
    } else {
        $taskSourcePaths=@(rg --files -g '*.cs' -g '!Mac/**' -g '!obj/**' -g '!bin/**' -g '!release/**')
        # Reuse existing XAML-generated type stubs in memory; do not regenerate or emit files.
        foreach($taskGenerated in @('obj/Release/net8.0-windows/App.g.cs','obj/Release/net8.0-windows/UI/PetWindow.g.cs','obj/Release/net8.0-windows/GeneratedInternalTypeHelper.g.cs')) {
            $taskText=[IO.File]::ReadAllText((Join-Path $taskRoot $taskGenerated))
            if($taskGenerated -like '*PetWindow.g.cs') {$taskText=$taskText.Replace('System.Windows.Controls.Image','Momonga.UI.LayeredSprite')}
            $taskTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($taskText,$taskParse,$taskGenerated))
        }
    }
    foreach($taskPath in $taskSourcePaths) {
        $taskText=[IO.File]::ReadAllText((Join-Path $taskRoot $taskPath))
        if($Preview){$taskText=$taskText.Replace('pack://application:,,,/','pack://application:,,,/Momonga.Desktop;component/')}
        $taskTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($taskText,$taskParse,$taskPath))
    }
    $taskRefs=[Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
    $taskPacks=@('Microsoft.NETCore.App.Ref')
    if($Platform -eq 'Windows') {$taskPacks+='Microsoft.WindowsDesktop.App.Ref'}
    $taskDirectories=@()
    foreach($taskPack in $taskPacks) {
        $taskVersion=Get-ChildItem -LiteralPath (Join-Path $DotnetRoot "packs/$taskPack") -Directory | Sort-Object { [Version]$_.Name } -Descending | Select-Object -First 1
        $taskDirectories+=Join-Path $taskVersion.FullName 'ref/net8.0'
    }
    if($Platform -eq 'Mac') {$taskDirectories+=Join-Path $taskRoot 'Mac/bin/Release/net8.0'}
    foreach($taskDirectory in $taskDirectories) {
        foreach($taskFile in Get-ChildItem -LiteralPath $taskDirectory -Filter '*.dll') {
            if($taskFile.Name -ne 'Chiikawa.Companion.dll') {$taskRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($taskFile.FullName))}
        }
    }
    $taskOptions=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release)
    $taskCompilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('SourceCheck',$taskTrees.ToArray(),$taskRefs.ToArray(),$taskOptions)
    $taskErrors=@($taskCompilation.GetDiagnostics() | Where-Object Severity -eq 'Error')
    $taskErrors | ForEach-Object ToString
    if($taskErrors.Count) {exit 1}
    if($Preview) {
        if($Platform -ne 'Windows'){throw 'The in-memory preview currently uses WPF.'}
        $taskResources=[Collections.Generic.List[Microsoft.CodeAnalysis.ResourceDescription]]::new()
        $taskResourcePath=Join-Path $taskRoot 'obj/Release/net8.0-windows/win-x64/Momonga.Desktop.g.resources'
        $taskFactory=[Func[IO.Stream]]{[IO.File]::OpenRead($taskResourcePath)}.GetNewClosure()
        $taskResources.Add([Microsoft.CodeAnalysis.ResourceDescription]::new('Momonga.Desktop.g.resources',$taskFactory,$true))
        foreach($taskJson in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Content') -Filter '*.json') {
            $taskResourcePath=$taskJson.FullName
            $taskFactory=[Func[IO.Stream]]{[IO.File]::OpenRead($taskResourcePath)}.GetNewClosure()
            $taskResources.Add([Microsoft.CodeAnalysis.ResourceDescription]::new('Content/'+$taskJson.Name,$taskFactory,$true))
        }
        $taskCompilation=$taskCompilation.WithAssemblyName('Momonga.Desktop')
        $taskMemory=[IO.MemoryStream]::new()
        $taskResult=$taskCompilation.Emit($taskMemory,$null,$null,$null,$taskResources.ToArray())
        if(-not $taskResult.Success){$taskResult.Diagnostics | ForEach-Object ToString;throw 'Preview compilation failed'}
        $taskDesktop=Get-ChildItem -LiteralPath (Join-Path $DotnetRoot 'shared/Microsoft.WindowsDesktop.App') -Directory | Sort-Object {[Version]$_.Name} -Descending | Select-Object -First 1
        Add-Type 'using System.Runtime.InteropServices; public static class PreviewNative { [DllImport("kernel32",CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string path); }'
        [void][PreviewNative]::SetDllDirectory($taskDesktop.FullName)
        foreach($taskDll in Get-ChildItem -LiteralPath $taskDesktop.FullName -Filter '*.dll') {
            try{[void][Reflection.AssemblyName]::GetAssemblyName($taskDll.FullName)}catch [BadImageFormatException]{continue}
            [void][Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath($taskDll.FullName)
        }
        $taskPreviewAssembly=[Reflection.Assembly]::Load($taskMemory.ToArray())
        $taskPreviewAssembly.GetType('Momonga.UI.RigPreview').GetMethod('Run').Invoke($null,@())
        'Current-source preview rendered in memory to obj/rig-preview. No executable emitted or desktop launched.'
        exit 0
    }
    "$Platform source type check passed. No binaries emitted; runtime and XAML generation remain untested."
} finally {Pop-Location}
