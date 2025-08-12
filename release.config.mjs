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
                    // Create platform-specific zips using Linux commands
                    "cd publish/win-x64 && zip -r ../../zips/AssetBundleBuilder-win-x64-v${nextRelease.version}.zip . && cd ../..",
                    "cd publish/linux-x64 && zip -r ../../zips/AssetBundleBuilder-linux-x64-v${nextRelease.version}.zip . && cd ../..",
                    "cd publish/osx-x64 && zip -r ../../zips/AssetBundleBuilder-osx-x64-v${nextRelease.version}.zip . && cd ../.."
                ].join(" && "),
                "publishCmd": `dotnet nuget push AssetBundleBuilder/bin/Release/*.nupkg --api-key ${process.env.NUGET_API_KEY} --source https://api.nuget.org/v3/index.json --skip-duplicate`
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
    tagFormat: "v${version}",
};