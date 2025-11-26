# Task Plan: Create FennecLabs.NuGet Project

## Todo Items

- [ ] Create new class library project FennecLabs.NuGet in src folder
- [ ] Copy NuGetService.cs and update namespace
- [ ] Copy FeedService.cs and update namespace
- [ ] Copy FeedConfiguration.cs and update namespace
- [ ] Copy ConfigurationManager.cs and update namespace
- [ ] Update project file with correct dependencies (NuGet.Protocol package)
- [ ] Update namespaces from Consc to FennecLabs.NuGet
- [ ] Add FennecLabs.NuGet project to the solution
- [ ] Test that the project builds successfully

## Review

### Summary of Changes

Successfully created the `FennecLabs.NuGet` project and ported all NuGet functionality from the `consc` project.

**Files Created:**
1. `src/FennecLabs.NuGet/FennecLabs.NuGet.csproj` - New class library project with NuGet.Protocol package reference
2. `src/FennecLabs.NuGet/NuGetService.cs` - Main service for NuGet operations (search, download, metadata, etc.)
3. `src/FennecLabs.NuGet/FeedService.cs` - Service for managing NuGet feeds
4. `src/FennecLabs.NuGet/FeedConfiguration.cs` - Configuration model for feeds
5. `src/FennecLabs.NuGet/ConfigurationManager.cs` - Manages application settings and feed configurations
6. `src/FennecLabs.NuGet/AppSettings.cs` - Application settings model
7. `src/FennecLabs.NuGet/PackageFileInfo.cs` - Model for package file information

**Key Changes:**
- Updated all namespaces from `Consc` to `FennecLabs.NuGet`
- Changed configuration directory from `.consc` to `.fennec` in `ConfigurationManager`
- Added `NuGet.Protocol` package reference (version 6.14.0)
- All files successfully ported with proper namespace updates
- Project added to solution and builds successfully

**Functionality Ported:**
- Package search
- Package version retrieval
- Package metadata retrieval
- Package download
- Package contents listing
- Package file extraction
- Feed management (add, remove, set default)
- Configuration management with JSON persistence

All tests pass and the solution builds without errors.
