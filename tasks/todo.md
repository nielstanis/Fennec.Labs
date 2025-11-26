# Task Plan: Create FennecLabs.DotNetCli Project

## Todo Items

- [x] Create new class library project FennecLabs.DotNetCli in src folder
- [x] Copy DotnetCliExecutor.cs and update namespace
- [x] Copy DotnetCliResult.cs and update namespace
- [x] Copy DotnetCliResultExtensions.cs and update namespace
- [x] Copy PackageListResult.cs and update namespace
- [x] Copy PackageReference.cs and update namespace
- [x] Copy Project.cs and update namespace
- [x] Copy Framework.cs and update namespace
- [x] Update project file with correct dependencies (if any)
- [x] Add FennecLabs.DotNetCli project to the solution
- [x] Test that the project builds successfully

## Review

### Summary of Changes

1. **Created New Class Library Project**: Created `FennecLabs.DotNetCli` class library project in the `src/` folder with target framework net10.0.

2. **Copied All Source Files**: Copied all DotNetCli-related files from `/Users/nelson/Research/consc/`:
   - `DotnetCliExecutor.cs` - Executes dotnet CLI commands via Process API
   - `DotnetCliResult.cs` - Record type for storing CLI execution results
   - `DotnetCliResultExtensions.cs` - Extension method to deserialize package list JSON
   - `PackageListResult.cs` - Record type for package list JSON structure
   - `PackageReference.cs` - Record type for package reference information
   - `Project.cs` - Record type for project information
   - `Framework.cs` - Record type for framework information with package lists

3. **Updated Namespaces**: Changed all namespaces from `Consc` to `FennecLabs.DotNetCli` to match the new project structure.

4. **Project Dependencies**: The project uses only standard .NET libraries:
   - System.Text.Json (included in .NET)
   - System.Diagnostics (for Process execution)
   - No additional NuGet packages required

5. **Added to Solution**: Added the new project to the solution.

### Files Modified
- `FennecLabs.sln`: Added FennecLabs.DotNetCli project

### Files Created
- `src/FennecLabs.DotNetCli/` - Complete class library with all DotNetCli functionality

### Implementation Details
- The `DotnetCliExecutor` class provides a static method `ExecuteAsync` that runs dotnet CLI commands
- It captures both standard output and standard error streams
- The `DotnetCliResultExtensions` provides a helper method to deserialize the JSON output from `dotnet list package` commands
- All data structures use C# records for immutability
- The project uses net10.0 target framework for consistency with other projects

### Usage Example
```csharp
var result = await DotnetCliExecutor.ExecuteAsync("list package --include-transitive --format json");
var packageList = result.DeserializePackageList();
// Access packageList.Projects, Frameworks, TopLevelPackages, TransitivePackages, etc.
```

### Testing
- Build succeeded with 0 warnings and 0 errors
- All projects in the solution build successfully
