# Task Plan: Add Instrumentation Tests to Test Folder

## Todo Items

- [x] Create test project FennecLabs.Instrumentation.Tests in test folder
- [x] Copy AssemblyAnalyzerTests.cs and update namespace
- [x] Copy TestProjectReferenceAttribute.cs and update namespace
- [x] Copy TestProjectRefs.targets and update namespace references
- [x] Copy TestResources.cs and update namespace
- [x] Update project file with correct dependencies and project references
- [x] Copy TestProjects/BasicConsole test project
- [x] Update test project references to point to FennecLabs.Instrumentation
- [x] Add test project to solution
- [x] Test that the test project builds successfully

## Review

### Summary of Changes

1. **Created Test Project**: Created `FennecLabs.Instrumentation.Tests` xUnit test project in the `test/` folder with target framework net10.0.

2. **Copied Test Files**: Copied all test-related files from `/Users/nelson/github/Fennec.NetCore/test/Fennec.Instrumentation.Tests/`:
   - `AssemblyAnalyzerTests.cs` - Main test class with BasicConsoleResultTest
   - `TestProjectReferenceAttribute.cs` - Attribute for test project references
   - `TestProjectRefs.targets` - MSBuild targets for handling test project references
   - `TestResources.cs` - Helper class for getting test project assemblies

3. **Updated Namespaces**: Changed all namespaces from `Fennec.Instrumentation` to `FennecLabs.Instrumentation` to match the new project structure.

4. **Updated Project References**: 
   - Added project reference to `FennecLabs.Instrumentation` class library
   - Added test project reference to `BasicConsole` project
   - Updated `TestProjectRefs.targets` to use the new namespace in assembly attributes

5. **Copied Test Project**: Copied `TestProjects/BasicConsole` from the source repository:
   - Updated target framework from net8.0 to net10.0
   - Preserved all source code including Program.cs

6. **Added to Solution**: Added both the test project and the BasicConsole test project to the solution.

7. **Updated Dependencies**: The test project uses:
   - xUnit 2.9.3
   - Microsoft.NET.Test.Sdk 17.14.1
   - coverlet.collector 6.0.4
   - xunit.runner.visualstudio 3.1.4

### Files Modified
- `FennecLabs.sln`: Added test project and BasicConsole project

### Files Created
- `test/FennecLabs.Instrumentation.Tests/` - Complete test project with all test files
- `test/TestProjects/BasicConsole/` - Test project used by the tests

### Implementation Details
- All projects use net10.0 target framework for consistency
- Test project references the FennecLabs.Instrumentation class library
- TestProjectRefs.targets mechanism allows tests to dynamically find test project assemblies
- All tests pass successfully (1 test passed)
- Solution builds with no errors or warnings

### Testing
- Build succeeded with 0 warnings and 0 errors
- All tests pass: 1 test passed, 0 failed, 0 skipped
