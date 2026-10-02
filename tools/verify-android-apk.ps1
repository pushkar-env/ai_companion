param(
    [Parameter(Mandatory=$true)][string]$Apk,
    [string]$BuildTools='C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0'
)
$ErrorActionPreference='Stop'
$apkPath=(Resolve-Path -LiteralPath $Apk).Path
$manifest=& (Join-Path $BuildTools 'aapt2.exe') dump xmltree $apkPath --file AndroidManifest.xml 2>&1
if($LASTEXITCODE -ne 0){throw "Unable to inspect Android manifest: $manifest"}
$text=$manifest -join "`n"
# aapt2 versions may render typed values as decimal/boolean or hexadecimal.
if($text -notmatch 'screenOrientation\([^)]*\)=\s*(?:1|0x0*1)(?:\s|$)'){throw 'Portrait orientation not found in packaged manifest'}
if($text -notmatch 'debuggable\([^)]*\)=\s*(?:true|0xffffffff)(?:\s|$)'){throw 'Expected a development/debuggable APK'}
if($text -notmatch 'package="com\.local\.aicompanion\.cctest"'){throw 'Unexpected diagnostic application ID'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($apkPath)
try {
    $entries=@($zip.Entries | ForEach-Object {$_.FullName})
    if($entries -notcontains 'lib/arm64-v8a/libil2cpp.so'){throw 'ARM64 IL2CPP library missing'}
    if(@($entries | Where-Object {$_ -match '^lib/(armeabi|x86)'}).Count -gt 0){throw 'Unexpected non-ARM64 native architecture'}
} finally {$zip.Dispose()}
$result=@('PASS packaged portrait orientation','PASS development/debuggable manifest','PASS diagnostic application ID','PASS ARM64 IL2CPP native library',('SHA256 '+(Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash))
$result | Set-Content -LiteralPath (Join-Path (Split-Path $apkPath) 'apk-verification.txt')
$result
