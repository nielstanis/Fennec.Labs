# Task Plan: Add Solution and Create FennecLabs.Instrumentation Class Library

## Todo Items

- [x] Create a solution file (FennecLabs.sln)
- [x] Add existing FennecLabs project to the solution
- [x] Create new class library project FennecLabs.Instrumentation
- [x] Copy AnalyseAssembly.cs and update namespace
- [x] Copy Analytics folder files (Category.cs, Result.cs, Rules.cs) and update namespace
- [x] Copy Output folder files (FxtWriter.cs, JsonWriter.cs, Writer.cs, WriterFactory.cs) and update namespace
- [x] Copy Result folder files (AssemblyResult.cs, ClassTypeResult.cs, InvocationResult.cs, MethodResult.cs) and update namespace
- [x] Update project file with correct dependencies (mono.cecil package)
- [x] Add FennecLabs.Instrumentation project to the solution
- [x] Test that both projects build successfully

## Review

### Summary of Changes

1. **Created Solution File**: Created `FennecLabs.sln` to contain both projects.

2. **Added Existing Project to Solution**: Added the existing `FennecLabs.csproj` project to the solution.

3. **Created Class Library Project**: Created a new class library project `FennecLabs.Instrumentation` with the same target framework (net10.0) as the main project.

4. **Copied Source Files**: Copied all functional implementation files from `/Users/nelson/github/Fennec.NetCore/src/Fennec.Instrumentation/`:
   - `AnalyseAssembly.cs` - Main assembly analyzer class
   - `Analytics/` folder: `Category.cs`, `Result.cs`, `Rules.cs`
   - `Output/` folder: `FxtWriter.cs`, `JsonWriter.cs`, `Writer.cs`, `WriterFactory.cs`
   - `Result/` folder: `AssemblyResult.cs`, `ClassTypeResult.cs`, `InvocationResult.cs`, `MethodResult.cs`

5. **Updated Namespaces**: Changed all namespaces from `Fennec.Instrumentation` to `FennecLabs.Instrumentation` to match the new project structure.

6. **Updated Project Dependencies**: Added `mono.cecil` package reference (version 0.11.5) to the `FennecLabs.Instrumentation.csproj` project file.

7. **Fixed Nullable Warnings**: Updated `AssemblyResult.cs` to make `ExceptionOccurred` nullable and added null checks in `AnalyseAssembly.cs` to handle nullable operands.

8. **Fixed Project Compilation Issue**: Added exclusion rules to `FennecLabs.csproj` to prevent it from compiling files from the `FennecLabs.Instrumentation` folder, which was causing build errors.

### Files Modified
- `FennecLabs.csproj`: Added exclusion rules for the Instrumentation folder
- `FennecLabs.sln`: Created solution file with both projects
- `FennecLabs.Instrumentation/FennecLabs.Instrumentation.csproj`: Created with mono.cecil dependency

### Files Created
- All source files in `FennecLabs.Instrumentation/` directory structure matching the original project

### Implementation Details
- The class library uses the same target framework (net10.0) as the main project
- All namespaces have been updated from `Fennec.Instrumentation` to `FennecLabs.Instrumentation`
- The project successfully builds with no errors or warnings
- Both projects in the solution build successfully
