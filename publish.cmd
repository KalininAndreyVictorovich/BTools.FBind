call build-release && nuget pack -OutputDirectory bin && nuget push bin\*.nupkg && del bin\*.nupkg