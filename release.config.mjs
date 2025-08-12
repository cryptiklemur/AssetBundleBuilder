/**
 * @type {import('semantic-release').GlobalConfig}
 */
export default {
    branches: ["main", {name: 'beta', prerelease: true}, {name: 'alpha', prerelease: true}],
    "plugins": [
        "@semantic-release/commit-analyzer",
        "@semantic-release/release-notes-generator",
        [
            "@semantic-release/exec",
            {
                "prepareCmd": [
                    // Build and pack NuGet global tool
                    "dotnet pack AssetBundleBuilder/AssetBundleBuilder.csproj -c Release -p:PackageVersion=${nextRelease.version}",
                    // Publish platform-specific builds
                    "dotnet publish AssetBundleBuilder/AssetBundleBuilder.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/win-x64",
                    "dotnet publish AssetBundleBuilder/AssetBundleBuilder.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/linux-x64", 
                    "dotnet publish AssetBundleBuilder/AssetBundleBuilder.csproj -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ./publish/osx-x64",
                    // Create zips directory
                    "mkdir -p zips",
                    // Create platform-specific zips on Windows (using PowerShell)
                    "powershell -Command \"Compress-Archive -Path './publish/win-x64/*' -DestinationPath './zips/AssetBundleBuilder-win-x64-${nextRelease.version}.zip' -Force\"",
                    "powershell -Command \"Compress-Archive -Path './publish/linux-x64/*' -DestinationPath './zips/AssetBundleBuilder-linux-x64-${nextRelease.version}.zip' -Force\"",
                    "powershell -Command \"Compress-Archive -Path './publish/osx-x64/*' -DestinationPath './zips/AssetBundleBuilder-osx-x64-${nextRelease.version}.zip' -Force\""
                ].join(" && "),
                "publishCmd": "dotnet nuget push AssetBundleBuilder/bin/Release/*.nupkg --api-key ${NUGET_API_KEY} --source https://api.nuget.org/v3/index.json --skip-duplicate"
            }
        ],
        [
            "@semantic-release/github",
            {
                "assets": [
                    {path: './zips/*.zip'},
                    {path: './AssetBundleBuilder/bin/Release/*.nupkg'}
                ]
            }
        ]
    ],
    tagFormat: "${version}",
};